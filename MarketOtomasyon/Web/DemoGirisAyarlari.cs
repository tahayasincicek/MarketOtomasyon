namespace MarketOtomasyon.Web;

/// <summary>
/// Giris ekraninda deneme hesaplarinin gosterilip gosterilmeyecegi.
///
/// Tanitim amacli yayinlarda (portfolyo, demo) ziyaretcinin giris
/// bilgisini bir yerde aramasi gerekmesin diye acilir. Gercek bir
/// isletmede kurulunca varsayilan KAPALI kalir: orada ekranda sifre
/// gostermek dogrudan guvenlik acigidir.
///
/// Gelistirme ortaminda bu ayara bakilmaz, hesaplar her zaman gorunur.
/// </summary>
public sealed class DemoGirisAyarlari
{
    public const string Bolum = "DemoGiris";

    public bool Goster { get; set; }
}
