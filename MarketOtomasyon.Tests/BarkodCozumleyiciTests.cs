using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class BarkodCozumleyiciTests
{
    [Theory]
    [InlineData("8690000000012")]   // seed: Sut 1 L tekli
    [InlineData("8690000000029")]   // seed: Sut 1 L koli
    [InlineData("5449000000996")]   // gercek bir EAN-13
    public void Ean13Gecerli_DogruKontrolHanesiOlaniKabulEder(string barkod)
    {
        Assert.True(BarkodCozumleyici.Ean13Gecerli(barkod));
    }

    [Theory]
    [InlineData("8690000000017")]   // son hane 2 olmaliydi
    [InlineData("5449000000999")]   // son hane 6 olmaliydi
    public void Ean13Gecerli_YanlisKontrolHanesiniReddeder(string barkod)
    {
        Assert.False(BarkodCozumleyici.Ean13Gecerli(barkod));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("869000000001")]     // 12 hane
    [InlineData("86900000000123")]   // 14 hane
    [InlineData("869000000001A")]    // harf iceriyor
    public void Ean13Gecerli_BicimiBozukOlaniReddeder(string? barkod)
    {
        Assert.False(BarkodCozumleyici.Ean13Gecerli(barkod));
    }

    [Fact]
    public void Ean13KontrolHanesi_BilinenDegeriUretir()
    {
        Assert.Equal('2', BarkodCozumleyici.Ean13KontrolHanesi("869000000001"));
        Assert.Equal('6', BarkodCozumleyici.Ean13KontrolHanesi("544900000099"));
    }

    [Theory]
    [InlineData("2800001012500", true)]    // 28 oneki
    [InlineData("2900001012500", true)]    // 29 oneki
    [InlineData("8690000000012", false)]   // normal urun barkodu
    [InlineData("2800001", false)]         // 13 hane degil
    public void TeraziBarkoduMu_OnekeGoreAyirir(string barkod, bool beklenen)
    {
        Assert.Equal(beklenen, BarkodCozumleyici.TeraziBarkoduMu(barkod));
    }

    [Theory]
    [InlineData("2800001012500", "2800001", 1.250)]   // 1250 g -> 1.250 kg
    [InlineData("2800003003400", "2800003", 0.340)]   // 340 g
    [InlineData("2800002250000", "2800002", 25.000)]  // 25000 g -> 25 kg
    public void TeraziAyristir_AnahtariVeGramajiCozer(string barkod, string beklenenAnahtar, decimal beklenenKg)
    {
        var (anahtar, miktar) = BarkodCozumleyici.TeraziAyristir(barkod);

        Assert.Equal(beklenenAnahtar, anahtar);
        Assert.Equal(beklenenKg, miktar);
    }

    [Fact]
    public void TeraziAyristir_TeraziOlmayanBarkodaHataVerir()
    {
        Assert.Throws<ArgumentException>(() => BarkodCozumleyici.TeraziAyristir("8690000000012"));
    }
}
