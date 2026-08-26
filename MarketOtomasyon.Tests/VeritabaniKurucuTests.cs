using MarketOtomasyon.Data;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Kurulum betiklerinin secim mantigi. Veritabani gerektirmez.
///
/// Bu testlerin korudugu sey: demo betikleri uretimde ASLA
/// calismamali. 30_/31_ sahte satis gecmisi ve ornek tedarikci uretir;
/// gercek bir markette ciro raporlarini bozar ve stok sayilarini
/// sasirtir.
/// </summary>
public class VeritabaniKurucuTests
{
    private const string Onek = "MarketOtomasyon.Data.Sql.";

    /* ---------- Sema ve ornek veri her zaman secilir ---------- */

    [Theory]
    [InlineData("01_ilk_sema.sql")]
    [InlineData("13_tedarikci_fatura.sql")]
    [InlineData("20_ornek_veri.sql")]
    [InlineData("23_hizli_urun.sql")]
    public void Sema_DemoKapaliOlsaBileSecilir(string dosya)
        => Assert.True(VeritabaniKurucu.BetikSecilsinMi(Onek + dosya, demoVerisiDahil: false));

    /* ---------- Demo betikleri ---------- */

    [Theory]
    [InlineData("30_demo_satis_gecmisi.sql")]
    [InlineData("31_demo_tedarikci_fatura.sql")]
    public void Demo_VarsayilanOlarakDISARIDA(string dosya)
        => Assert.False(VeritabaniKurucu.BetikSecilsinMi(Onek + dosya, demoVerisiDahil: false));

    [Theory]
    [InlineData("30_demo_satis_gecmisi.sql")]
    [InlineData("31_demo_tedarikci_fatura.sql")]
    public void Demo_AcikcaIstendigindeSecilir(string dosya)
        => Assert.True(VeritabaniKurucu.BetikSecilsinMi(Onek + dosya, demoVerisiDahil: true));

    /* ---------- Sinir durumlari ---------- */

    [Fact]
    public void SqlOlmayanKaynakSecilmez()
    {
        // Derlemede gorseller, css gibi baska gomulu kaynaklar da
        // bulunabilir; yalnizca .sql alinmali.
        Assert.False(VeritabaniKurucu.BetikSecilsinMi(
            "MarketOtomasyon.wwwroot.css.site.css", demoVerisiDahil: true));
    }

    [Fact]
    public void BaskaKlasordekiSqlSecilmez()
    {
        // Yalnizca Data/Sql altindakiler kurulum betigidir.
        Assert.False(VeritabaniKurucu.BetikSecilsinMi(
            "MarketOtomasyon.Belgeler.ornek.sql", demoVerisiDahil: true));
    }

    [Fact]
    public void Adinda3Gecen_SemaBetigiDemoSanilmaz()
    {
        /* Demo filtresi "Data.Sql.3" onekine bakiyor, dosya adinin
           herhangi bir yerindeki 3'e degil. Yoksa 13_tedarikci_fatura
           ya da 23_hizli_urun demo sayilir ve uretimde hic
           uygulanmazdi - sema eksik kalirdi. */
        Assert.True(VeritabaniKurucu.BetikSecilsinMi(
            Onek + "13_tedarikci_fatura.sql", demoVerisiDahil: false));

        Assert.True(VeritabaniKurucu.BetikSecilsinMi(
            Onek + "23_hizli_urun.sql", demoVerisiDahil: false));

        Assert.True(VeritabaniKurucu.BetikSecilsinMi(
            Onek + "03_kampanya.sql", demoVerisiDahil: false));
    }
}
