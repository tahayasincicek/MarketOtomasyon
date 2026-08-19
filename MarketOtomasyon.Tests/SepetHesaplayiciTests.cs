using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class SepetHesaplayiciTests
{
    private static SepetSatirVm Satir(decimal miktar, decimal fiyat, decimal kdv, decimal indirim = 0) => new()
    {
        Miktar = miktar,
        BirimFiyat = fiyat,
        KdvOrani = kdv,
        IndirimTutari = indirim
    };

    [Theory]
    [InlineData(1, 32.50, 0, 32.50)]
    [InlineData(12, 32.50, 0, 390.00)]
    [InlineData(1.250, 24.90, 0, 31.13)]     // 31.125 -> yukari yuvarlanir
    [InlineData(3, 18.75, 6.25, 50.00)]      // 56.25 - 6.25 indirim
    public void SatirToplam_MiktarFiyatVeIndirimiDogruHesaplar(
        decimal miktar, decimal fiyat, decimal indirim, decimal beklenen)
    {
        Assert.Equal(beklenen, SepetHesaplayici.SatirToplamHesapla(miktar, fiyat, indirim));
    }

    [Fact]
    public void SatirToplam_IndirimTutardanBuyukseSifirDoner()
    {
        Assert.Equal(0m, SepetHesaplayici.SatirToplamHesapla(1, 10m, 25m));
    }

    [Theory]
    [InlineData(118, 18, 18)]        // klasik ornek
    [InlineData(120, 20, 20)]
    [InlineData(110, 10, 10)]
    [InlineData(101, 1, 1)]
    [InlineData(50, 0, 0)]           // KDV'siz urun
    public void KdvAyristir_KdvDahilTutarinIcindekiKdviBulur(
        decimal tutar, decimal oran, decimal beklenen)
    {
        Assert.Equal(beklenen, SepetHesaplayici.KdvAyristir(tutar, oran));
    }

    [Fact]
    public void Topla_TekSatirdaToplamlarTutarli()
    {
        var sepet = SepetHesaplayici.Topla([Satir(2, 60m, 20)]);   // 120 TL, %20 KDV dahil

        Assert.Equal(120m, sepet.GenelToplam);
        Assert.Equal(20m, sepet.ToplamKdv);
        Assert.Equal(100m, sepet.AraToplam);
        Assert.Equal(sepet.GenelToplam, sepet.AraToplam + sepet.ToplamKdv);
    }

    [Fact]
    public void Topla_FarkliKdvOranlariAyriAyriAyristirilir()
    {
        // 100 TL %1 gida + 120 TL %20 temizlik
        var sepet = SepetHesaplayici.Topla([Satir(1, 100m, 1), Satir(1, 120m, 20)]);

        Assert.Equal(220m, sepet.GenelToplam);
        Assert.Equal(0.99m + 20m, sepet.ToplamKdv);   // 100/1.01 -> 0.99, 120/1.20 -> 20
        Assert.Equal(sepet.GenelToplam - sepet.ToplamKdv, sepet.AraToplam);
    }

    [Fact]
    public void KdvKirilimi_AyniOrandakiSatirlariGruplar()
    {
        var kirilim = SepetHesaplayici.KdvKirilimiHesapla([
            Satir(1, 100m, 1),
            Satir(1, 200m, 1),
            Satir(1, 120m, 20)
        ]);

        Assert.Equal(2, kirilim.Count);

        var birlik = kirilim.Single(k => k.Oran == 1);
        Assert.Equal(300m, birlik.Toplam);
        Assert.Equal(2.97m, birlik.KdvTutari);
        Assert.Equal(297.03m, birlik.Matrah);

        var yirmilik = kirilim.Single(k => k.Oran == 20);
        Assert.Equal(120m, yirmilik.Toplam);
        Assert.Equal(20m, yirmilik.KdvTutari);
        Assert.Equal(100m, yirmilik.Matrah);
    }

    [Fact]
    public void KdvKirilimi_OranaGoreSirali()
    {
        var kirilim = SepetHesaplayici.KdvKirilimiHesapla([
            Satir(1, 120m, 20), Satir(1, 110m, 10), Satir(1, 101m, 1)
        ]);

        Assert.Equal([1m, 10m, 20m], kirilim.Select(k => k.Oran));
    }

    [Fact]
    public void KdvKirilimi_HerGrubunMatrahVeKdvToplamiGrupToplaminaEsit()
    {
        var kirilim = SepetHesaplayici.KdvKirilimiHesapla([
            Satir(3, 18.75m, 1), Satir(2, 47.50m, 10), Satir(1, 89m, 20)
        ]);

        foreach (var grup in kirilim)
            Assert.Equal(grup.Toplam, grup.Matrah + grup.KdvTutari);
    }

    [Fact]
    public void Topla_BosSepetSifirDoner()
    {
        var sepet = SepetHesaplayici.Topla([]);

        Assert.True(sepet.Bos);
        Assert.Equal(0m, sepet.GenelToplam);
        Assert.Equal(0m, sepet.ToplamKdv);
        Assert.Empty(sepet.KdvKirilimi);
    }

    [Fact]
    public void Topla_SatirToplamlariniYenidenHesaplar()
    {
        // Disaridan yanlis SatirToplam gelse bile duzeltilir.
        var satir = Satir(2, 50m, 20);
        satir.SatirToplam = 9999m;

        var sepet = SepetHesaplayici.Topla([satir]);

        Assert.Equal(100m, sepet.Satirlar[0].SatirToplam);
        Assert.Equal(100m, sepet.GenelToplam);
    }
}
