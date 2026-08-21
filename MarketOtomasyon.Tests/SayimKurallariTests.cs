using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class SayimKurallariTests
{
    [Fact]
    public void Kabul_EksikSayim_CikisHareketiyleBakiyeyiSayilanaGetirir()
    {
        const decimal sistem = 10m;
        const decimal sayilan = 7m;

        var duzeltme = SayimKurallari.DuzeltmeHesapla(sistem, sayilan);
        var yeniBakiye = sistem - duzeltme.Miktar;

        Assert.Equal(-3m, duzeltme.Fark);
        Assert.Equal((byte)2, duzeltme.Yon);
        Assert.Equal(3m, duzeltme.Miktar);
        Assert.True(duzeltme.HareketGerekli);
        Assert.Equal(sayilan, yeniBakiye);
    }

    [Fact]
    public void FazlaSayim_GirisHareketiyleBakiyeyiSayilanaGetirir()
    {
        const decimal sistem = 10m;
        const decimal sayilan = 12.5m;

        var duzeltme = SayimKurallari.DuzeltmeHesapla(sistem, sayilan);
        var yeniBakiye = sistem + duzeltme.Miktar;

        Assert.Equal(2.5m, duzeltme.Fark);
        Assert.Equal((byte)1, duzeltme.Yon);
        Assert.Equal(2.5m, duzeltme.Miktar);
        Assert.Equal(sayilan, yeniBakiye);
    }

    [Fact]
    public void EsitSayim_StokHareketiOlusturmaz()
    {
        var duzeltme = SayimKurallari.DuzeltmeHesapla(8m, 8m);

        Assert.Equal(0m, duzeltme.Fark);
        Assert.Null(duzeltme.Yon);
        Assert.Equal(0m, duzeltme.Miktar);
        Assert.False(duzeltme.HareketGerekli);
    }

    [Fact]
    public void NegatifSayilanMiktar_Reddedilir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SayimKurallari.DuzeltmeHesapla(5m, -1m));
    }
}
