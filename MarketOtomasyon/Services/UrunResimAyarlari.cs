namespace MarketOtomasyon.Services;

/// <summary>Urun fotografi cekme ayarlari. appsettings.json -> "UrunResim".</summary>
public class UrunResimAyarlari
{
    public string ApiTabani { get; set; } = "https://world.openfoodfacts.org/api/v2/product/";

    /// <summary>
    /// Open Food Facts kimliksiz istekleri bot sayip engelliyor; bu alan zorunlu.
    /// Bicim: Uygulama/Surum (iletisim).
    /// </summary>
    public string KullaniciAjani { get; set; } = "MarketOtomasyon/1.0";

    /// <summary>wwwroot altindaki klasor adi.</summary>
    public string KlasorAdi { get; set; } = "urun-resim";

    /// <summary>
    /// Iki istek arasi bekleme. Open Food Facts dakikada 15 urun sorgusuna
    /// izin veriyor; 4500 ms ile dakikada ~13 istek yapilir, pay kalir.
    /// </summary>
    public int IstekAraligiMs { get; set; } = 4500;

    public int ZamanAsimiSaniye { get; set; } = 15;
}
