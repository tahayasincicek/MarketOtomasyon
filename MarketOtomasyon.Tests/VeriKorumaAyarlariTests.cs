using MarketOtomasyon.Web;
using Microsoft.AspNetCore.DataProtection;

namespace MarketOtomasyon.Tests;

public sealed class VeriKorumaAyarlariTests
{
    [Fact]
    public void ContainerDisinda_VarsayilanAyarlarGecerlidir()
    {
        var ayarlar = new VeriKorumaAyarlari();

        Assert.Empty(ayarlar.DogrulamaHatalari(containerdaCalisiyor: false));
    }

    [Fact]
    public void Containerda_AnahtarKlasoruZorunludur()
    {
        var ayarlar = new VeriKorumaAyarlari { AnahtarKlasoru = "" };

        var hatalar = ayarlar.DogrulamaHatalari(containerdaCalisiyor: true);

        Assert.Contains(hatalar, h => h.Contains("AnahtarKlasoru"));
    }

    [Fact]
    public void AyniKlasorVeUygulamaAdi_AnahtarlariInstanceLarArasindaPaylastirir()
    {
        var klasor = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "MarketOtomasyonTests", Guid.NewGuid().ToString("N")));

        try
        {
            var birinci = DataProtectionProvider.Create(
                klasor,
                ayar => ayar.SetApplicationName("MarketOtomasyon"));
            var sifreli = birinci.CreateProtector("OturumTesti").Protect("kasiyer-oturumu");

            var ikinci = DataProtectionProvider.Create(
                klasor,
                ayar => ayar.SetApplicationName("MarketOtomasyon"));
            var cozulmus = ikinci.CreateProtector("OturumTesti").Unprotect(sifreli);

            Assert.Equal("kasiyer-oturumu", cozulmus);
        }
        finally
        {
            klasor.Delete(recursive: true);
        }
    }
}
