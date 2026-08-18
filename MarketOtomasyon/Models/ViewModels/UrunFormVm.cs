namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Urun ekleme/duzenleme formu. Dogrulama UrunFormVmValidator icinde.</summary>
public class UrunFormVm
{
    /// <summary>Yeni kayitta 0.</summary>
    public int Id { get; set; }

    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int KategoriId { get; set; }
    public string Birim { get; set; } = "ADET";
    public decimal KdvOrani { get; set; } = 20;
    public decimal MinStokSeviyesi { get; set; }
    public bool Tartili { get; set; }
    public bool Aktif { get; set; } = true;

    /// <summary>Satis fiyati. Degistiyse yeni bir UrunFiyat satiri acilir.</summary>
    public decimal Fiyat { get; set; }

    public IReadOnlyList<Entities.Kategori> Kategoriler { get; set; } = [];
}
