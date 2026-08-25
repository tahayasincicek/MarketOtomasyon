using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using System.Globalization;

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
    private readonly KampanyaService _kampanyaService;
    private readonly IslemLogRepository _islemLogRepository;
    private readonly MudurOnayService _mudurOnayService;

    public SepetService(
        IDbConnectionFactory factory,
        FisRepository fisRepository,
        BarkodService barkodService,
        KampanyaService kampanyaService,
        IslemLogRepository islemLogRepository,
        MudurOnayService mudurOnayService)
    {
        _factory = factory;
        _fisRepository = fisRepository;
        _barkodService = barkodService;
        _kampanyaService = kampanyaService;
        _islemLogRepository = islemLogRepository;
        _mudurOnayService = mudurOnayService;
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

        var sepet = await GetirAsync(vardiyaId, ct);
        sepet.SonOkutulanUrunId = cozum.UrunId;
        return (sepet, null);
    }

    /// <summary>Miktar sifir veya negatif verilirse satir silinir.</summary>
    public async Task<(SepetVm Sepet, string? Hata)> MiktarGuncelleAsync(
        int vardiyaId, int satirId, decimal miktar, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Açık sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        var satir = satirlar.FirstOrDefault(s => s.SatirId == satirId);
        if (satir is null) return (await GetirAsync(vardiyaId, ct), "Satır bulunamadı.");

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
        if (fis is null) return (new SepetVm(), "Açık sepet yok.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var silinen = await _fisRepository.SatirSilAsync(conn, tx, fis.Id, satirId, ct);
        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), silinen > 0 ? null : "Satır bulunamadı.");
    }

    /// <summary>
    /// Satira yuzde bazli manuel indirim uygular. Yuzde 0 verilirse indirim kaldirilir.
    /// Onaylayan kullanici verilmezse islemi yapan kasiyerin yetkisine bakilir.
    /// </summary>
    public async Task<(SepetVm Sepet, string? Hata)> SatirIndirimiUygulaAsync(
        int vardiyaId,
        int satirId,
        decimal yuzde,
        byte rol,
        int kullaniciId,
        string? onayKullaniciAdi = null,
        string? onaySifre = null,
        string? onaySebebi = null,
        CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Açık sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        var satir = satirlar.FirstOrDefault(s => s.SatirId == satirId);
        if (satir is null) return (await GetirAsync(vardiyaId, ct), "Satır bulunamadı.");

        int? onaylayanId = null;
        if (yuzde != 0)
        {
            var (izin, onayVeren, hata) = await OnayCozumleAsync(
                rol, yuzde, IndirimYetkisi.KasiyerSatirLimitiYuzde, "Satır",
                onayKullaniciAdi, onaySifre, onaySebebi, kullaniciId, ct);

            if (!izin) return (await GetirAsync(vardiyaId, ct), hata);
            onaylayanId = onayVeren;
        }

        var brut = SepetHesaplayici.BrutHesapla(satir.Miktar, satir.BirimFiyat);
        var indirim = decimal.Round(brut * yuzde / 100m, 2, MidpointRounding.AwayFromZero);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _fisRepository.SatirIndirimGuncelleAsync(conn, tx, fis.Id, satirId, indirim,
            SepetHesaplayici.SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, indirim), ct);

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "ManuelIndirim",
            HedefTipi = "FisSatir",
            HedefId = satirId,
            EskiDeger = Para(satir.IndirimTutari),
            YeniDeger = Para(indirim),
            Aciklama = $"Fiş #{fis.Id}, satır indirimi %{yuzde:0.##}",
            OnaylayanKullaniciId = onaylayanId,
            OnaySebebi = onaylayanId is null ? null : onaySebebi?.Trim()
        }, ct);

        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    /// <summary>
    /// Fis geneline yuzde bazli indirim uygular. Indirim satirlara brut tutarlari
    /// oraninda dagitilir; boylece her KDV orani kendi payini dogru dusurur.
    /// </summary>
    public async Task<(SepetVm Sepet, string? Hata)> FisIndirimiUygulaAsync(
        int vardiyaId,
        decimal yuzde,
        byte rol,
        int kullaniciId,
        string? onayKullaniciAdi = null,
        string? onaySifre = null,
        string? onaySebebi = null,
        CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (new SepetVm(), "Açık sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        if (satirlar.Count == 0) return (await GetirAsync(vardiyaId, ct), "Sepet boş.");

        int? onaylayanId = null;
        if (yuzde != 0)
        {
            var (izin, onayVeren, hata) = await OnayCozumleAsync(
                rol, yuzde, IndirimYetkisi.KasiyerFisLimitiYuzde, "Fiş",
                onayKullaniciAdi, onaySifre, onaySebebi, kullaniciId, ct);

            if (!izin) return (await GetirAsync(vardiyaId, ct), hata);
            onaylayanId = onayVeren;
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

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "ManuelIndirim",
            HedefTipi = "Fis",
            HedefId = fis.Id,
            EskiDeger = Para(satirlar.Sum(s => s.IndirimTutari)),
            YeniDeger = Para(indirimTutari),
            Aciklama = $"Fiş geneli indirimi %{yuzde:0.##}",
            OnaylayanKullaniciId = onaylayanId,
            OnaySebebi = onaylayanId is null ? null : onaySebebi?.Trim()
        }, ct);

        await ToplamlariYazAsync(conn, tx, fis.Id, ct);
        tx.Commit();

        return (await GetirAsync(vardiyaId, ct), null);
    }

    /// <summary>Sepeti bosaltir: fis iptal edilir (Durum 9), satirlari silinir.</summary>
    public async Task<SepetVm> IptalEtAsync(
        int vardiyaId,
        int kullaniciId,
        CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return new SepetVm();

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _fisRepository.IptalEtAsync(conn, tx, fis.Id, ct);
        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "SatisIptali",
            HedefTipi = "Fis",
            HedefId = fis.Id,
            EskiDeger = Para(fis.GenelToplam),
            YeniDeger = Para(0),
            Aciklama = $"{fis.FisNo} numaralı bekleyen fiş iptal edildi."
        }, ct);
        tx.Commit();

        return new SepetVm();
    }

    private static string Para(decimal tutar)
        => tutar.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Once kampanyalari uygular, sonra fis basligindaki toplamlari yeniden yazar.
    ///
    /// Kampanyalar her sepet degisikliginde bastan hesaplanir: bir satirin
    /// eklenmesi baska bir satirin kampanyasini degistirebilir (orn. tutar
    /// baraji asilinca sepete indirim gelir, satir silinince kalkar).
    /// Toplamlar fiste de saklanir; raporlar her seferinde satirlari toplamasin.
    /// </summary>
    private async Task ToplamlariYazAsync(
        System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int fisId, CancellationToken ct)
    {
        await _kampanyaService.UygulaAsync(conn, tx, fisId, ct);

        var satirlar = await _fisRepository.SatirlariGetirAsync(conn, tx, fisId, ct);
        var sepet = SepetHesaplayici.Topla(satirlar);

        await _fisRepository.ToplamlariGuncelleAsync(conn, tx, fisId,
            sepet.AraToplam, sepet.ToplamIndirim, sepet.ToplamKdv, sepet.GenelToplam, ct);
    }

    /// <summary>
    /// Indirim yetkisini ve gerekiyorsa mudur onayini birlikte cozer.
    ///
    /// Donen izin true ise islem yapilabilir; onaylayan Id null ise
    /// onaya hic gerek olmamistir (kasiyer kendi limiti icinde kaldi
    /// veya islemi zaten mudur yapiyor).
    ///
    /// Basarisiz onay denemesi de loglanir: kasada mudur sifresi deneyen
    /// biri, en az basarili onay kadar onemli bir denetim olayidir.
    /// </summary>
    private async Task<(bool Izin, int? OnaylayanId, string? Hata)> OnayCozumleAsync(
        byte rol,
        decimal yuzde,
        decimal kasiyerLimiti,
        string kapsam,
        string? onayKullaniciAdi,
        string? onaySifre,
        string? onaySebebi,
        int kullaniciId,
        CancellationToken ct)
    {
        var durum = MudurOnayiKurallari.Degerlendir(rol, yuzde, kasiyerLimiti);

        switch (durum)
        {
            case OnayDurumu.Gecersiz:
                return (false, null, "İndirim oranı sıfırdan büyük olmalıdır.");

            // Mutlak limit onayla da asilamaz; bu dal bilerek onay
            // istemeden reddeder.
            case OnayDurumu.OnaylaDaAsilamaz:
                return (false, null,
                    $"İndirim %{IndirimYetkisi.MutlakLimitYuzde:0.##} oranını aşamaz. " +
                    "Müdür onayı da bu sınırı kaldırmaz.");

            case OnayDurumu.Gerekmez:
                return (true, null, null);
        }

        // Buradan sonrasi: onay gerekli.
        if (string.IsNullOrWhiteSpace(onayKullaniciAdi) && string.IsNullOrEmpty(onaySifre))
            return (false, null,
                $"{kapsam} indiriminde %{kasiyerLimiti:0.##} üstü müdür onayı gerektirir.");

        var (sebepGecerli, sebepHatasi) = MudurOnayiKurallari.SebepGecerliMi(onaySebebi);
        if (!sebepGecerli) return (false, null, sebepHatasi);

        var (onaylayanId, onayHatasi) = await _mudurOnayService.DogrulaAsync(
            onayKullaniciAdi, onaySifre, ct);

        if (onaylayanId is null)
        {
            await BasarisizOnayLoglaAsync(kullaniciId, yuzde, kapsam, onayKullaniciAdi, ct);
            return (false, null, onayHatasi);
        }

        return (true, onaylayanId, null);
    }

    /// <summary>
    /// Basarisiz onay denemesi kendi transaction'inda yazilir: asil islem
    /// zaten yapilmayacak, ama deneme kaydi kaybolmamali.
    ///
    /// Girilen sifre HICBIR sekilde loglanmaz; yalnizca denenen kullanici
    /// adi tutulur.
    /// </summary>
    private async Task BasarisizOnayLoglaAsync(
        int kullaniciId, decimal yuzde, string kapsam, string? denenenKullaniciAdi, CancellationToken ct)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "MudurOnayiBasarisiz",
            HedefTipi = "Indirim",
            Aciklama = $"{kapsam} indirimi %{yuzde:0.##} için başarısız onay denemesi " +
                       $"(denenen kullanıcı: {denenenKullaniciAdi})"
        }, ct);

        tx.Commit();
    }
}
