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

    public const int KullaniciAdiEnAz = 3;
    public const int KullaniciAdiEnFazla = 50;

    /// <summary>
    /// Kullanici adi kurallari. Benzersizlik BURADA bakilmaz; o
    /// veritabani sorgusu gerektirir ve servise aittir.
    ///
    /// Bosluk ve buyuk harf kabul edilmiyor: giris ekraninda yazilan ad
    /// birebir eslesmek zorunda. "Ali Veli" ile "ali veli" arasindaki
    /// farki kullanicinin hatirlamasini beklemek, gun basinda giris
    /// yapamayan bir kasiyer demek.
    /// </summary>
    public static (bool Gecerli, string? Hata) KullaniciAdiGecerliMi(string? kullaniciAdi)
    {
        var temiz = kullaniciAdi?.Trim();

        if (string.IsNullOrWhiteSpace(temiz))
            return (false, "Kullanıcı adı zorunludur.");

        if (temiz.Length < KullaniciAdiEnAz)
            return (false, $"Kullanıcı adı en az {KullaniciAdiEnAz} karakter olmalıdır.");

        if (temiz.Length > KullaniciAdiEnFazla)
            return (false, $"Kullanıcı adı en fazla {KullaniciAdiEnFazla} karakter olabilir.");

        if (temiz.Any(char.IsWhiteSpace))
            return (false, "Kullanıcı adı boşluk içeremez.");

        /* Yalnizca ASCII harf, rakam, nokta, tire ve alt cizgi.
           Turkce karakterler disarida: klavye duzeni farkli bir makinede
           "cicek" yazip giremeyen bir kullanici, sorunun kaynagini
           bulamaz. Ad soyad alani zaten Turkce karakter kabul ediyor. */
        if (!temiz.All(k => char.IsAsciiLetterOrDigit(k) || k is '.' or '-' or '_'))
            return (false, "Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.");

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
