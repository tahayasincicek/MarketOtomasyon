using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Kullanicinin kendi hesabinda yaptigi degisikliklerin kurallari.
///
/// Mevcut sifrenin DOGRULUGU burada test edilmez; o hash
/// karsilastirmasi gerektirdigi icin ProfilService'in isi. Burasi
/// forma ait kontrolleri sabitler.
/// </summary>
public class ProfilKurallariTests
{
    private const string GecerliSifre = "YeniSifre123";

    /* ---------- Sifre degistirme ---------- */

    [Fact]
    public void Sifre_GecerliFormKabulEdilir()
    {
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            "EskiSifre123", GecerliSifre, GecerliSifre);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sifre_MevcutSifreZorunlu(string? mevcut)
    {
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            mevcut, GecerliSifre, GecerliSifre);

        Assert.False(gecerli);
        Assert.Contains("Mevcut şifre", hata);
    }

    [Fact]
    public void Sifre_TekrarTutmazsaReddedilir()
    {
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            "EskiSifre123", GecerliSifre, "BaskaSifre123");

        Assert.False(gecerli);
        Assert.Contains("tekrarı aynı değil", hata);
    }

    [Fact]
    public void Sifre_EskisiyleAyniOlamaz()
    {
        /* Sessiz basarisizlik olmamali: sifresinin ele gectigini
           dusunerek buraya gelen biri, ayni sifreyi yazip "degistirdim"
           saniyorsa hala risk altinda demektir. */
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            GecerliSifre, GecerliSifre, GecerliSifre);

        Assert.False(gecerli);
        Assert.Contains("eskisinden farklı", hata);
    }

    [Theory]
    [InlineData("kisa1")]           // 8 karakterden az
    [InlineData("sadeceharfler")]   // rakam yok
    [InlineData("12345678")]        // harf yok
    public void Sifre_YeniSifreKurallaraUymaliysa(string yeni)
    {
        var (gecerli, _) = ProfilKurallari.SifreDegisikligiGecerliMi(
            "EskiSifre123", yeni, yeni);

        Assert.False(gecerli);
    }

    [Fact]
    public void Sifre_KuralHatasiTekrarKontrolundenONCE()
    {
        // Zayif ve tekrari tutmayan bir sifrede once kural hatasi
        // bildirilmeli; kullanici once tekrari duzeltip sonra
        // "zaten gecersizmis" demesin.
        var (gecerli, hata) = ProfilKurallari.SifreDegisikligiGecerliMi(
            "EskiSifre123", "kisa", "baska");

        Assert.False(gecerli);
        Assert.DoesNotContain("tekrarı", hata);
    }

    /* ---------- Ad soyad ---------- */

    [Fact]
    public void AdSoyad_GecerliDegerKabulEdilir()
        => Assert.True(ProfilKurallari.AdSoyadGecerliMi("Taha Yasin Çiçek").Gecerli);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AdSoyad_BosOlamaz(string? deger)
        => Assert.False(ProfilKurallari.AdSoyadGecerliMi(deger).Gecerli);

    [Fact]
    public void AdSoyad_CokKisaReddedilir()
        => Assert.False(ProfilKurallari.AdSoyadGecerliMi("Ab").Gecerli);

    [Fact]
    public void AdSoyad_BosluklarKirpilarakOlculur()
    {
        // "  Ab  " kirpildiginda 2 karakter: uzunluk kirpilmis deger
        // uzerinden olculmeli, yoksa bosluklarla sinir asilabilir.
        Assert.False(ProfilKurallari.AdSoyadGecerliMi("  Ab  ").Gecerli);
        Assert.True(ProfilKurallari.AdSoyadGecerliMi("  Ali  ").Gecerli);
    }

    [Fact]
    public void AdSoyad_UstSinirAsilamaz()
    {
        var uzun = new string('a', ProfilKurallari.AdSoyadEnFazla + 1);

        Assert.False(ProfilKurallari.AdSoyadGecerliMi(uzun).Gecerli);
        Assert.True(ProfilKurallari.AdSoyadGecerliMi(
            new string('a', ProfilKurallari.AdSoyadEnFazla)).Gecerli);
    }
}
