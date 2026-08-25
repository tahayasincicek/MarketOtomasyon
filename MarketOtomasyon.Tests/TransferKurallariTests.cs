using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class TransferKurallariTests
{
    private const int ArkaDepo = 1;
    private const int MarketRafi = 2;

    private static TransferSatirVm Satir(int urunId, decimal miktar, string ad = "Ürün") =>
        new() { UrunId = urunId, UrunAd = ad, Miktar = miktar };

    /* ---------- Depolar ---------- */

    [Fact]
    public void Depo_AyniDepoyaTransferReddedilir()
    {
        var (gecerli, hata) = TransferKurallari.DepolarGecerliMi(ArkaDepo, ArkaDepo);

        Assert.False(gecerli);
        Assert.Contains("aynı olamaz", hata);
    }

    [Fact]
    public void Depo_FarkliDepolarKabulEdilir()
    {
        var (gecerli, hata) = TransferKurallari.DepolarGecerliMi(ArkaDepo, MarketRafi);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    public void Depo_KaynakSecilmemisseReddedilir(int kaynak, int hedef)
    {
        var (gecerli, hata) = TransferKurallari.DepolarGecerliMi(kaynak, hedef);

        Assert.False(gecerli);
        Assert.Contains("Kaynak depo", hata);
    }

    [Fact]
    public void Depo_HedefSecilmemisseReddedilir()
    {
        var (gecerli, hata) = TransferKurallari.DepolarGecerliMi(ArkaDepo, 0);

        Assert.False(gecerli);
        Assert.Contains("Hedef depo", hata);
    }

    /* ---------- Satirlar ---------- */

    [Fact]
    public void Satir_BosListeReddedilir()
    {
        var (gecerli, hata) = TransferKurallari.SatirlarGecerliMi([]);

        Assert.False(gecerli);
        Assert.Contains("en az bir ürün", hata);
    }

    [Fact]
    public void Satir_NullListeReddedilir()
    {
        var (gecerli, _) = TransferKurallari.SatirlarGecerliMi(null);
        Assert.False(gecerli);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Satir_GecersizMiktarReddedilir(decimal miktar)
    {
        var (gecerli, hata) = TransferKurallari.SatirlarGecerliMi([Satir(1, miktar, "Süt")]);

        Assert.False(gecerli);
        Assert.Contains("Süt", hata);
        Assert.Contains("sıfırdan büyük", hata);
    }

    /// <summary>
    /// UQ_TransferSatir (TransferId, UrunId) ayni urunu ikinci satirda kabul
    /// etmez; ekranin tekrari birlestirmesi gerekir. Bu kural o hatanin
    /// veritabanina kadar gitmesini engeller.
    /// </summary>
    [Fact]
    public void Satir_AyniUrunIkiKezReddedilir()
    {
        var (gecerli, hata) = TransferKurallari.SatirlarGecerliMi(
        [
            Satir(1, 5, "Ekmek"),
            Satir(2, 3, "Süt"),
            Satir(1, 2, "Ekmek")
        ]);

        Assert.False(gecerli);
        Assert.Contains("Ekmek", hata);
        Assert.Contains("birden fazla", hata);
    }

    [Fact]
    public void Satir_GecerliListeKabulEdilir()
    {
        var (gecerli, hata) = TransferKurallari.SatirlarGecerliMi(
        [
            Satir(1, 5, "Ekmek"),
            Satir(2, 2.5m, "Domates")
        ]);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    [Fact]
    public void Satir_GecersizUrunIdReddedilir()
    {
        var (gecerli, hata) = TransferKurallari.SatirlarGecerliMi([Satir(0, 5)]);

        Assert.False(gecerli);
        Assert.Contains("Geçersiz ürün", hata);
    }

    /* ---------- Tuketim son kullanma tarihi ve lotu tasiyor mu ----------
       Transfer, hedef depoda partiyi yeniden acarken bu alanlari kullanir.
       Tasinmazlarsa hedefteki parti tarihsiz kalir ve FEFO sirasinin sonuna
       duser - hata vermeden yanlis davranir. */

    [Fact]
    public void Tuketim_SonKullanmaVeLotBilgisiniTasir()
    {
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 1, KalanMiktar = 4, BirimMaliyet = 10m,
                    SonKullanmaTarihi = new DateTime(2026, 9, 15), LotNo = "LOT-A" },
            new() { StokPartiId = 2, KalanMiktar = 4, BirimMaliyet = 11m,
                    SonKullanmaTarihi = new DateTime(2026, 10, 20), LotNo = "LOT-B" }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 6m);

        Assert.True(sonuc.Basarili);
        Assert.Equal(2, sonuc.Tuketimler.Count);

        Assert.Equal(new DateTime(2026, 9, 15), sonuc.Tuketimler[0].SonKullanmaTarihi);
        Assert.Equal("LOT-A", sonuc.Tuketimler[0].LotNo);

        Assert.Equal(new DateTime(2026, 10, 20), sonuc.Tuketimler[1].SonKullanmaTarihi);
        Assert.Equal("LOT-B", sonuc.Tuketimler[1].LotNo);
    }

    [Fact]
    public void Tuketim_TarihsizPartideNullTasir()
    {
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 1, KalanMiktar = 5, BirimMaliyet = 10m,
                    SonKullanmaTarihi = null, LotNo = null }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 3m);

        Assert.True(sonuc.Basarili);
        Assert.Null(sonuc.Tuketimler[0].SonKullanmaTarihi);
        Assert.Null(sonuc.Tuketimler[0].LotNo);
    }
}
