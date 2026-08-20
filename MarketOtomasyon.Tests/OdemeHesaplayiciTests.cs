using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class OdemeHesaplayiciTests
{
    private const byte Nakit = OdemeHesaplayici.TipNakit;
    private const byte Kart = OdemeHesaplayici.TipKart;
    private const byte Puan = OdemeHesaplayici.TipPuan;

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

    // ---------- Acik odeme tipleri ----------

    [Fact]
    public void Nakit_Aciktir()
    {
        Assert.True(OdemeHesaplayici.TipAcikMi(Nakit));
    }

    [Theory]
    [InlineData(Kart)]
    [InlineData(Puan)]
    public void KartVePuan_HenuzKapali(byte tip)
    {
        Assert.False(OdemeHesaplayici.TipAcikMi(tip));

        var (gecerli, hata) = OdemeHesaplayici.Dogrula(tip, 60m, null, 100m);

        Assert.False(gecerli);
        Assert.Contains("yalnizca nakit", hata!);
    }

    [Fact]
    public void GecersizOdemeTipiReddedilir()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(9, 10m, null, 100m);

        Assert.False(gecerli);
        Assert.Contains("Gecersiz odeme tipi", hata!);
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
    public void OdemeTutari_KalanBorcuAsamaz()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Nakit, 150m, 150m, 100m);

        Assert.False(gecerli);
        Assert.Contains("kalan borcu", hata!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void OdemeTutari_PozitifOlmali(decimal tutar)
    {
        Assert.False(OdemeHesaplayici.Dogrula(Nakit, tutar, tutar, 100m).Gecerli);
    }

    [Fact]
    public void OdenmisFise_YeniOdemeAlinmaz()
    {
        var (gecerli, hata) = OdemeHesaplayici.Dogrula(Nakit, 10m, 10m, 0m);

        Assert.False(gecerli);
        Assert.Contains("bakiyesi yok", hata!);
    }

    /// <summary>
    /// Parcali odeme: 100 TL'lik fis iki ayri odemeyle kapanir.
    /// Kart acildiginda ikinci odeme kartla da alinabilecek; akis aynidir.
    /// </summary>
    [Fact]
    public void ParcaliOdeme_IkiOdemeIleKalanSifirlanir()
    {
        const decimal genelToplam = 100m;

        // 1. odeme: 40 TL, musteri 50 TL verdi -> 10 TL para ustu
        var kalan = OdemeHesaplayici.KalanHesapla(genelToplam, 0m);
        Assert.True(OdemeHesaplayici.Dogrula(Nakit, 40m, 50m, kalan).Gecerli);
        Assert.Equal(10m, OdemeHesaplayici.ParaUstuHesapla(40m, 50m));

        // 2. odeme: kalan 60 TL
        kalan = OdemeHesaplayici.KalanHesapla(genelToplam, 40m);
        Assert.Equal(60m, kalan);
        Assert.True(OdemeHesaplayici.Dogrula(Nakit, 60m, 60m, kalan).Gecerli);

        // Fis kapanir
        Assert.Equal(0m, OdemeHesaplayici.KalanHesapla(genelToplam, 100m));
    }
}
