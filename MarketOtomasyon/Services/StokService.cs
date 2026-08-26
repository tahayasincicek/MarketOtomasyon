using System.Data;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Services;

/// <summary>
/// Stok hareketlerini uretir. Stok her zaman hareket yazilarak degisir;
/// hicbir yerde bakiye kolonu guncellenmez.
/// </summary>
public class StokService
{
    /// <summary>KaynakTip: 3 = mal kabul.</summary>
    private const byte KaynakMalKabul = 3;
    private const byte YonGiris = 1;

    private readonly IDbConnectionFactory _factory;
    private readonly StokRepository _stokRepository;
    private readonly MaliyetService _maliyetService;
    private readonly UrunRepository _urunRepository;

    public StokService(
        IDbConnectionFactory factory,
        StokRepository stokRepository,
        MaliyetService maliyetService,
        UrunRepository urunRepository)
    {
        _factory = factory;
        _stokRepository = stokRepository;
        _maliyetService = maliyetService;
        _urunRepository = urunRepository;
    }

    /// <summary>Mal kabul: depoya giris hareketi yazar ve olusan yeni bakiyeyi doner.</summary>
    public async Task<decimal> MalKabulAsync(
        int urunId,
        int depoId,
        decimal miktar,
        decimal birimMaliyet,
        string? aciklama,
        DateTime? sonKullanmaTarihi = null,
        string? lotNo = null,
        int? tedarikciId = null,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await MalKabulYazAsync(
            conn, tx, urunId, depoId, miktar, birimMaliyet, aciklama,
            sonKullanmaTarihi, lotNo, tedarikciId, alisFaturasiSatirId: null, ct: ct);

        tx.Commit();

        return await _stokRepository.BakiyeAsync(urunId, depoId, ct);
    }

    /// <summary>
    /// Mal kabulun cekirdegi. Acik bir transaction icinde calisir; cagiran
    /// commit/rollback sorumlulugunu ustlenir.
    ///
    /// Alis faturasi bunu satir basina cagirir ve tumu TEK transaction'da
    /// yurur. MalKabulAsync (yukarida) kendi transaction'ini acip bunu
    /// tek satir icin cagiran bir sarmalayicidir - eger fatura da bu
    /// sarmalayiciyi cagirsaydi 10 satirlik fatura 10 ayri transaction
    /// olurdu ve yedinci satirda hata cikarsa ilk alti satir stoga
    /// girmis, fatura kaydedilmemis olurdu.
    /// </summary>
    public async Task<long> MalKabulYazAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        decimal miktar,
        decimal birimMaliyet,
        string? aciklama,
        DateTime? sonKullanmaTarihi = null,
        string? lotNo = null,
        int? tedarikciId = null,
        int? alisFaturasiSatirId = null,
        CancellationToken ct = default)
    {
        if (miktar <= 0)
            throw new ArgumentOutOfRangeException(nameof(miktar), "Mal kabul miktarı sıfırdan büyük olmalıdır.");
        if (birimMaliyet <= 0)
            throw new ArgumentOutOfRangeException(nameof(birimMaliyet), "Birim maliyet sıfırdan büyük olmalıdır.");

        // Kural kontrolu transaction acilmadan once konulamiyor cunku bu
        // metot zaten acik bir transaction icinde cagriliyor; ama urun
        // sorgusu ayni transaction disinda (salt okunur, kilit gerekmez).
        var urun = await _urunRepository.GetirAsync(urunId, ct)
            ?? throw new ArgumentException("Ürün bulunamadı.", nameof(urunId));

        var (sktGecerli, sktHatasi) = PartiKurallari.SonKullanmaGecerliMi(
            sonKullanmaTarihi, urun.SonKullanmaZorunlu, DateTime.Today);
        if (!sktGecerli) throw new ArgumentException(sktHatasi, nameof(sonKullanmaTarihi));

        var (lotGecerli, lotHatasi) = PartiKurallari.LotGecerliMi(lotNo);
        if (!lotGecerli) throw new ArgumentException(lotHatasi, nameof(lotNo));

        var hareketId = await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
        {
            UrunId = urunId,
            DepoId = depoId,
            Yon = YonGiris,
            Miktar = miktar,
            KaynakTip = KaynakMalKabul,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        }, ct);

        await _maliyetService.PartiAcAsync(
            conn,
            tx,
            urunId,
            depoId,
            hareketId,
            miktar,
            birimMaliyet,
            aciklama,
            sonKullanmaTarihi,
            lotNo,
            tedarikciId,
            alisFaturasiSatirId,
            ct: ct);

        return hareketId;
    }
}
