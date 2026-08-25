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

    /// <summary>Resim yoksa gosterilecek Phosphor Icons sinifi.</summary>
    public string YerTutucuIkonu { get; set; } = "ph-package";

    public bool VarMi => !string.IsNullOrWhiteSpace(Yol);
    public string BoyutSinifi => "urun-resim-" + Boyut;

    public static UrunResmiVm Olustur(string? yol, string ad, string boyut = "kucuk", string? kategori = null)
        => new() { Yol = yol, Ad = ad, Boyut = boyut, YerTutucuIkonu = IkonSec(kategori) };

    /// <summary>Kategoriye gore yer tutucu ikonu. Bilinmeyen kategoride genel kutu ikonu.</summary>
    private static string IkonSec(string? kategori) => kategori switch
    {
        "Gıda" or "Gida" => "ph-basket",
        "İçecek" or "Icecek" => "ph-beer-bottle",
        "Kahvaltılık" or "Kahvaltilik" => "ph-egg",
        "Atıştırmalık" or "Atistirmalik" => "ph-cookie",
        "Temizlik" => "ph-drop",
        "Kişisel Bakım" or "Kisisel Bakim" => "ph-heartbeat",
        _ => "ph-package"
    };
}
