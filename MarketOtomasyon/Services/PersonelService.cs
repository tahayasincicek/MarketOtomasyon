using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Identity;

namespace MarketOtomasyon.Services;

/// <summary>
/// Personel yonetimi. Kurallar KullaniciKurallari'nda (saf, test edilir);
/// burada yalnizca sira, transaction ve denetim kaydi var.
///
/// Her yazma islemi IslemLog'a da yazilir ve ikisi ayni transaction'dadir:
/// rol degisikligi ile pasiflestirme hassas islemlerdir, "kim ne zaman
/// yapti" sorusunun cevabi gercek durumla ayni anda olusmali.
/// </summary>
public sealed class PersonelService
{
    private readonly IDbConnectionFactory _factory;
    private readonly KullaniciRepository _kullaniciRepository;
    private readonly VardiyaRepository _vardiyaRepository;
    private readonly IslemLogRepository _islemLogRepository;
    private readonly IPasswordHasher<Kullanici> _sifreHesaplayici;

    public PersonelService(
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

    public async Task<PersonelEkranVm> EkranAsync(int oturumdakiKullaniciId, CancellationToken ct = default)
        => new()
        {
            Satirlar = await _kullaniciRepository.PersonelListesiAsync(ct),
            AktifMudurSayisi = await _kullaniciRepository.AktifMudurSayisiAsync(ct),
            OturumdakiKullaniciId = oturumdakiKullaniciId
        };

    public async Task<string?> OlusturAsync(
        PersonelFormVm form, int islemiYapanId, CancellationToken ct = default)
    {
        var kullaniciAdi = form.KullaniciAdi?.Trim() ?? "";
        var adSoyad = form.AdSoyad?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(kullaniciAdi))
            return "Kullanıcı adı zorunludur.";

        if (string.IsNullOrWhiteSpace(adSoyad))
            return "Ad soyad zorunludur.";

        if (form.Rol is not (Roller.KasiyerKodu or Roller.MudurKodu))
            return "Geçersiz rol.";

        var (sifreGecerli, sifreHatasi) = KullaniciKurallari.SifreGecerliMi(form.Sifre);
        if (!sifreGecerli) return sifreHatasi;

        if (await _kullaniciRepository.KullaniciAdiVarMiAsync(kullaniciAdi, ct))
            return $"'{kullaniciAdi}' kullanıcı adı zaten kayıtlı.";

        // Hash, kaydin kendisiyle uretilir; PasswordHasher'in imzasi boyle.
        var yeni = new Kullanici
        {
            KullaniciAdi = kullaniciAdi,
            AdSoyad = adSoyad,
            Rol = form.Rol,
            Aktif = true
        };
        yeni.SifreHash = _sifreHesaplayici.HashPassword(yeni, form.Sifre);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var yeniId = await _kullaniciRepository.EkleAsync(conn, tx, yeni, ct);

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = islemiYapanId,
            IslemTipi = "PersonelOlustur",
            HedefTipi = "Kullanici",
            HedefId = yeniId,
            YeniDeger = $"{kullaniciAdi} / {Roller.Ad(form.Rol)}",
            Aciklama = $"{adSoyad} kullanıcısı oluşturuldu"
        }, ct);

        tx.Commit();
        return null;
    }

    public async Task<string?> AktiflikDegistirAsync(
        int hedefKullaniciId, bool aktif, int islemiYapanId, CancellationToken ct = default)
    {
        var hedef = await _kullaniciRepository.GetirAsync(hedefKullaniciId, ct);
        if (hedef is null) return "Kullanıcı bulunamadı.";

        if (hedef.Aktif == aktif)
            return aktif ? "Kullanıcı zaten aktif." : "Kullanıcı zaten pasif.";

        // Kural yalnizca pasife alirken calisir; aktiflestirme her zaman
        // guvenlidir, sistemi yonetilemez hale getiremez.
        if (!aktif)
        {
            var acikVardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(hedefKullaniciId, ct);
            var (gecerli, hata) = KullaniciKurallari.PasifYapilabilirMi(
                hedefKullaniciId,
                hedef.Rol,
                islemiYapanId,
                await _kullaniciRepository.AktifMudurSayisiAsync(ct),
                acikVardiya is not null);

            if (!gecerli) return hata;
        }

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var etkilenen = await _kullaniciRepository.AktiflikGuncelleAsync(
            conn, tx, hedefKullaniciId, aktif, ct);

        if (etkilenen != 1)
        {
            tx.Rollback();
            return "Kullanıcı başka bir işlemde değişmiş. Listeyi yenileyin.";
        }

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = islemiYapanId,
            IslemTipi = aktif ? "PersonelAktiflestir" : "PersonelPasiflestir",
            HedefTipi = "Kullanici",
            HedefId = hedefKullaniciId,
            EskiDeger = hedef.Aktif ? "aktif" : "pasif",
            YeniDeger = aktif ? "aktif" : "pasif",
            Aciklama = $"{hedef.AdSoyad} ({hedef.KullaniciAdi})"
        }, ct);

        tx.Commit();
        return null;
    }

    public async Task<string?> RolDegistirAsync(
        int hedefKullaniciId, byte yeniRol, int islemiYapanId, CancellationToken ct = default)
    {
        var hedef = await _kullaniciRepository.GetirAsync(hedefKullaniciId, ct);
        if (hedef is null) return "Kullanıcı bulunamadı.";

        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId,
            hedef.Rol,
            yeniRol,
            islemiYapanId,
            await _kullaniciRepository.AktifMudurSayisiAsync(ct));

        if (!gecerli) return hata;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var etkilenen = await _kullaniciRepository.RolGuncelleAsync(conn, tx, hedefKullaniciId, yeniRol, ct);
        if (etkilenen != 1)
        {
            tx.Rollback();
            return "Kullanıcı başka bir işlemde değişmiş. Listeyi yenileyin.";
        }

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = islemiYapanId,
            IslemTipi = "PersonelRolDegistir",
            HedefTipi = "Kullanici",
            HedefId = hedefKullaniciId,
            EskiDeger = Roller.Ad(hedef.Rol),
            YeniDeger = Roller.Ad(yeniRol),
            Aciklama = $"{hedef.AdSoyad} ({hedef.KullaniciAdi})"
        }, ct);

        tx.Commit();
        return null;
    }

    /// <summary>
    /// Sifre hicbir zaman saklanmaz, ekranda gosterilmez ve loga yazilmaz;
    /// IslemLog'a yalnizca sifirlandigi bilgisi duser.
    /// </summary>
    public async Task<string?> SifreSifirlaAsync(
        int hedefKullaniciId, string? yeniSifre, int islemiYapanId, CancellationToken ct = default)
    {
        var (sifreGecerli, sifreHatasi) = KullaniciKurallari.SifreGecerliMi(yeniSifre);
        if (!sifreGecerli) return sifreHatasi;

        var hedef = await _kullaniciRepository.GetirAsync(hedefKullaniciId, ct);
        if (hedef is null) return "Kullanıcı bulunamadı.";

        var hash = _sifreHesaplayici.HashPassword(hedef, yeniSifre!);

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var etkilenen = await _kullaniciRepository.SifreSifirlaAsync(conn, tx, hedefKullaniciId, hash, ct);
        if (etkilenen != 1)
        {
            tx.Rollback();
            return "Kullanıcı bulunamadı.";
        }

        await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
        {
            KullaniciId = islemiYapanId,
            IslemTipi = "PersonelSifreSifirla",
            HedefTipi = "Kullanici",
            HedefId = hedefKullaniciId,
            Aciklama = $"{hedef.AdSoyad} ({hedef.KullaniciAdi}) için şifre sıfırlandı"
        }, ct);

        tx.Commit();
        return null;
    }
}
