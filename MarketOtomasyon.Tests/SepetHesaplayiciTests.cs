using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class SepetHesaplayiciTests
{
    private static SepetSatirVm Satir(decimal miktar, decimal fiyat, decimal kdv, decimal indirim = 0, int id = 0) => new()
    {
        SatirId = id,
        Miktar = miktar,
        BirimFiyat = fiyat,
        KdvOrani = kdv,
        IndirimTutari = indirim
    };

    /// <summary>
    /// 10 adet x 100 TL, %10 indirim, %20 KDV senaryosu.
    /// Fiyat KDV dahil oldugu icin musteriden 900 TL tahsil edilir;
    /// 150 TL bu tutarin icinden ayristirilan KDV'dir.
    /// </summary>
    [Fact]
    public void KabulSenaryosu_OnAdetYuzTL_YuzdeOnIndirim_YuzdeYirmiKdv()
    {
        var indirim = SepetHesaplayici.BrutHesapla(10, 100m) * 0.10m;   // 1000 x %10 = 100

        var sepet = SepetHesaplayici.Topla([Satir(10, 100m, 20, indirim)]);

        Assert.Equal(900m, sepet.GenelToplam);      // 1000 - 100
        Assert.Equal(150m, sepet.ToplamKdv);        // 900 - 900/1,20
        Assert.Equal(750m, sepet.AraToplam);        // matrah
        Assert.Equal(100m, sepet.ToplamIndirim);

        var kirilim = Assert.Single(sepet.KdvKirilimi);
        Assert.Equal(20m, kirilim.Oran);
        Assert.Equal(750m, kirilim.Matrah);
        Assert.Equal(150m, kirilim.KdvTutari);
        Assert.Equal(900m, kirilim.Toplam);
    }

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
    [InlineData(120, 20, 20)]      // 120 icindeki %20 KDV
    [InlineData(110, 10, 10)]
    [InlineData(101, 1, 1)]
    [InlineData(118, 18, 18)]
    [InlineData(50, 0, 0)]         // KDV'siz urun
    public void KdvAyristir_KdvDahilTutarinIcindekiKdviBulur(decimal tutar, decimal oran, decimal beklenen)
    {
        Assert.Equal(beklenen, SepetHesaplayici.KdvAyristir(tutar, oran));
    }

    [Fact]
    public void Topla_SatirinToplamKdvVeNetAlanlariniDoldurur()
    {
        var sepet = SepetHesaplayici.Topla([Satir(2, 60m, 20)]);   // 120 TL KDV dahil
        var satir = sepet.Satirlar[0];

        Assert.Equal(120m, satir.SatirToplam);
        Assert.Equal(20m, satir.SatirKdv);
        Assert.Equal(100m, satir.SatirNet);
    }

    [Fact]
    public void Topla_ToplamlarBirbiriyleTutarli()
    {
        var sepet = SepetHesaplayici.Topla([Satir(2, 60m, 20)]);

        Assert.Equal(sepet.GenelToplam, sepet.AraToplam + sepet.ToplamKdv);
    }

    [Fact]
    public void Topla_FarkliKdvOranlariAyriAyriAyristirilir()
    {
        // 101 TL %1 gida + 120 TL %20 temizlik
        var sepet = SepetHesaplayici.Topla([Satir(1, 101m, 1), Satir(1, 120m, 20)]);

        Assert.Equal(221m, sepet.GenelToplam);
        Assert.Equal(21m, sepet.ToplamKdv);        // 1 + 20
        Assert.Equal(200m, sepet.AraToplam);
    }

    [Fact]
    public void KdvKirilimi_AyniOrandakiSatirlariGruplar()
    {
        var kirilim = SepetHesaplayici.KdvKirilimiHesapla([
            Satir(1, 101m, 1),
            Satir(1, 202m, 1),
            Satir(1, 120m, 20)
        ]);

        Assert.Equal(2, kirilim.Count);

        var birlik = kirilim.Single(k => k.Oran == 1);
        Assert.Equal(303m, birlik.Toplam);
        Assert.Equal(3m, birlik.KdvTutari);
        Assert.Equal(300m, birlik.Matrah);

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

    // ---------- Fis bazli indirimin dagitilmasi ----------

    [Fact]
    public void FisIndirimi_BrutTutarlariOraninaGoreDagitilir()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, 300m, 20, id: 1), Satir(1, 100m, 1, id: 2) };

        var dagitim = SepetHesaplayici.FisIndiriminiDagit(satirlar, 40m);   // toplam brut 400

        Assert.Equal(30m, dagitim[1]);   // 300/400 x 40
        Assert.Equal(10m, dagitim[2]);   // 100/400 x 40
    }

    [Fact]
    public void FisIndirimi_YuvarlamaArtigiEnBuyukSatiraEklenir()
    {
        // 3 esit satira 10 TL: 3,33 + 3,33 + 3,33 = 9,99; 0,01 artik kalir.
        var satirlar = new List<SepetSatirVm>
        {
            Satir(1, 100m, 20, id: 1), Satir(1, 100m, 20, id: 2), Satir(1, 100m, 20, id: 3)
        };

        var dagitim = SepetHesaplayici.FisIndiriminiDagit(satirlar, 10m);

        Assert.Equal(10m, dagitim.Values.Sum());
    }

    [Fact]
    public void FisIndirimi_ToplamBrutuAsamaz()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, 100m, 20, id: 1) };

        var dagitim = SepetHesaplayici.FisIndiriminiDagit(satirlar, 500m);

        Assert.Equal(100m, dagitim[1]);
    }

    [Fact]
    public void FisIndirimi_BosSepetteDagitimYapilmaz()
    {
        Assert.Empty(SepetHesaplayici.FisIndiriminiDagit([], 50m));
    }

    [Fact]
    public void FisIndirimi_DagitilanIndirimSonrasiKdvKirilimiTutarli()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, 300m, 20, id: 1), Satir(1, 101m, 1, id: 2) };
        var dagitim = SepetHesaplayici.FisIndiriminiDagit(satirlar, 40.10m);

        foreach (var satir in satirlar)
            satir.IndirimTutari = dagitim[satir.SatirId];

        var sepet = SepetHesaplayici.Topla(satirlar);

        Assert.Equal(40.10m, sepet.ToplamIndirim);
        Assert.Equal(360.90m, sepet.GenelToplam);                            // 401 - 40,10
        Assert.Equal(sepet.AraToplam + sepet.ToplamKdv, sepet.GenelToplam);

        foreach (var grup in sepet.KdvKirilimi)
            Assert.Equal(grup.Toplam, grup.Matrah + grup.KdvTutari);
    }
}
