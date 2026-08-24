using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class IndirimYetkisiTests
{
    private const byte Kasiyer = IndirimYetkisi.RolKasiyer;
    private const byte Mudur = IndirimYetkisi.RolMudur;

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void Kasiyer_LimitiIcindekiSatirIndiriminiVerebilir(decimal yuzde)
    {
        var (yeterli, hata) = IndirimYetkisi.SatirIndirimiKontrol(Kasiyer, yuzde);

        Assert.True(yeterli);
        Assert.Null(hata);
    }

    [Fact]
    public void Kasiyer_LimitUstuSatirIndiriminiVeremez()
    {
        var (yeterli, hata) = IndirimYetkisi.SatirIndirimiKontrol(Kasiyer, 15m);

        Assert.False(yeterli);
        Assert.Contains("müdür onayı", hata!);
    }

    [Fact]
    public void Kasiyer_FisIndiriminde_DahaDarBirLimiteTabidir()
    {
        // Satirda serbest olan %10, fis genelinde onay ister.
        Assert.True(IndirimYetkisi.SatirIndirimiKontrol(Kasiyer, 10m).Yeterli);
        Assert.False(IndirimYetkisi.FisIndirimiKontrol(Kasiyer, 10m).Yeterli);
        Assert.True(IndirimYetkisi.FisIndirimiKontrol(Kasiyer, 5m).Yeterli);
    }

    [Fact]
    public void Mudur_KasiyerLimitiniAsanIndirimiOnaylayabilir()
    {
        Assert.True(IndirimYetkisi.SatirIndirimiKontrol(Mudur, 30m).Yeterli);
        Assert.True(IndirimYetkisi.FisIndirimiKontrol(Mudur, 25m).Yeterli);
    }

    [Fact]
    public void Mudur_MutlakLimitiAsamaz()
    {
        var (yeterli, hata) = IndirimYetkisi.SatirIndirimiKontrol(Mudur, 60m);

        Assert.False(yeterli);
        Assert.Contains("aşamaz", hata!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SifirVeyaNegatifIndirimReddedilir(decimal yuzde)
    {
        Assert.False(IndirimYetkisi.SatirIndirimiKontrol(Mudur, yuzde).Yeterli);
    }
}
