using MarketOtomasyon.Security;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class KullaniciKurallariTests
{
    private const int Mudur1 = 1;
    private const int Mudur2 = 2;
    private const int Kasiyer1 = 3;

    /* ---------- Pasiflestirme ---------- */

    [Fact]
    public void Pasif_KendiHesabiniPasifeAlamaz()
    {
        var (gecerli, hata) = KullaniciKurallari.PasifYapilabilirMi(
            hedefKullaniciId: Mudur1, hedefRol: Roller.MudurKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 3, acikVardiyasiVar: false);

        Assert.False(gecerli);
        Assert.Contains("Kendi hesabınızı", hata);
    }

    /// <summary>
    /// En agir senaryo: son mudur pasife alinirsa personel ekranina bir
    /// daha kimse giremez, geri donusun tek yolu elle SQL calistirmaktir.
    /// </summary>
    [Fact]
    public void Pasif_SonAktifMudurPasifeAlinamaz()
    {
        var (gecerli, hata) = KullaniciKurallari.PasifYapilabilirMi(
            hedefKullaniciId: Mudur1, hedefRol: Roller.MudurKodu,
            islemiYapanId: Mudur2, aktifMudurSayisi: 1, acikVardiyasiVar: false);

        Assert.False(gecerli);
        Assert.Contains("son aktif müdür", hata);
    }

    [Fact]
    public void Pasif_IkiMudurVarkenBiriPasifeAlinabilir()
    {
        var (gecerli, hata) = KullaniciKurallari.PasifYapilabilirMi(
            hedefKullaniciId: Mudur1, hedefRol: Roller.MudurKodu,
            islemiYapanId: Mudur2, aktifMudurSayisi: 2, acikVardiyasiVar: false);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    /// <summary>
    /// Pasif kullanici giris yapamaz; acik vardiyasi kapatilamaz kalir,
    /// Z raporu uretilemez ve kasa mutabakati asili kalir.
    /// </summary>
    [Fact]
    public void Pasif_AcikVardiyasiOlanPasifeAlinamaz()
    {
        var (gecerli, hata) = KullaniciKurallari.PasifYapilabilirMi(
            hedefKullaniciId: Kasiyer1, hedefRol: Roller.KasiyerKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 2, acikVardiyasiVar: true);

        Assert.False(gecerli);
        Assert.Contains("açık vardiyası", hata);
    }

    [Fact]
    public void Pasif_VardiyasiKapaliKasiyerPasifeAlinabilir()
    {
        var (gecerli, _) = KullaniciKurallari.PasifYapilabilirMi(
            hedefKullaniciId: Kasiyer1, hedefRol: Roller.KasiyerKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 1, acikVardiyasiVar: false);

        // Tek mudur olmasi kasiyeri etkilemez: kural yalnizca mudur rolune bakar.
        Assert.True(gecerli);
    }

    /* ---------- Rol degisikligi ---------- */

    [Fact]
    public void Rol_KendiRolunuDegistiremez()
    {
        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId: Mudur1, mevcutRol: Roller.MudurKodu, yeniRol: Roller.KasiyerKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 3);

        Assert.False(gecerli);
        Assert.Contains("Kendi rolünüzü", hata);
    }

    [Fact]
    public void Rol_SonMudurKasiyereDusurulemez()
    {
        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId: Mudur1, mevcutRol: Roller.MudurKodu, yeniRol: Roller.KasiyerKodu,
            islemiYapanId: Mudur2, aktifMudurSayisi: 1);

        Assert.False(gecerli);
        Assert.Contains("son aktif müdür", hata);
    }

    /// <summary>Mudur sayisini artiran yon her zaman guvenli.</summary>
    [Fact]
    public void Rol_TekMudurVarkenKasiyerMudurYapilabilir()
    {
        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId: Kasiyer1, mevcutRol: Roller.KasiyerKodu, yeniRol: Roller.MudurKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 1);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void Rol_AyniRoleDegistirmeReddedilir()
    {
        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId: Kasiyer1, mevcutRol: Roller.KasiyerKodu, yeniRol: Roller.KasiyerKodu,
            islemiYapanId: Mudur1, aktifMudurSayisi: 2);

        Assert.False(gecerli);
        Assert.Contains("zaten bu rolde", hata);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)3)]
    [InlineData((byte)255)]
    public void Rol_TanimsizRolKoduReddedilir(byte gecersizRol)
    {
        var (gecerli, hata) = KullaniciKurallari.RolDegistirilebilirMi(
            hedefKullaniciId: Kasiyer1, mevcutRol: Roller.KasiyerKodu, yeniRol: gecersizRol,
            islemiYapanId: Mudur1, aktifMudurSayisi: 2);

        Assert.False(gecerli);
        Assert.Contains("Geçersiz rol", hata);
    }

    /* ---------- Sifre ---------- */

    [Theory]
    [InlineData(null, "zorunludur")]
    [InlineData("", "zorunludur")]
    [InlineData("   ", "zorunludur")]
    [InlineData("ab1", "en az 8")]
    [InlineData("abcdefgh", "harf ve bir rakam")]
    [InlineData("12345678", "harf ve bir rakam")]
    public void Sifre_GecersizGirdilerReddedilir(string? sifre, string beklenenMesaj)
    {
        var (gecerli, hata) = KullaniciKurallari.SifreGecerliMi(sifre);

        Assert.False(gecerli);
        Assert.Contains(beklenenMesaj, hata);
    }

    [Theory]
    [InlineData("Kasiyer123!")]
    [InlineData("abcdefg1")]
    public void Sifre_GecerliGirdilerKabulEdilir(string sifre)
    {
        var (gecerli, hata) = KullaniciKurallari.SifreGecerliMi(sifre);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void Sifre_CokUzunSifreReddedilir()
    {
        var (gecerli, hata) = KullaniciKurallari.SifreGecerliMi(new string('a', 128) + "1");

        Assert.False(gecerli);
        Assert.Contains("en fazla 128", hata);
    }
}
