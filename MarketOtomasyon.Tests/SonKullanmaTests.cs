using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Son kullanma takibinin veritabanindan bagimsiz kisimlari.
///
/// Partileri eleyen filtrenin kendisi SQL'de (MaliyetRepository) oldugu
/// icin burada test edilemez; o taraf gercek veritabaninda ROLLBACK'li
/// betikle dogrulandi. Burasi ekranin siniflandirma mantigini korur.
/// </summary>
public class SonKullanmaTests
{
    private static SonKullanmaSatirVm Satir(int kalanGun, decimal kalan = 10m, decimal maliyet = 20m) =>
        new()
        {
            StokPartiId = 1,
            UrunId = 1,
            Ad = "Süt",
            Birim = "adet",
            KalanMiktar = kalan,
            BirimMaliyet = maliyet,
            SonKullanmaTarihi = DateTime.Today.AddDays(kalanGun),
            KalanGun = kalanGun
        };

    /* ---------- Gun sayisi kirpma ---------- */

    [Theory]
    [InlineData(30, 30)]
    [InlineData(0, 0)]
    [InlineData(365, 365)]
    public void GunSayisi_GecerliDegerlerAynenGecer(int girdi, int beklenen)
        => Assert.Equal(beklenen, SonKullanmaKurallari.GunSayisiniKirp(girdi));

    [Fact]
    public void GunSayisi_NegatifDegerSifiraKirpilir()
    {
        // Negatif kalsaydi sorgunun ust siniri bugunun gerisine duser ve
        // suresi gecmis partiler de listeden cikardi.
        Assert.Equal(0, SonKullanmaKurallari.GunSayisiniKirp(-5));
    }

    [Fact]
    public void GunSayisi_CokBuyukDegerUstSinirdaKalir()
        => Assert.Equal(365, SonKullanmaKurallari.GunSayisiniKirp(99999));

    /* ---------- Satir siniflandirmasi ---------- */

    [Fact]
    public void Satir_DunDolanSuresiGecmisSayilir()
        => Assert.True(Satir(kalanGun: -1).SuresiGecmis);

    [Fact]
    public void Satir_BugunDolanSuresiGecmisSAYILMAZ()
    {
        // Satis tarafiyla ayni sinir: SonKullanmaTarihi >= bugun ise
        // urun hala satilabilir. Ekran "zayi'ye al" derken satis
        // "satilir" deseydi kullanici satilabilir mali dusururdu.
        Assert.False(Satir(kalanGun: 0).SuresiGecmis);
    }

    [Fact]
    public void Satir_IleriTarihliYaklasanSayilir()
        => Assert.False(Satir(kalanGun: 7).SuresiGecmis);

    [Fact]
    public void Satir_RiskTutariKalanIleMaliyetinCarpimi()
        => Assert.Equal(50m, Satir(kalanGun: 3, kalan: 2.5m, maliyet: 20m).RiskTutari);

    /* ---------- Ekran ozeti ---------- */

    [Fact]
    public void Ekran_GecmisVeYaklasanAyriGruplanir()
    {
        var ekran = new SonKullanmaEkranVm
        {
            Satirlar = [Satir(-3, kalan: 1m, maliyet: 10m),
                        Satir(-1, kalan: 2m, maliyet: 10m),
                        Satir(5,  kalan: 4m, maliyet: 10m)]
        };

        Assert.Equal(2, ekran.SuresiGecmisler.Count());
        Assert.Single(ekran.Yaklasanlar);
        Assert.Equal(30m, ekran.SuresiGecmisTutar);
        Assert.Equal(40m, ekran.YaklasanTutar);
    }
}
