using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Kasadaki acik sepeti yonetir. Sepet, veritabaninda Durum 1 (Beklemede)
/// olan bir fistir: kasa cokse bile sepet kaybolmaz, baska kasadan devralinabilir.
/// Beklemedeki fis stogu etkilemez; stok ancak odeme alininca duser.
/// </summary>
public class SepetService
{
    private readonly IDbConnectionFactory _factory;
    private readonly FisRepository _fisRepository;
    private readonly BarkodService _barkodService;

    public SepetService(IDbConnectionFactory factory, FisRepository fisRepository, BarkodService barkodService)
    {
        _factory = factory;
        _fisRepository = fisRepository;
        _barkodService = barkodService;
    }

    /// <summary>Vardiyadaki acik sepeti getirir; yoksa bos sepet doner.</summary>
    public async Task<SepetVm> GetirAsync(int vardiyaId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return new SepetVm();

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);

        var sepet = SepetHesaplayici.Topla(satirlar);
        sepet.FisId = fis.Id;
        sepet.FisNo = fis.FisNo;
        return sepet;
    }

    /// <summary>
    /// Barkod okutur ve sepete ekler. Acik fis yoksa acar.
    /// Ayni urun tekrar okutulursa yeni satir acmak yerine miktar artirilir.
    /// </summary>
    public async Task<(SepetVm Sepet, string? Hata)> BarkodEkleAsync(
        int vardiyaId, int kullaniciId, string? barkod, CancellationToken ct = default)
    {
        var cozum = await _barkodService.CozAsync(barkod, ct);
        if (!cozum.Basarili)
            return (await GetirAsync(vardiyaId, ct), cozum.Hata);

        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var fisId = fis?.Id ?? (await _fisRepository.FisAcAsync(conn, tx, vardiyaId, kullaniciId, ct)).FisId;

        // Tartili urunlerde her okutma ayri bir tartimdir; miktarlari birlestirmek yaniltici olur.
        var mevcutSatirId = cozum.Birim == "KG"
            ? null
            : await _fisRepository.AyniUrunSatiriBulAsync(conn, tx, fisId, cozum.UrunId, ct);

        if (mevcutSatirId is not null)
        {
            var satirlar = await _fisRepository.SatirlariGetirAsync(conn, tx, fisId, ct);
            var mevcut = satirlar.First(s => s.SatirId == mevcutSatirId.Value);
            var yeniMiktar = mevcut.Miktar + cozum.Miktar;

            await _fisRepository.SatirMiktarGuncelleAsync(conn, tx, fisId, mevcutSatirId.Value, yeniMiktar,
                SepetHesaplayici.SatirToplamHesapla(yeniMiktar, mevcut.BirimFiyat, mevcut.IndirimTutari), ct);
        }
        else
        {
            await _fisRepository.SatirEkleAsync(conn, tx, new FisSatir
            {
                FisId = fisId,
                UrunId = cozum.UrunId,
                Miktar = cozum.Miktar,
                BirimFiyat = cozum.BirimFiyat,
                KdvOrani = cozum.KdvOrani,
                SatirToplam = SepetHesaplayici.SatirToplamHesapla(cozum.Miktar, cozum.BirimFiyat)
            }, ct);
        }

        await ToplamlariYazAsync(conn, tx, fisId, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    /// <summary>Miktar sifir veya negatif verilirse satir silinir.</summary>
    public async Task<(SepetVm Sepet, string? Hata)> MiktarGuncelleAsync(
        int vardiyaId, int satirId, decimal miktar, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Acik sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        var satir = satirlar.FirstOrDefault(s => s.SatirId == satirId);
        if (satir is null) return (await GetirAsync(vardiyaId, ct), "Satir bulunamadi.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        if (miktar <= 0)
        {
            await _fisRepository.SatirSilAsync(conn, tx, fis.Id, satirId, ct);
        }
        else
        {
            await _fisRepository.SatirMiktarGuncelleAsync(conn, tx, fis.Id, satirId, miktar,
                SepetHesaplayici.SatirToplamHesapla(miktar, satir.BirimFiyat, satir.IndirimTutari), ct);
        }

        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    public async Task<(SepetVm Sepet, string? Hata)> SatirSilAsync(
        int vardiyaId, int satirId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Acik sepet yok.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var silinen = await _fisRepository.SatirSilAsync(conn, tx, fis.Id, satirId, ct);
        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), silinen > 0 ? null : "Satir bulunamadi.");
    }

    /// <summary>
    /// Satira yuzde bazli manuel indirim uygular. Yuzde 0 verilirse indirim kaldirilir.
    /// Onaylayan kullanici verilmezse islemi yapan kasiyerin yetkisine bakilir.
    /// </summary>
    public async Task<(SepetVm Sepet, string? Hata)> SatirIndirimiUygulaAsync(
        int vardiyaId, int satirId, decimal yuzde, byte rol, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Acik sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        var satir = satirlar.FirstOrDefault(s => s.SatirId == satirId);
        if (satir is null) return (await GetirAsync(vardiyaId, ct), "Satir bulunamadi.");

        if (yuzde != 0)
        {
            var (yeterli, hata) = IndirimYetkisi.SatirIndirimiKontrol(rol, yuzde);
            if (!yeterli) return (await GetirAsync(vardiyaId, ct), hata);
        }

        var brut = SepetHesaplayici.BrutHesapla(satir.Miktar, satir.BirimFiyat);
        var indirim = decimal.Round(brut * yuzde / 100m, 2, MidpointRounding.AwayFromZero);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _fisRepository.SatirIndirimGuncelleAsync(conn, tx, fis.Id, satirId, indirim,
            SepetHesaplayici.SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, indirim), ct);

        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    /// <summary>
    /// Fis geneline yuzde bazli indirim uygular. Indirim satirlara brut tutarlari
    /// oraninda dagitilir; boylece her KDV orani kendi payini dogru dusurur.
    /// </summary>
    public async Task<(SepetVm Sepet, string? Hata)> FisIndirimiUygulaAsync(
        int vardiyaId, decimal yuzde, byte rol, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Acik sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        if (satirlar.Count == 0) return (await GetirAsync(vardiyaId, ct), "Sepet bos.");

        if (yuzde != 0)
        {
            var (yeterli, hata) = IndirimYetkisi.FisIndirimiKontrol(rol, yuzde);
            if (!yeterli) return (await GetirAsync(vardiyaId, ct), hata);
        }

        var toplamBrut = satirlar.Sum(s => SepetHesaplayici.BrutHesapla(s.Miktar, s.BirimFiyat));
        var indirimTutari = decimal.Round(toplamBrut * yuzde / 100m, 2, MidpointRounding.AwayFromZero);
        var dagitim = SepetHesaplayici.FisIndiriminiDagit(satirlar, indirimTutari);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        foreach (var satir in satirlar)
        {
            var pay = dagitim[satir.SatirId];
            await _fisRepository.SatirIndirimGuncelleAsync(conn, tx, fis.Id, satir.SatirId, pay,
                SepetHesaplayici.SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, pay), ct);
        }

        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    /// <summary>Sepeti bosaltir: fis iptal edilir (Durum 9), satirlari silinir.</summary>
    public async Task<SepetVm> IptalEtAsync(int vardiyaId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return new SepetVm();

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _fisRepository.IptalEtAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return new SepetVm();
    }

    /// <summary>
    /// Fis basligindaki toplamlari satirlardan yeniden hesaplayip yazar.
    /// Toplamlar fiste de saklanir; raporlar her seferinde satirlari toplamasin.
    /// </summary>
    private async Task ToplamlariYazAsync(
        System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int fisId, CancellationToken ct)
    {
        var satirlar = await _fisRepository.SatirlariGetirAsync(conn, tx, fisId, ct);
        var sepet = SepetHesaplayici.Topla(satirlar);

        await _fisRepository.ToplamlariGuncelleAsync(conn, tx, fisId,
            sepet.AraToplam, sepet.ToplamIndirim, sepet.ToplamKdv, sepet.GenelToplam, ct);
    }
}
