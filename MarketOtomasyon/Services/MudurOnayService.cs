using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Security;

namespace MarketOtomasyon.Services;

/// <summary>
/// Kasada verilen anlik mudur onayini dogrular.
///
/// Bilerek YAPMADIGI sey: oturum acmak. Ne cookie yenilenir ne
/// HttpContext.User degisir. Donen tek sey "bu tek islem icin onay
/// veren mudurun Id'si"dir; cagiran servis onu log kaydina yazar.
/// </summary>
public sealed class MudurOnayService
{
    private readonly KimlikDogrulamaService _kimlikDogrulama;
    private readonly ILogger<MudurOnayService> _kayit;

    public MudurOnayService(
        KimlikDogrulamaService kimlikDogrulama,
        ILogger<MudurOnayService> kayit)
    {
        _kimlikDogrulama = kimlikDogrulama;
        _kayit = kayit;
    }

    /// <summary>
    /// Basarili olursa onaylayan mudurun Id'sini doner.
    ///
    /// Hata mesajlari kasitli olarak ayni ve genel: "kullanici adi
    /// yanlis" ile "sifre yanlis" ayri ayri soylenirse kasadaki ekran
    /// gecerli mudur adlarini deneyerek bulmanin araci olur.
    /// </summary>
    public async Task<(int? OnaylayanId, string? Hata)> DogrulaAsync(
        string? kullaniciAdi,
        string? sifre,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            return (null, "Müdür kullanıcı adı ve şifresi gerekli.");

        // GirisDogrulaAsync yalnizca aktif kullaniciyi getirir
        // (SqlKullaniciAdiyla icinde Aktif = 1 var), yani pasife alinmis
        // bir mudur hesabi onay veremez.
        var mudur = await _kimlikDogrulama.GirisDogrulaAsync(kullaniciAdi, sifre, ct);
        if (mudur is null)
        {
            // Kasada mudur sifresi deneyen biri, bir saldirinin ilk
            // isaretidir. SIFRE ASLA LOGLANMAZ; yalnizca denenen ad.
            _kayit.LogWarning(
                "Mudur onayi basarisiz: kullanici bulunamadi veya sifre hatali {DenenenKullanici}",
                kullaniciAdi.Trim());

            return (null, "Müdür bilgileri doğrulanamadı.");
        }

        // Sifrenin dogru olmasi yetmez: hesap gercekten mudur olmali.
        if (mudur.Rol != Roller.MudurKodu)
        {
            // Sifre dogru ama hesap mudur degil: yetki asma denemesi.
            _kayit.LogWarning(
                "Mudur onayi reddedildi: hesap mudur rolunde degil {DenenenKullanici} {Rol}",
                mudur.KullaniciAdi, mudur.Rol);

            return (null, "Müdür bilgileri doğrulanamadı.");
        }

        _kayit.LogInformation("Mudur onayi verildi {OnaylayanId} {OnaylayanKullanici}",
            mudur.Id, mudur.KullaniciAdi);

        return (mudur.Id, null);
    }
}
