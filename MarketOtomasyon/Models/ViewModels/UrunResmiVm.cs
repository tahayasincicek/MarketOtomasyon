namespace MarketOtomasyon.Models.ViewModels;

/// <summary>
/// _UrunResmi partial'inin girdisi. Resmi olan/olmayan ayrimini ve
/// boyutu tek yerde toplar; her ekran ayni kurali tekrar yazmasin.
/// </summary>
public class UrunResmiVm
{
    public string? Yol { get; set; }
    public string Ad { get; set; } = string.Empty;

    /// <summary>kucuk (izgara satiri), orta (liste), buyuk (detay/kasa paneli).</summary>
    public string Boyut { get; set; } = "kucuk";

    /// <summary>Resim yoksa gosterilecek Bootstrap Icons sinifi.</summary>
    public string YerTutucuIkonu { get; set; } = "bi-box-seam";

    public bool VarMi => !string.IsNullOrWhiteSpace(Yol);
    public string BoyutSinifi => "urun-resim-" + Boyut;

    public static UrunResmiVm Olustur(string? yol, string ad, string boyut = "kucuk", string? kategori = null)
        => new() { Yol = yol, Ad = ad, Boyut = boyut, YerTutucuIkonu = IkonSec(kategori) };

    /// <summary>Kategoriye gore yer tutucu ikonu. Bilinmeyen kategoride genel kutu ikonu.</summary>
    private static string IkonSec(string? kategori) => kategori switch
    {
        "Gida" => "bi-basket",
        "Icecek" => "bi-cup-straw",
        "Kahvaltilik" => "bi-egg-fried",
        "Atistirmalik" => "bi-cookie",
        "Temizlik" => "bi-droplet",
        "Kisisel Bakim" => "bi-heart-pulse",
        _ => "bi-box-seam"
    };
}
