namespace MarketOtomasyon.Services;

/// <summary>
/// Kullanicinin kendi hesabinda yaptigi degisikliklerin kurallari.
/// Veritabani bilmez; dogrudan test edilir.
///
/// Mudurun baskasinin sifresini sifirlamasindan (PersonelService) ayri
/// duruyor, cunku kurallari farkli: orada mevcut sifre sorulmaz ve
/// "eskisiyle ayni olmasin" kosulu yoktur.
/// </summary>
public static class ProfilKurallari
{
    public const int AdSoyadEnAz = 3;
    public const int AdSoyadEnFazla = 100;

    /// <summary>
    /// Sifre degistirme formunun veritabanindan bagimsiz kontrolleri.
    /// Mevcut sifrenin DOGRULUGU burada bakilmaz - onun icin hash
    /// karsilastirmasi gerekir, o is servise ait.
    /// </summary>
    public static (bool Gecerli, string? Hata) SifreDegisikligiGecerliMi(
        string? mevcutSifre, string? yeniSifre, string? yeniSifreTekrar)
    {
        if (string.IsNullOrWhiteSpace(mevcutSifre))
            return (false, "Mevcut şifrenizi girin.");

        var (yeniGecerli, yeniHata) = KullaniciKurallari.SifreGecerliMi(yeniSifre);
        if (!yeniGecerli) return (false, yeniHata);

        if (yeniSifre != yeniSifreTekrar)
            return (false, "Yeni şifre ile tekrarı aynı değil.");

        /* Ayni sifreyi yeniden yazmak degisiklik degildir. Kullanici
           sifresini degistirdigini sanip eski sifreyle devam etmesin;
           ozellikle sifresinin ele gectigini dusunerek buraya gelmisse
           bu sessiz basarisizlik tehlikeli olur. */
        if (mevcutSifre == yeniSifre)
            return (false, "Yeni şifre eskisinden farklı olmalıdır.");

        return (true, null);
    }

    public static (bool Gecerli, string? Hata) AdSoyadGecerliMi(string? adSoyad)
    {
        var temiz = adSoyad?.Trim();

        if (string.IsNullOrWhiteSpace(temiz))
            return (false, "Ad soyad zorunludur.");

        if (temiz.Length < AdSoyadEnAz)
            return (false, $"Ad soyad en az {AdSoyadEnAz} karakter olmalıdır.");

        if (temiz.Length > AdSoyadEnFazla)
            return (false, $"Ad soyad en fazla {AdSoyadEnFazla} karakter olabilir.");

        return (true, null);
    }
}
