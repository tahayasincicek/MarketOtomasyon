using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Fisin tahsilatini yonetir. Bir fise birden fazla odeme baglanabilir;
/// kalan borc sifirlanana kadar fis Beklemede kalir, sifirlaninca Tamamlandi
/// durumuna gecer. Odeme silinirse fis yeniden Beklemede'ye doner.
/// </summary>
public class OdemeService
{
    private const byte DurumBeklemede = 1;
    private const byte DurumTamamlandi = 2;

    private readonly IDbConnectionFactory _factory;
    private readonly FisRepository _fisRepository;
    private readonly OdemeRepository _odemeRepository;
    private readonly SatisService _satisService;
    private readonly StokRepository _stokRepository;

    public OdemeService(
        IDbConnectionFactory factory,
        FisRepository fisRepository,
        OdemeRepository odemeRepository,
        SatisService satisService,
        StokRepository stokRepository)
    {
        _factory = factory;
        _fisRepository = fisRepository;
        _odemeRepository = odemeRepository;
        _satisService = satisService;
        _stokRepository = stokRepository;
    }

    /// <summary>Vardiyadaki acik fisin odeme durumu; acik fis yoksa bos doner.</summary>
    public async Task<OdemeDurumVm> DurumAsync(int vardiyaId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        return fis is null ? new OdemeDurumVm() : await DurumAsync(fis, ct);
    }

    private async Task<OdemeDurumVm> DurumAsync(Fis fis, CancellationToken ct)
    {
        var odemeler = await _odemeRepository.FisOdemeleriAsync(fis.Id, ct);

        return new OdemeDurumVm
        {
            FisId = fis.Id,
            FisNo = fis.FisNo,
            GenelToplam = fis.GenelToplam,
            Odenen = odemeler.Sum(o => o.Tutar),
            Odemeler = odemeler,
            Tamamlandi = fis.Durum == DurumTamamlandi,
            ToplamParaUstu = odemeler.Sum(o => o.ParaUstu ?? 0)
        };
    }

    /// <summary>
    /// Fise odeme ekler. Kalan borc sifirlanirsa fis kapatilir.
    /// Nakitte alinan tutar mahsuptan fazlaysa fark para ustu olarak kaydedilir.
    /// </summary>
    public async Task<(OdemeDurumVm Durum, string? Hata)> OdemeEkleAsync(
        int vardiyaId, byte tip, decimal tutar, decimal? alinanTutar, string? onayKodu,
        CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new OdemeDurumVm(), "Ödenecek açık fiş yok.");

        if (fis.GenelToplam <= 0)
            return (await DurumAsync(fis, ct), "Sepet boş, ödeme alınamaz.");

        var odenen = await _odemeRepository.OdenenToplamAsync(fis.Id, ct);
        var kalan = OdemeHesaplayici.KalanHesapla(fis.GenelToplam, odenen);

        var (gecerli, hata) = OdemeHesaplayici.Dogrula(tip, tutar, alinanTutar, kalan);
        if (!gecerli) return (await DurumAsync(fis, ct), hata);

        var paraUstu = tip == OdemeHesaplayici.TipNakit
            ? OdemeHesaplayici.ParaUstuHesapla(tutar, alinanTutar!.Value)
            : (decimal?)null;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _odemeRepository.EkleAsync(conn, tx, new Odeme
        {
            FisId = fis.Id,
            Tip = tip,
            Tutar = tutar,
            AlinanTutar = tip == OdemeHesaplayici.TipNakit ? alinanTutar : null,
            ParaUstu = paraUstu,
            OnayKodu = string.IsNullOrWhiteSpace(onayKodu) ? null : onayKodu.Trim()
        }, ct);

        // Odeme eklendikten sonraki toplam ayni transaction icinden okunur;
        // ayri baglanti acilirsa henuz commit edilmemis satiri goremez.
        var yeniOdenen = await _odemeRepository.OdenenToplamAsync(conn, tx, fis.Id, ct);
        var yeniKalan = OdemeHesaplayici.KalanHesapla(fis.GenelToplam, yeniOdenen);

        // Borc kapandiysa satis ayni transaction icinde tamamlanir: stok
        // hareketleri ve fis durumu bu son odemeyle birlikte gecer. Stok
        // kontrolu takilirsa odeme de geri alinir.
        if (yeniKalan <= 0)
        {
            var satisSonuc = await _satisService.TamamlaAsync(conn, tx, fis, ct);
            if (!satisSonuc.Basarili)
            {
                tx.Rollback();
                return (await DurumAsync(fis, ct), satisSonuc.Hata);
            }

            tx.Commit();

            var kapananFis = await _fisRepository.GetirAsync(fis.Id, ct);
            var durum = await DurumAsync(kapananFis!, ct);
            durum.Uyarilar = satisSonuc.Uyarilar;
            return (durum, null);
        }

        tx.Commit();

        var guncelFis = await _fisRepository.GetirAsync(fis.Id, ct);
        return (await DurumAsync(guncelFis!, ct), null);
    }

    /// <summary>
    /// Tek bir odemeyi iptal eder. Fis kapanmissa yeniden Beklemede'ye alinir;
    /// kasiyer sepete donup duzeltme yapabilsin.
    /// </summary>
    public async Task<(OdemeDurumVm Durum, string? Hata)> OdemeIptalAsync(
        int fisId, int odemeId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.GetirAsync(fisId, ct);
        if (fis is null) return (new OdemeDurumVm(), "Fiş bulunamadı.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var silinen = await _odemeRepository.SilAsync(conn, tx, fisId, odemeId, ct);
        if (silinen == 0)
        {
            tx.Rollback();
            return (await DurumAsync(fis, ct), "Ödeme bulunamadı.");
        }

        // Fis kapanmissa satis geri aliniyor demektir: dusurulen stok da
        // geri verilir, yoksa iptal edilen satisin urunleri stokta eksik kalir.
        if (fis.Durum == DurumTamamlandi)
            await _stokRepository.SatisHareketleriniSilAsync(conn, tx, fisId, ct);

        await _fisRepository.DurumGuncelleAsync(conn, tx, fisId, DurumBeklemede, ct);
        tx.Commit();

        var guncelFis = await _fisRepository.GetirAsync(fisId, ct);
        return (await DurumAsync(guncelFis!, ct), null);
    }

    /// <summary>
    /// Odemeden vazgecip sepete doner: alinan tum odemeler silinir,
    /// fis Beklemede'ye alinir. Sepet satirlarina dokunulmaz.
    /// </summary>
    public async Task<OdemeDurumVm> OdemedenVazgecAsync(int fisId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.GetirAsync(fisId, ct);
        if (fis is null) return new OdemeDurumVm();

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _odemeRepository.TumunuSilAsync(conn, tx, fisId, ct);

        if (fis.Durum == DurumTamamlandi)
            await _stokRepository.SatisHareketleriniSilAsync(conn, tx, fisId, ct);

        await _fisRepository.DurumGuncelleAsync(conn, tx, fisId, DurumBeklemede, ct);
        tx.Commit();

        var guncelFis = await _fisRepository.GetirAsync(fisId, ct);
        return await DurumAsync(guncelFis!, ct);
    }
}
