using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class FifoMaliyetHesaplayiciTests
{
    [Fact]
    public void IkiFarkliMaliyet_IlkPartiYeterliyseSadeceIlkPartidenTuketir()
    {
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 10, KalanMiktar = 3, BirimMaliyet = 20 },
            new() { StokPartiId = 20, KalanMiktar = 5, BirimMaliyet = 35 }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 2);

        Assert.True(sonuc.Basarili);
        var tuketim = Assert.Single(sonuc.Tuketimler);
        Assert.Equal(10, tuketim.StokPartiId);
        Assert.Equal(2, tuketim.Miktar);
        Assert.Equal(20, tuketim.BirimMaliyet);
        Assert.Equal(40, sonuc.ToplamMaliyet);
    }

    [Fact]
    public void IlkPartiYetmezse_KalaniIkinciPartidenTuketir()
    {
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 10, KalanMiktar = 3, BirimMaliyet = 20 },
            new() { StokPartiId = 20, KalanMiktar = 5, BirimMaliyet = 35 }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 4);

        Assert.True(sonuc.Basarili);
        Assert.Collection(
            sonuc.Tuketimler,
            ilk =>
            {
                Assert.Equal(10, ilk.StokPartiId);
                Assert.Equal(3, ilk.Miktar);
                Assert.Equal(20, ilk.BirimMaliyet);
            },
            ikinci =>
            {
                Assert.Equal(20, ikinci.StokPartiId);
                Assert.Equal(1, ikinci.Miktar);
                Assert.Equal(35, ikinci.BirimMaliyet);
            });
        Assert.Equal(95, sonuc.ToplamMaliyet);
    }

    [Fact]
    public void PartiBakiyesiYetmezse_TuketimOlusturmaz()
    {
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 10, KalanMiktar = 1, BirimMaliyet = 20 }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 2);

        Assert.False(sonuc.Basarili);
        Assert.Empty(sonuc.Tuketimler);
        Assert.Contains("Eksik miktar", sonuc.Hata);
    }
}
