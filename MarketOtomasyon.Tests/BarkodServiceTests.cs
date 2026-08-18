using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class BarkodServiceTests
{
    private const string TekliBarkod = "8690000000012";
    private const string KoliBarkod  = "8690000000029";
    private const string TeraziKod   = "2800001";

    private static BarkodService Servis()
    {
        var repo = new SahteBarkodRepository()
            .Ekle(TekliBarkod, new BarkodCozumVm
            {
                UrunId = 1, Kod = "URN001", Ad = "Sut 1 L", Birim = "ADET",
                KdvOrani = 1, Barkod = TekliBarkod, Carpan = 1, BarkodTip = 1, Fiyat = 32.50m
            })
            .Ekle(KoliBarkod, new BarkodCozumVm
            {
                UrunId = 1, Kod = "URN001", Ad = "Sut 1 L", Birim = "ADET",
                KdvOrani = 1, Barkod = KoliBarkod, Carpan = 12, BarkodTip = 2, Fiyat = 32.50m
            })
            .Ekle(TeraziKod, new BarkodCozumVm
            {
                UrunId = 2, Kod = "URN002", Ad = "Domates", Birim = "KG",
                KdvOrani = 1, Tartili = true, Barkod = TeraziKod, Carpan = 1, BarkodTip = 3, Fiyat = 24.90m
            })
            .Ekle("FIYATSIZ", new BarkodCozumVm
            {
                UrunId = 3, Kod = "URN099", Ad = "Fiyatsiz Urun", Birim = "ADET",
                Barkod = "FIYATSIZ", Carpan = 1, BarkodTip = 1, Fiyat = null
            });

        return new BarkodService(repo);
    }

    /// <summary>Verilen ilk 12 haneye dogru kontrol hanesini ekleyip gecerli terazi barkodu uretir.</summary>
    private static string TeraziBarkodu(string urunKodu7, int gramaj)
    {
        var ilk12 = urunKodu7 + gramaj.ToString("D5");
        return ilk12 + BarkodCozumleyici.Ean13KontrolHanesi(ilk12);
    }

    [Fact]
    public async Task TekliBarkod_BirAdetDoner()
    {
        var sonuc = await Servis().CozAsync(TekliBarkod);

        Assert.True(sonuc.Basarili);
        Assert.Equal("Sut 1 L", sonuc.Ad);
        Assert.Equal(1m, sonuc.Miktar);
        Assert.Equal(32.50m, sonuc.SatirToplam);
        Assert.Equal("tekli", sonuc.BarkodTipi);
    }

    [Fact]
    public async Task KoliBarkodu_CarpanKadarMiktarDoner()
    {
        var sonuc = await Servis().CozAsync(KoliBarkod);

        Assert.True(sonuc.Basarili);
        Assert.Equal(12m, sonuc.Miktar);
        Assert.Equal(390.00m, sonuc.SatirToplam);   // 12 x 32.50
        Assert.Equal("koli", sonuc.BarkodTipi);
    }

    [Fact]
    public async Task TeraziBarkodu_GramajdanMiktariCozer()
    {
        var barkod = TeraziBarkodu(TeraziKod, 1250);   // 1.250 kg domates

        var sonuc = await Servis().CozAsync(barkod);

        Assert.True(sonuc.Basarili);
        Assert.Equal("Domates", sonuc.Ad);
        Assert.Equal(1.250m, sonuc.Miktar);
        Assert.Equal(31.13m, sonuc.SatirToplam);      // 1.250 x 24.90 = 31.125 -> 31.13
        Assert.Equal("terazi", sonuc.BarkodTipi);
    }

    [Fact]
    public async Task GecersizKontrolHanesi_Reddedilir()
    {
        var sonuc = await Servis().CozAsync("8690000000017");   // son hane 2 olmaliydi

        Assert.False(sonuc.Basarili);
        Assert.Contains("kontrol hanesi", sonuc.Hata!);
    }

    [Fact]
    public async Task TanimsizBarkod_AnlamliHataDoner()
    {
        var sonuc = await Servis().CozAsync("1234567890128");   // gecerli EAN, kayitli degil

        Assert.False(sonuc.Basarili);
        Assert.Contains("bulunamadi", sonuc.Hata!);
    }

    [Fact]
    public async Task TanimsizTeraziKodu_AnlamliHataDoner()
    {
        var sonuc = await Servis().CozAsync(TeraziBarkodu("2809999", 500));

        Assert.False(sonuc.Basarili);
        Assert.Contains("tanimli degil", sonuc.Hata!);
    }

    [Fact]
    public async Task FiyatiOlmayanUrun_SatisaAcilmaz()
    {
        var sonuc = await Servis().CozAsync("FIYATSIZ");

        Assert.False(sonuc.Basarili);
        Assert.Contains("fiyat", sonuc.Hata!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BosBarkod_Reddedilir(string? barkod)
    {
        var sonuc = await Servis().CozAsync(barkod);

        Assert.False(sonuc.Basarili);
    }
}
