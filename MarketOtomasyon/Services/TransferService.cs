using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Depolar arasi transfer. Tek transaction icinde cikis, giris ve
/// PARTI TASIMA yapar.
///
/// Parti tasima olmazsa olmaz: StokParti depo bazlidir. Yalnizca iki
/// stok hareketi yazilsaydi hedef depoda vw_StokBakiye uzerinden bakiye
/// gorunur ama parti bulunmaz; kasada satis aninda FifoTuketAsync
/// "parti bakiyesi yetersiz" dondurur ve satis tamamen kirilir.
/// </summary>
public sealed class TransferService
{
    private const byte YonGiris = 1;
    private const byte YonCikis = 2;

    /// <summary>01_ilk_sema.sql'deki kaynak tipi listesinde 7 numara.</summary>
    private const byte KaynakTransfer = 7;

    private readonly IDbConnectionFactory _factory;
    private readonly TransferRepository _transferRepository;
    private readonly StokRepository _stokRepository;
    private readonly MaliyetService _maliyetService;
    private readonly DepoRepository _depoRepository;
    private readonly ILogger<TransferService> _kayit;

    public TransferService(
        IDbConnectionFactory factory,
        TransferRepository transferRepository,
        StokRepository stokRepository,
        MaliyetService maliyetService,
        DepoRepository depoRepository,
        ILogger<TransferService> kayit)
    {
        _factory = factory;
        _transferRepository = transferRepository;
        _stokRepository = stokRepository;
        _maliyetService = maliyetService;
        _depoRepository = depoRepository;
        _kayit = kayit;
    }

    public async Task<IReadOnlyList<Depo>> DepolarAsync(CancellationToken ct = default)
        => await _depoRepository.AktifleriGetirAsync(ct);

    public async Task<IReadOnlyList<TransferGecmisSatirVm>> SonTransferlerAsync(
        CancellationToken ct = default)
        => await _transferRepository.SonTransferlerAsync(20, ct);

    public async Task<decimal> BakiyeAsync(int urunId, int depoId, CancellationToken ct = default)
        => await _stokRepository.BakiyeAsync(urunId, depoId, ct);

    public async Task<(string? TransferNo, string? Hata)> TransferEtAsync(
        int kaynakDepoId,
        int hedefDepoId,
        IReadOnlyList<TransferSatirVm> satirlar,
        string? aciklama,
        int kullaniciId,
        CancellationToken ct = default)
    {
        // Kurallar transaction acilmadan once: gecersiz girdi icin bosuna
        // baglanti acip kilit tutmanin anlami yok.
        var (depoGecerli, depoHatasi) = TransferKurallari.DepolarGecerliMi(kaynakDepoId, hedefDepoId);
        if (!depoGecerli) return (null, depoHatasi);

        var (satirGecerli, satirHatasi) = TransferKurallari.SatirlarGecerliMi(satirlar);
        if (!satirGecerli) return (null, satirHatasi);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var (transferId, transferNo) = await _transferRepository.EkleAsync(conn, tx, new StokTransfer
        {
            KaynakDepoId = kaynakDepoId,
            HedefDepoId = hedefDepoId,
            KullaniciId = kullaniciId,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        }, ct);

        foreach (var satir in satirlar)
        {
            await _transferRepository.SatirEkleAsync(conn, tx, new StokTransferSatir
            {
                TransferId = transferId,
                UrunId = satir.UrunId,
                Miktar = satir.Miktar
            }, ct);

            // 1) Kaynak depodan cikis hareketi.
            var cikisId = await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
            {
                UrunId = satir.UrunId,
                DepoId = kaynakDepoId,
                Yon = YonCikis,
                Miktar = satir.Miktar,
                KaynakTip = KaynakTransfer,
                KaynakId = transferId,
                Aciklama = $"Transfer {transferNo} çıkış"
            }, ct);

            // 2) Kaynak partileri FEFO ile tuket. fisSatirId null: bu bir
            //    satis degil. Partiler UPDLOCK ile okundugu icin ayri bir
            //    bakiye kontrolune gerek yok; yetersizse burasi hata doner.
            var tuketim = await _maliyetService.FifoTuketAsync(
                conn, tx, satir.UrunId, kaynakDepoId, cikisId, fisSatirId: null, satir.Miktar, ct);

            if (!tuketim.Basarili)
            {
                tx.Rollback();

                _kayit.LogWarning(
                    "Transfer iptal: kaynak depoda stok yetersiz {UrunId} {DepoId} {Istenen}",
                    satir.UrunId, kaynakDepoId, satir.Miktar);

                return (null, $"{satir.UrunAd}: kaynak depoda yeterli stok yok.");
            }

            // 3) Tuketilen HER parti icin hedefte AYRI giris hareketi ve
            //    AYRI parti. Ayri hareket sart: UX_StokParti_Hareket
            //    benzersiz oldugu icin bir harekete tek parti baglanabilir.
            foreach (var parca in tuketim.Tuketimler)
            {
                var girisId = await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
                {
                    UrunId = satir.UrunId,
                    DepoId = hedefDepoId,
                    Yon = YonGiris,
                    Miktar = parca.Miktar,
                    KaynakTip = KaynakTransfer,
                    KaynakId = transferId,
                    Aciklama = $"Transfer {transferNo} giriş"
                }, ct);

                // Maliyet, son kullanma tarihi ve lot AYNEN tasinir: tasinan
                // sey ayni fiziksel mal, transfer kar ya da zarar uretmez.
                // Tedarikci null: transfer bir satin alma degil.
                await _maliyetService.PartiAcAsync(
                    conn, tx, satir.UrunId, hedefDepoId, girisId,
                    parca.Miktar, parca.BirimMaliyet,
                    $"Transfer {transferNo}",
                    parca.SonKullanmaTarihi,
                    parca.LotNo,
                    tedarikciAdi: null,
                    ct: ct);
            }
        }

        tx.Commit();

        _kayit.LogInformation(
            "Transfer tamamlandi {TransferNo} {KaynakDepoId} {HedefDepoId} {SatirSayisi}",
            transferNo, kaynakDepoId, hedefDepoId, satirlar.Count);

        return (transferNo, null);
    }
}
