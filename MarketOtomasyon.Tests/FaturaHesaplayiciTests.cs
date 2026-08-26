using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class FaturaHesaplayiciTests
{
    [Fact]
    public void SatirMatrahHesapla_MiktarCarpiFiyat()
    {
        var matrah = FaturaHesaplayici.SatirMatrahHesapla(100, 12.50m);
        Assert.Equal(1250.00m, matrah);
    }

    [Fact]
    public void SatirKdvHesapla_MatrahinUstuneEklenir()
    {
        var matrah = FaturaHesaplayici.SatirMatrahHesapla(100, 12.50m);
        var kdv = FaturaHesaplayici.SatirKdvHesapla(matrah, 20);

        Assert.Equal(1250.00m, matrah);
        Assert.Equal(250.00m, kdv);
        Assert.Equal(1500.00m, matrah + kdv);
    }

    [Fact]
    public void SatirKdvHesapla_KdvSifirsaKdvSifirdir()
    {
        var matrah = FaturaHesaplayici.SatirMatrahHesapla(10, 5m);
        var kdv = FaturaHesaplayici.SatirKdvHesapla(matrah, 0);

        Assert.Equal(0m, kdv);
    }

    /// <summary>Yuvarlama: 3 x 3,33 = 9,99; %18 KDV = 1,7982 degil 1,80.</summary>
    [Fact]
    public void Yuvarlama_SatirBazindaYapilir()
    {
        var matrah = FaturaHesaplayici.SatirMatrahHesapla(3, 3.33m);
        var kdv = FaturaHesaplayici.SatirKdvHesapla(matrah, 18);

        Assert.Equal(9.99m, matrah);
        Assert.Equal(1.80m, kdv);
    }

    /// <summary>
    /// FaturaHesaplayici ile SepetHesaplayici birbirinin TERSI yonde
    /// calisir. Alis fiyati KDV HARICTIR (KDV ustune eklenir), satis
    /// fiyati KDV DAHILDIR (KDV icinden ayristirilir). Ikisini
    /// karistirmamak icin ayni testte yan yana dogrulaniyor.
    /// </summary>
    [Fact]
    public void FaturaKdvsi_SepetKdvsininTersiYondedir()
    {
        var satisKdvsi = SepetHesaplayici.KdvAyristir(120, 20);
        var alisKdvsi = FaturaHesaplayici.SatirKdvHesapla(120, 20);

        Assert.Equal(20m, satisKdvsi);     // 120 TL'nin ICINDEN 20 TL KDV cikar
        Assert.Equal(24m, alisKdvsi);      // 120 TL'nin USTUNE 24 TL KDV eklenir
    }

    [Fact]
    public void SatirlarToplami_FaturaToplamiylaTutar()
    {
        var satirlar = new[]
        {
            (Miktar: 100m, Fiyat: 12.50m, Kdv: 20m),
            (Miktar: 3m, Fiyat: 3.33m, Kdv: 18m),
            (Miktar: 5m, Fiyat: 0m, Kdv: 1m)   // bedelsiz numune
        };

        decimal araToplam = 0, toplamKdv = 0;
        foreach (var (miktar, fiyat, kdv) in satirlar)
        {
            var matrah = FaturaHesaplayici.SatirMatrahHesapla(miktar, fiyat);
            araToplam += matrah;
            toplamKdv += FaturaHesaplayici.SatirKdvHesapla(matrah, kdv);
        }

        Assert.Equal(1259.99m, araToplam);
        Assert.Equal(251.80m, toplamKdv);
    }
}
