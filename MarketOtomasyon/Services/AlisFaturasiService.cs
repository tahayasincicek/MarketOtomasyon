using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Alis faturasi kaydi. Fatura mal kabulu DEGISTIRMEZ, SARMALAR: her
/// satir icin StokService.MalKabulYazAsync ile ayni transaction icinde
/// stok hareketi ve FEFO partisi acilir. Boylece stok ile belge hicbir
/// durumda birbirinden ayrilamaz - yedinci satirda hata cikarsa ilk alti
/// satir da geri alinir.
/// </summary>
public sealed class AlisFaturasiService
{
    private readonly IDbConnectionFactory _factory;
    private readonly AlisFaturasiRepository _faturaRepository;
    private readonly TedarikciRepository _tedarikciRepository;
    private readonly DepoRepository _depoRepository;
    private readonly StokService _stokService;
    private readonly MaliyetRepository _maliyetRepository;
    private readonly ILogger<AlisFaturasiService> _kayit;

    public AlisFaturasiService(
        IDbConnectionFactory factory,
        AlisFaturasiRepository faturaRepository,
        TedarikciRepository tedarikciRepository,
        DepoRepository depoRepository,
        StokService stokService,
        MaliyetRepository maliyetRepository,
        ILogger<AlisFaturasiService> kayit)
    {
        _factory = factory;
        _faturaRepository = faturaRepository;
        _tedarikciRepository = tedarikciRepository;
        _depoRepository = depoRepository;
        _stokService = stokService;
        _maliyetRepository = maliyetRepository;
        _kayit = kayit;
    }

    public async Task<IReadOnlyList<Tedarikci>> TedarikcilerAsync(CancellationToken ct = default)
        => await _tedarikciRepository.AktifleriGetirAsync(ct);

    public async Task<IReadOnlyList<Models.Entities.Depo>> DepolarAsync(CancellationToken ct = default)
        => await _depoRepository.AktifleriGetirAsync(ct);

    public async Task<IReadOnlyList<AlisFaturasiGecmisSatirVm>> SonFaturalarAsync(CancellationToken ct = default)
        => await _faturaRepository.SonFaturalarAsync(20, ct);

    public async Task<AlisFaturasiDetayVm?> DetayGetirAsync(int id, CancellationToken ct = default)
        => await _faturaRepository.DetayGetirAsync(id, ct);

    public async Task<(string? FaturaNo, string? Hata)> KaydetAsync(
        int tedarikciId,
        int depoId,
        string? faturaNo,
        DateTime faturaTarihi,
        IReadOnlyList<AlisFaturasiSatirVm> satirlar,
        string? aciklama,
        int kullaniciId,
        CancellationToken ct = default)
    {
        if (tedarikciId <= 0) return (null, "Tedarikçi seçilmedi.");
        if (depoId <= 0) return (null, "Depo seçilmedi.");
        if (string.IsNullOrWhiteSpace(faturaNo)) return (null, "Fatura numarası zorunludur.");

        var (tarihGecerli, tarihHatasi) = TedarikciKurallari.FaturaTarihiGecerliMi(
            faturaTarihi, DateTime.Today);
        if (!tarihGecerli) return (null, tarihHatasi);

        var (satirGecerli, satirHatasi) = TedarikciKurallari.SatirlarGecerliMi(satirlar);
        if (!satirGecerli) return (null, satirHatasi);

        var temizFaturaNo = faturaNo.Trim();

        // Kesin koruma UQ_AlisFat_No kisitidir; bu erken uyaridir. Kontrol
        // ile insert arasinda baska bir kullanici ayni faturayi girebilir,
        // o durumda asagidaki kisit ihlalini yakalariz.
        if (await _faturaRepository.FaturaVarMiAsync(tedarikciId, temizFaturaNo, ct))
            return (null, $"{temizFaturaNo} numaralı fatura bu tedarikçi için zaten kayıtlı.");

        // Tutarlar saf hesaplayiciyla, veritabanina dokunmadan hesaplanir.
        // Alis fiyati KDV HARICTIR; KDV matrahin ustune eklenir - satisin
        // tersi yonde (bkz. FaturaHesaplayici basindaki aciklama).
        var hesaplanan = satirlar.Select(s => new AlisFaturasiSatirVm
        {
            UrunId = s.UrunId,
            UrunKod = s.UrunKod,
            UrunAd = s.UrunAd,
            Birim = s.Birim,
            Miktar = s.Miktar,
            BirimFiyat = s.BirimFiyat,
            KdvOrani = s.KdvOrani,
            SonKullanmaTarihi = s.SonKullanmaTarihi,
            LotNo = s.LotNo,
            SatirMatrah = FaturaHesaplayici.SatirMatrahHesapla(s.Miktar, s.BirimFiyat)
        }).ToList();

        foreach (var satir in hesaplanan)
            satir.SatirKdv = FaturaHesaplayici.SatirKdvHesapla(satir.SatirMatrah, satir.KdvOrani);

        var araToplam = hesaplanan.Sum(s => s.SatirMatrah);
        var toplamKdv = hesaplanan.Sum(s => s.SatirKdv);
        var genelToplam = araToplam + toplamKdv;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var faturaId = await _faturaRepository.EkleAsync(conn, tx, new AlisFaturasi
        {
            TedarikciId = tedarikciId,
            FaturaNo = temizFaturaNo,
            FaturaTarihi = faturaTarihi.Date,
            KullaniciId = kullaniciId,
            DepoId = depoId,
            AraToplam = araToplam,
            ToplamKdv = toplamKdv,
            GenelToplam = genelToplam,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        }, ct);

        var satirNo = 1;
        foreach (var satir in hesaplanan)
        {
            var satirId = await _faturaRepository.SatirEkleAsync(conn, tx, new Models.Entities.AlisFaturasiSatir
            {
                FaturaId = faturaId,
                SatirNo = satirNo++,
                UrunId = satir.UrunId,
                Miktar = satir.Miktar,
                BirimFiyat = satir.BirimFiyat,
                KdvOrani = satir.KdvOrani,
                SatirMatrah = satir.SatirMatrah,
                SatirKdv = satir.SatirKdv,
                SonKullanmaTarihi = satir.SonKullanmaTarihi,
                LotNo = satir.LotNo
            }, ct);

            try
            {
                // Parti maliyeti KDV HARIC birim fiyattir - alis KDV'si
                // indirilebilir oldugu icin maliyete girmez.
                await _stokService.MalKabulYazAsync(
                    conn, tx, satir.UrunId, depoId, satir.Miktar, satir.BirimFiyat,
                    $"Alış faturası {temizFaturaNo}",
                    satir.SonKullanmaTarihi,
                    satir.LotNo,
                    tedarikciId,
                    satirId,
                    ct: ct);
            }
            catch (ArgumentException ex)
            {
                tx.Rollback();

                _kayit.LogWarning(
                    "Alış faturası reddedildi {FaturaNo} {UrunId} {Sebep}",
                    temizFaturaNo, satir.UrunId, ex.Message);

                return (null, $"{satir.UrunAd}: {ex.Message.Split(" (Parameter")[0]}");
            }
        }

        tx.Commit();

        _kayit.LogInformation(
            "Alış faturası kaydedildi {FaturaNo} {TedarikciId} {GenelToplam} {SatirSayisi}",
            temizFaturaNo, tedarikciId, genelToplam, hesaplanan.Count);

        return (temizFaturaNo, null);
    }
}
