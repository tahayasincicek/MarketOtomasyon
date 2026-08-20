using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class KampanyaHesaplayiciTests
{
    private static readonly DateTime Bugun = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private const int SutId = 1;
    private const int DeterjanId = 2;
    private const int GidaKategori = 10;
    private const int TemizlikKategori = 20;

    private static readonly Dictionary<int, int> UrunKategorileri = new()
    {
        [SutId] = GidaKategori,
        [DeterjanId] = TemizlikKategori
    };

    private static SepetSatirVm Satir(
        int satirId, int urunId, decimal miktar, decimal fiyat, decimal elleIndirim = 0) => new()
    {
        SatirId = satirId,
        UrunId = urunId,
        Miktar = miktar,
        BirimFiyat = fiyat,
        KdvOrani = 1,
        IndirimTutari = elleIndirim
    };

    private static KampanyaTanimVm Kampanya(
        int id, string ad, List<KampanyaKosulVm> kosullar, List<KampanyaSonucVm> sonuclar,
        int oncelik = 100, bool birlesir = false) => new()
    {
        Id = id,
        Kod = "K" + id,
        Ad = ad,
        Oncelik = oncelik,
        DigerleriyleBirlesir = birlesir,
        Aktif = true,
        BaslangicTarihi = Bugun.AddDays(-1),
        BitisTarihi = Bugun.AddDays(1),
        Kosullar = kosullar,
        Sonuclar = sonuclar
    };

    private static KampanyaKosulVm UrunKosulu(int urunId, decimal? minMiktar = null)
        => new() { Tip = KosulTipi.Urun, UrunId = urunId, MinMiktar = minMiktar };

    private static KampanyaKosulVm KategoriKosulu(int kategoriId)
        => new() { Tip = KosulTipi.Kategori, KategoriId = kategoriId };

    private static KampanyaKosulVm TutarBaraji(decimal minTutar)
        => new() { Tip = KosulTipi.SepetTutari, MinTutar = minTutar };

    private static KampanyaSonucVm Yuzde(decimal yuzde)
        => new() { Tip = SonucTipi.YuzdeIndirim, Yuzde = yuzde };

    private static KampanyaSonucVm Tutar(decimal tutar)
        => new() { Tip = SonucTipi.TutarIndirimi, Tutar = tutar };

    private static KampanyaSonucVm NAlMOde(decimal m)
        => new() { Tip = SonucTipi.NAlMOde, OdenecekMiktar = m };

    private static KampanyaSonucuVm Hesapla(
        List<SepetSatirVm> satirlar, params KampanyaTanimVm[] kampanyalar)
        => KampanyaHesaplayici.Hesapla(satirlar, kampanyalar, UrunKategorileri, Bugun);

    // ---------- Kabul senaryosu ----------

    /// <summary>
    /// Yol haritasindaki kabul kriteri: "3 al 2 ode" urununden 7 adet
    /// alinca 5 adet ucreti cikar.
    /// </summary>
    [Fact]
    public void KabulSenaryosu_UcAlIkiOde_YediAdette_BesAdetUcretiCikar()
    {
        var satir = Satir(1, SutId, miktar: 7, fiyat: 10m);   // brut 70 TL

        var sonuc = Hesapla([satir],
            Kampanya(1, "3 al 2 öde", [UrunKosulu(SutId, minMiktar: 3)], [NAlMOde(2)]));

        // 7 / 3 = 2 tam grup -> her grupta 1 bedava -> 2 adet bedava = 20 TL
        Assert.Equal(20m, sonuc.ToplamIndirim);

        // Odenecek: 70 - 20 = 50 TL, yani 5 adet ucreti.
        Assert.Equal(50m, 70m - sonuc.ToplamIndirim);
    }

    [Theory]
    [InlineData(2, 0)]     // gruba ulasmadi
    [InlineData(3, 10)]    // 1 tam grup -> 1 bedava
    [InlineData(6, 20)]    // 2 tam grup -> 2 bedava
    [InlineData(7, 20)]    // 2 tam grup + artik -> yine 2 bedava
    [InlineData(9, 30)]    // 3 tam grup
    public void NAlMOde_GrupSayisiKadarBedavaVerir(decimal miktar, decimal beklenenIndirim)
    {
        var sonuc = Hesapla([Satir(1, SutId, miktar, 10m)],
            Kampanya(1, "3 al 2 öde", [UrunKosulu(SutId, minMiktar: 3)], [NAlMOde(2)]));

        Assert.Equal(beklenenIndirim, sonuc.ToplamIndirim);
    }

    // ---------- Tip bazli ----------

    [Fact]
    public void YuzdeIndirim_UrunBazli()
    {
        var sonuc = Hesapla([Satir(1, SutId, 2, 50m)],   // brut 100
            Kampanya(1, "Sütte %10", [UrunKosulu(SutId)], [Yuzde(10)]));

        Assert.Equal(10m, sonuc.ToplamIndirim);
    }

    [Fact]
    public void YuzdeIndirim_KategoriBazli_KategorideOlmayanaUygulanmaz()
    {
        var sonuc = Hesapla(
            [Satir(1, SutId, 1, 100m), Satir(2, DeterjanId, 1, 100m)],
            Kampanya(1, "Gıda %20", [KategoriKosulu(GidaKategori)], [Yuzde(20)]));

        var indirim = Assert.Single(sonuc.SatirIndirimleri);
        Assert.Equal(1, indirim.SatirId);
        Assert.Equal(20m, indirim.Indirim);
    }

    [Fact]
    public void TutarIndirimi_SabitTutarDuser()
    {
        var sonuc = Hesapla([Satir(1, SutId, 1, 100m)],
            Kampanya(1, "5 TL indirim", [UrunKosulu(SutId)], [Tutar(5m)]));

        Assert.Equal(5m, sonuc.ToplamIndirim);
    }

    [Fact]
    public void Indirim_SatirBrutunuAsamaz()
    {
        var sonuc = Hesapla([Satir(1, SutId, 1, 10m)],
            Kampanya(1, "Abartili indirim", [UrunKosulu(SutId)], [Tutar(50m)]));

        Assert.Equal(10m, sonuc.ToplamIndirim);
    }

    [Fact]
    public void TutarBaraji_EsikAsilmadiginaUygulanmaz()
    {
        var sonuc = Hesapla([Satir(1, SutId, 1, 100m)],
            Kampanya(1, "200 TL üstü %5", [TutarBaraji(200m)], [Yuzde(5)]));

        Assert.Empty(sonuc.SatirIndirimleri);
    }

    [Fact]
    public void TutarBaraji_EsikAsilincaSepeteDagitilir()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, SutId, 1, 300m), Satir(2, DeterjanId, 1, 100m) };

        var sonuc = Hesapla(satirlar,
            Kampanya(1, "200 TL üstü %10", [TutarBaraji(200m)], [Yuzde(10)]));

        // 400 TL uzerinden %10 = 40 TL, brut oraninda dagilir.
        Assert.Equal(40m, sonuc.ToplamIndirim);
        Assert.Equal(30m, sonuc.SatirIndirimleri.Single(s => s.SatirId == 1).Indirim);
        Assert.Equal(10m, sonuc.SatirIndirimleri.Single(s => s.SatirId == 2).Indirim);
    }

    // ---------- Cakisma ----------

    [Fact]
    public void Cakisma_MusteriLehineEnYuksekIndirimSecilir()
    {
        var satir = Satir(1, SutId, 1, 100m);

        var sonuc = Hesapla([satir],
            Kampanya(1, "%10", [UrunKosulu(SutId)], [Yuzde(10)]),
            Kampanya(2, "%25", [UrunKosulu(SutId)], [Yuzde(25)]));

        var indirim = Assert.Single(sonuc.SatirIndirimleri);
        Assert.Equal(25m, indirim.Indirim);
        Assert.Equal(2, indirim.KampanyaId);
    }

    [Fact]
    public void Cakisma_EsitIndirimdeOncelikKucukOlanKazanir()
    {
        var satir = Satir(1, SutId, 1, 100m);

        var sonuc = Hesapla([satir],
            Kampanya(1, "Sonraki", [UrunKosulu(SutId)], [Yuzde(10)], oncelik: 50),
            Kampanya(2, "Öncelikli", [UrunKosulu(SutId)], [Tutar(10m)], oncelik: 10));

        var indirim = Assert.Single(sonuc.SatirIndirimleri);
        Assert.Equal(10m, indirim.Indirim);
        Assert.Equal(2, indirim.KampanyaId);
    }

    [Fact]
    public void Cakisma_UrunVeKategoriKampanyasindanIyiOlanSecilir()
    {
        var sonuc = Hesapla([Satir(1, SutId, 1, 100m)],
            Kampanya(1, "Gıda %5", [KategoriKosulu(GidaKategori)], [Yuzde(5)]),
            Kampanya(2, "Sütte %15", [UrunKosulu(SutId)], [Yuzde(15)]));

        Assert.Equal(15m, Assert.Single(sonuc.SatirIndirimleri).Indirim);
    }

    [Fact]
    public void Birlesmeyen_SatirKampanyasiVarsaSepetIndirimiOSatiraGelmez()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, SutId, 1, 300m), Satir(2, DeterjanId, 1, 100m) };

        var sonuc = Hesapla(satirlar,
            // birlesir: false -> bu satir sepet indirimine kapali
            Kampanya(1, "Sütte %10", [UrunKosulu(SutId)], [Yuzde(10)], birlesir: false),
            Kampanya(2, "200 TL üstü %10", [TutarBaraji(200m)], [Yuzde(10)]));

        var sutIndirimi = sonuc.SatirIndirimleri.Single(s => s.SatirId == 1);
        var deterjanIndirimi = sonuc.SatirIndirimleri.Single(s => s.SatirId == 2);

        // Sut yalnizca kendi kampanyasini alir (300 x %10).
        Assert.Equal(30m, sutIndirimi.Indirim);
        Assert.Equal(1, sutIndirimi.KampanyaId);

        // Sepet indirimi yalnizca deterjan satirina dagitilir.
        Assert.Equal(2, deterjanIndirimi.KampanyaId);
        Assert.True(deterjanIndirimi.Indirim > 0);
    }

    [Fact]
    public void Birlesen_SatirKampanyasiUstuneSepetIndirimiDeGelir()
    {
        var satirlar = new List<SepetSatirVm> { Satir(1, SutId, 1, 300m) };

        var sonuc = Hesapla(satirlar,
            Kampanya(1, "Sütte %10", [UrunKosulu(SutId)], [Yuzde(10)], birlesir: true),
            Kampanya(2, "200 TL üstü %10", [TutarBaraji(200m)], [Yuzde(10)]));

        // 30 TL satir indirimi + kalan 270 uzerinden 27 TL sepet indirimi.
        Assert.Equal(57m, sonuc.ToplamIndirim);
    }

    // ---------- Elle indirim ve gecerlilik ----------

    [Fact]
    public void ElleIndirimliSatira_KampanyaUygulanmaz()
    {
        var sonuc = Hesapla([Satir(1, SutId, 1, 100m, elleIndirim: 20m)],
            Kampanya(1, "Sütte %30", [UrunKosulu(SutId)], [Yuzde(30)]));

        Assert.Empty(sonuc.SatirIndirimleri);
    }

    [Fact]
    public void PasifKampanya_Uygulanmaz()
    {
        var kampanya = Kampanya(1, "Pasif", [UrunKosulu(SutId)], [Yuzde(50)]);
        kampanya.Aktif = false;

        Assert.Empty(Hesapla([Satir(1, SutId, 1, 100m)], kampanya).SatirIndirimleri);
    }

    [Fact]
    public void TarihiGecmisKampanya_Uygulanmaz()
    {
        var kampanya = Kampanya(1, "Dün bitti", [UrunKosulu(SutId)], [Yuzde(50)]);
        kampanya.BitisTarihi = Bugun.AddDays(-1);

        Assert.Empty(Hesapla([Satir(1, SutId, 1, 100m)], kampanya).SatirIndirimleri);
    }

    [Fact]
    public void HenuzBaslamamisKampanya_Uygulanmaz()
    {
        var kampanya = Kampanya(1, "Yarın başlıyor", [UrunKosulu(SutId)], [Yuzde(50)]);
        kampanya.BaslangicTarihi = Bugun.AddDays(1);

        Assert.Empty(Hesapla([Satir(1, SutId, 1, 100m)], kampanya).SatirIndirimleri);
    }

    [Fact]
    public void KampanyaYoksa_IndirimYok()
    {
        Assert.Empty(Hesapla([Satir(1, SutId, 1, 100m)]).SatirIndirimleri);
    }

    [Fact]
    public void BosSepette_Cokmez()
    {
        Assert.Empty(Hesapla([], Kampanya(1, "%10", [UrunKosulu(SutId)], [Yuzde(10)])).SatirIndirimleri);
    }
}
