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

    public MudurOnayService(KimlikDogrulamaService kimlikDogrulama)
        => _kimlikDogrulama = kimlikDogrulama;

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
            return (null, "Müdür bilgileri doğrulanamadı.");

        // Sifrenin dogru olmasi yetmez: hesap gercekten mudur olmali.
        if (mudur.Rol != Roller.MudurKodu)
            return (null, "Müdür bilgileri doğrulanamadı.");

        return (mudur.Id, null);
    }
}
