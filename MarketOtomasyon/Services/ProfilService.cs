using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Identity;

namespace MarketOtomasyon.Services;

/// <summary>
/// Kullanicinin kendi hesabi uzerindeki islemleri.
///
/// PersonelService'ten ayri duruyor: orasi MUDURUN baskasi uzerinde
/// yaptigi islemleri yonetir ve [Authorize(Roles = Mudur)] ile korunur.
/// Burasi her rolun kendi hesabi icin kullandigi yol; hedef kullanici
/// her zaman oturumdaki kisidir, disaridan Id alinmaz.
/// </summary>
public sealed class ProfilService
{
    private readonly IDbConnectionFactory _factory;
    private readonly KullaniciRepository _kullaniciRepository;
    private readonly VardiyaRepository _vardiyaRepository;
    private readonly IslemLogRepository _islemLogRepository;
    private readonly IPasswordHasher<Kullanici> _sifreHesaplayici;

    public ProfilService(
        IDbConnectionFactory factory,
        KullaniciRepository kullaniciRepository,
        VardiyaRepository vardiyaRepository,
        IslemLogRepository islemLogRepository,
        IPasswordHasher<Kullanici> sifreHesaplayici)
    {
        _factory = factory;
        _kullaniciRepository = kullaniciRepository;
        _vardiyaRepository = vardiyaRepository;
        _islemLogRepository = islemLogRepository;
        _sifreHesaplayici = sifreHesaplayici;
    }

    public async Task<ProfilVm?> EkranAsync(int kullaniciId, CancellationToken ct = default)
    {
        var kullanici = await _kullaniciRepository.GetirAsync(kullaniciId, ct);
        if (kullanici is null) return null;

        var acikVardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);

        return new ProfilVm
        {
            KullaniciId = kullanici.Id,
            KullaniciAdi = kullanici.KullaniciAdi,
            AdSoyad = kullanici.AdSoyad,
            YeniAdSoyad = kullanici.AdSoyad,
            RolAdi = kullanici.Rol == Roller.MudurKodu ? "Müdür" : "Kasiyer",
            AcikVardiyaAcilisUtc = acikVardiya?.AcilisTarihi
        };
    }

    /// <summary>
    /// Kullanicinin kendi sifresini degistirmesi. Hata varsa mesaji,
    /// basariliysa null doner.
    ///
    /// Mevcut sifre SORULUYOR. Mudurun sifirlama akisinda sorulmaz cunku
    /// orada yetkiyi rol saglar; burada ise tek dogrulama oturumun
    /// kendisidir. Mevcut sifre istenmezse acik birakilmis bir kasa
    /// ekranindan gecen biri sifreyi degistirip hesabi kalici olarak
    /// ele gecirebilir.
    /// </summary>
    public async Task<string?> SifreDegistirAsync(
        int kullaniciId, SifreDegistirVm form, CancellationToken ct = default)
    {
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            form.MevcutSifre, form.YeniSifre, form.YeniSifreTekrar);
        if (!gecerli) return hata;

        var kullanici = await _kullaniciRepository.GetirAsync(kullaniciId, ct);
        if (kullanici is null) return "Hesap bulunamadı.";
        if (!kullanici.Aktif) return "Hesabınız pasif durumda.";

        var dogrulama = _sifreHesaplayici.VerifyHashedPassword(
            kullanici, kullanici.SifreHash, form.MevcutSifre!);

        /* Yanlis mevcut sifre, gecersiz form alanlariyla ayni genel
           mesaji almaz; kullanici neyi duzeltecegini bilmeli. Bu bir
           bilgi sizintisi degil: kisi zaten oturum acmis durumda,
           hesabin varligi sir degil. */
        if (dogrulama == PasswordVerificationResult.Failed)
            return "Mevcut şifreniz hatalı.";

        var yeniHash = _sifreHesaplayici.HashPassword(kullanici, form.YeniSifre!);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var etkilenen = await _kullaniciRepository.KendiSifresiniGuncelleAsync(
            conn, tx, kullaniciId, yeniHash, ct);

        if (etkilenen != 1)
        {
            tx.Rollback();
            return "Şifre güncellenemedi.";
        }

        // Sifrenin kendisi loga YAZILMAZ; yalnizca degistigi bilgisi.
        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "ProfilSifreDegistir",
            HedefTipi = "Kullanici",
            HedefId = kullaniciId,
            Aciklama = "Kullanıcı kendi şifresini değiştirdi"
        }, ct);

        tx.Commit();
        return null;
    }

    /// <summary>
    /// Ad soyad guncelleme. Kullanici adi ve rol BILEREK degistirilemez:
    /// kullanici adi gecmis loglarin okunabilirligini bozar, rol ise
    /// kullanicinin kendini mudur yapmasi anlamina gelirdi.
    /// </summary>
    public async Task<string?> AdSoyadGuncelleAsync(
        int kullaniciId, string? yeniAdSoyad, CancellationToken ct = default)
    {
        var (gecerli, hata) = ProfilKurallari.AdSoyadGecerliMi(yeniAdSoyad);
        if (!gecerli) return hata;

        var temiz = yeniAdSoyad!.Trim();

        var kullanici = await _kullaniciRepository.GetirAsync(kullaniciId, ct);
        if (kullanici is null) return "Hesap bulunamadı.";
        if (kullanici.AdSoyad == temiz) return null;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var etkilenen = await _kullaniciRepository.AdSoyadGuncelleAsync(
            conn, tx, kullaniciId, temiz, ct);

        if (etkilenen != 1)
        {
            tx.Rollback();
            return "Ad soyad güncellenemedi.";
        }

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = kullaniciId,
            IslemTipi = "ProfilAdSoyadDegistir",
            HedefTipi = "Kullanici",
            HedefId = kullaniciId,
            Aciklama = $"Ad soyad '{kullanici.AdSoyad}' -> '{temiz}' olarak değiştirildi"
        }, ct);

        tx.Commit();
        return null;
    }
}
