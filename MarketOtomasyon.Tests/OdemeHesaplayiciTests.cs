using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class OdemeHesaplayiciTests
{
    private const byte Nakit = OdemeHesaplayici.TipNakit;
    private const byte Kart = OdemeHesaplayici.TipKart;

    [Theory]
    [InlineData(40, 50, 10)]        // 40 TL nakit, musteri 50 verdi
    [InlineData(100, 100, 0)]       // tam para
    [InlineData(87.50, 100, 12.50)]
    [InlineData(40, 200, 160)]
    public void ParaUstu_AlinanTutardanMahsubuDuser(decimal tutar, decimal alinan, decimal beklenen)
    {
        Assert.Equal(beklenen, OdemeHesaplayici.ParaUstuHesapla(tutar, alinan));
    }

    [Fact]
    public void ParaUstu_AlinanEksikseNegatifDonmez()
    {
        Assert.Equal(0m, OdemeHesaplayici.ParaUstuHesapla(100m, 80m));
    }

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 40, 60)]
    [InlineData(100, 100, 0)]
    public void Kalan_GenelToplamdanOdeneniDuser(decimal genelToplam, decimal odenen, decimal beklenen)
    {
        Assert.Equal(beklenen, OdemeHesaplayici.KalanHesapla(genelToplam, odenen));
    }

    // ---------- Dogrulama kurallari ----------

    [Fact]
    public void Nakit_AlinanTutarZorunlu()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Nakit, 40m, null, 100m);

        Assert.False(gecerli);
        Assert.Contains("alinan tutar", hata!);
    }

    [Fact]
    public void Nakit_AlinanTutarMahsuptanAzOlamaz()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Nakit, 40m, 30m, 100m);

        Assert.False(gecerli);
        Assert.Contains("az olamaz", hata!);
    }

    [Fact]
    public void Nakit_AlinanFazlaysaKabulEdilir()
    {
        Assert.True(OdemeHesaplayici.Dogrula(Nakit, 40m, 50m, 100m).Gecerli);
    }

    [Fact]
    public void Kart_AlinanTutarAranmaz()
    {
        Assert.True(OdemeHesaplayici.Dogrula(Kart, 60m, null, 60m).Gecerli);
    }

    [Fact]
    public void OdemeTutari_KalanBorcuAsamaz()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Kart, 150m, null, 100m);

        Assert.False(gecerli);
        Assert.Contains("kalan borcu", hata!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void OdemeTutari_PozitifOlmali(decimal tutar)
    {
        Assert.False(OdemeHesaplayici.Dogrula(Kart, tutar, null, 100m).Gecerli);
    }

    [Fact]
    public void OdenmisFise_YeniOdemeAlinmaz()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Kart, 10m, null, 0m);

        Assert.False(gecerli);
        Assert.Contains("bakiyesi yok", hata!);
    }

    [Fact]
    public void GecersizOdemeTipiReddedilir()
    {
        Assert.False(OdemeHesaplayici.Dogrula(9, 10m, null, 100m).Gecerli);
    }

    /// <summary>Yol haritasindaki kabul senaryosu: 100 TL'nin 40'i nakit, 60'i kart.</summary>
    [Fact]
    public void KabulSenaryosu_KarisikOdeme_KalanSifirlanir()
    {
        const decimal genelToplam = 100m;

        // 1. odeme: 40 TL nakit, musteri 50 TL verdi
        var kalan = OdemeHesaplayici.KalanHesapla(genelToplam, 0m);
        Assert.True(OdemeHesaplayici.Dogrula(Nakit, 40m, 50m, kalan).Gecerli);
        Assert.Equal(10m, OdemeHesaplayici.ParaUstuHesapla(40m, 50m));

        // 2. odeme: kalan 60 TL kart
        kalan = OdemeHesaplayici.KalanHesapla(genelToplam, 40m);
        Assert.Equal(60m, kalan);
        Assert.True(OdemeHesaplayici.Dogrula(Kart, 60m, null, kalan).Gecerli);

        // Fis kapanir
        Assert.Equal(0m, OdemeHesaplayici.KalanHesapla(genelToplam, 100m));
    }
}
