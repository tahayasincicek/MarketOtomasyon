using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class MudurOnayiKurallariTests
{
    private const byte Kasiyer = IndirimYetkisi.RolKasiyer;
    private const byte Mudur = IndirimYetkisi.RolMudur;
    private const decimal SatirLimiti = IndirimYetkisi.KasiyerSatirLimitiYuzde; // %10

    [Fact]
    public void Kasiyer_LimitIcinde_OnayGerekmez()
    {
        Assert.Equal(OnayDurumu.Gerekmez,
            MudurOnayiKurallari.Degerlendir(Kasiyer, 8m, SatirLimiti));
    }

    [Fact]
    public void Kasiyer_LimitinTamUstunde_OnayGerekmez()
    {
        // Sinir dahil: %10 kasiyerin kendi yetkisi, %10,01 onay ister.
        Assert.Equal(OnayDurumu.Gerekmez,
            MudurOnayiKurallari.Degerlendir(Kasiyer, SatirLimiti, SatirLimiti));
    }

    [Fact]
    public void Kasiyer_LimitAsildi_OnayGerekli()
    {
        Assert.Equal(OnayDurumu.Gerekli,
            MudurOnayiKurallari.Degerlendir(Kasiyer, 25m, SatirLimiti));
    }

    [Fact]
    public void Mudur_KendiYetkisiyle_OnayGerekmez()
    {
        Assert.Equal(OnayDurumu.Gerekmez,
            MudurOnayiKurallari.Degerlendir(Mudur, 40m, SatirLimiti));
    }

    /// <summary>
    /// En kritik kural: mutlak limit onayla da asilamaz. Aksi halde
    /// "mudur onayi" %100 indirimin kapisi olurdu.
    /// </summary>
    [Theory]
    [InlineData(50.01)]
    [InlineData(75)]
    [InlineData(100)]
    public void MutlakLimitUstu_KasiyerdeDeMudurdeDe_OnaylaDaAsilamaz(decimal yuzde)
    {
        Assert.Equal(OnayDurumu.OnaylaDaAsilamaz,
            MudurOnayiKurallari.Degerlendir(Kasiyer, yuzde, SatirLimiti));

        Assert.Equal(OnayDurumu.OnaylaDaAsilamaz,
            MudurOnayiKurallari.Degerlendir(Mudur, yuzde, SatirLimiti));
    }

    [Fact]
    public void MutlakLimitinTamUstunde_Mudur_Yapabilir()
    {
        Assert.Equal(OnayDurumu.Gerekmez,
            MudurOnayiKurallari.Degerlendir(Mudur, IndirimYetkisi.MutlakLimitYuzde, SatirLimiti));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SifirVeyaNegatif_Gecersiz(decimal yuzde)
    {
        Assert.Equal(OnayDurumu.Gecersiz,
            MudurOnayiKurallari.Degerlendir(Kasiyer, yuzde, SatirLimiti));
    }

    /* ---------- Onay sebebi ---------- */

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sebep_Bos_Reddedilir(string? sebep)
    {
        var (gecerli, hata) = MudurOnayiKurallari.SebepGecerliMi(sebep);
        Assert.False(gecerli);
        Assert.Contains("zorunludur", hata);
    }

    [Fact]
    public void Sebep_CokKisa_Reddedilir()
    {
        var (gecerli, hata) = MudurOnayiKurallari.SebepGecerliMi("ok");
        Assert.False(gecerli);
        Assert.Contains("en az", hata);
    }

    [Fact]
    public void Sebep_CokUzun_Reddedilir()
    {
        var (gecerli, hata) = MudurOnayiKurallari.SebepGecerliMi(
            new string('a', MudurOnayiKurallari.SebepEnFazlaUzunluk + 1));

        Assert.False(gecerli);
        Assert.Contains("en fazla", hata);
    }

    [Fact]
    public void Sebep_Gecerli_KabulEdilir()
    {
        var (gecerli, hata) = MudurOnayiKurallari.SebepGecerliMi("Ambalaj hasarlı");
        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void Sebep_BasSonBosluk_KirpilarakDegerlendirilir()
    {
        // "ok" bosluklarla 5 karakteri gecse de kirpildiktan sonra kisa.
        var (gecerli, _) = MudurOnayiKurallari.SebepGecerliMi("   ok   ");
        Assert.False(gecerli);
    }
}
