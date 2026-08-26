using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class TedarikciKurallariTests
{
    private static readonly DateTime Bugun = new(2026, 8, 26);

    /* ---------- Vergi no ---------- */

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VergiNo_BosKabulEdilir(string? vergiNo)
    {
        var (gecerli, _) = TedarikciKurallari.VergiNoGecerliMi(vergiNo);
        Assert.True(gecerli);
    }

    [Fact]
    public void VergiNo_OnHaneKabulEdilir_Vkn()
    {
        var (gecerli, hata) = TedarikciKurallari.VergiNoGecerliMi("1234567890");
        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void VergiNo_OnbirHaneKabulEdilir_Tckn()
    {
        var (gecerli, hata) = TedarikciKurallari.VergiNoGecerliMi("12345678901");
        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Theory]
    [InlineData("123456789")]     // 9 hane
    [InlineData("123456789012")]  // 12 hane
    public void VergiNo_YanlisUzunlukReddedilir(string vergiNo)
    {
        var (gecerli, hata) = TedarikciKurallari.VergiNoGecerliMi(vergiNo);
        Assert.False(gecerli);
        Assert.Contains("10 (VKN) veya 11 (TCKN)", hata);
    }

    [Fact]
    public void VergiNo_HarfIcerirseReddedilir()
    {
        var (gecerli, hata) = TedarikciKurallari.VergiNoGecerliMi("12345ABC90");
        Assert.False(gecerli);
        Assert.Contains("yalnızca rakam", hata);
    }

    /* ---------- Fatura tarihi ---------- */

    [Fact]
    public void FaturaTarihi_BugunKabulEdilir()
    {
        var (gecerli, hata) = TedarikciKurallari.FaturaTarihiGecerliMi(Bugun, Bugun);
        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void FaturaTarihi_YarinReddedilir()
    {
        var (gecerli, hata) = TedarikciKurallari.FaturaTarihiGecerliMi(Bugun.AddDays(1), Bugun);
        Assert.False(gecerli);
        Assert.Contains("ileri olamaz", hata);
    }

    [Fact]
    public void FaturaTarihi_SinirdakiGecmisKabulEdilir()
    {
        var (gecerli, _) = TedarikciKurallari.FaturaTarihiGecerliMi(
            Bugun.AddYears(-TedarikciKurallari.FaturaEnFazlaGecmisYil), Bugun);
        Assert.True(gecerli);
    }

    [Fact]
    public void FaturaTarihi_CokEskiReddedilir()
    {
        var (gecerli, hata) = TedarikciKurallari.FaturaTarihiGecerliMi(
            Bugun.AddYears(-TedarikciKurallari.FaturaEnFazlaGecmisYil).AddDays(-1), Bugun);
        Assert.False(gecerli);
        Assert.Contains("eski olamaz", hata);
    }

    /* ---------- Satirlar ---------- */

    private static AlisFaturasiSatirVm Satir(int urunId, decimal miktar, decimal birimFiyat, decimal kdv = 20, string ad = "Ürün") =>
        new() { UrunId = urunId, UrunAd = ad, Miktar = miktar, BirimFiyat = birimFiyat, KdvOrani = kdv };

    [Fact]
    public void Satirlar_BosListeReddedilir()
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi([]);
        Assert.False(gecerli);
        Assert.Contains("en az bir ürün", hata);
    }

    [Fact]
    public void Satirlar_NullListeReddedilir()
    {
        var (gecerli, _) = TedarikciKurallari.SatirlarGecerliMi(null);
        Assert.False(gecerli);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Satirlar_GecersizMiktarReddedilir(decimal miktar)
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi([Satir(1, miktar, 10, ad: "Süt")]);
        Assert.False(gecerli);
        Assert.Contains("Süt", hata);
    }

    [Fact]
    public void Satirlar_SifirBirimFiyatKabulEdilir_BedelsizNumune()
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi([Satir(1, 5, 0)]);
        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void Satirlar_NegatifBirimFiyatReddedilir()
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi([Satir(1, 5, -1)]);
        Assert.False(gecerli);
        Assert.Contains("negatif olamaz", hata);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Satirlar_GecersizKdvOraniReddedilir(decimal kdv)
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi([Satir(1, 5, 10, kdv)]);
        Assert.False(gecerli);
        Assert.Contains("KDV oranı geçersiz", hata);
    }

    [Fact]
    public void Satirlar_GecerliListeKabulEdilir()
    {
        var (gecerli, hata) = TedarikciKurallari.SatirlarGecerliMi(
            [Satir(1, 5, 10, 20), Satir(2, 2.5m, 18.5m, 1)]);
        Assert.True(gecerli);
        Assert.Null(hata);
    }
}
