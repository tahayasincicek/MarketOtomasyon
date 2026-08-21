namespace MarketOtomasyon.Models.Entities;

public class Urun
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int KategoriId { get; set; }

    /// <summary>ADET veya KG.</summary>
    public string Birim { get; set; } = string.Empty;

    public decimal KdvOrani { get; set; }
    public decimal MinStokSeviyesi { get; set; }
    public bool Tartili { get; set; }
    public bool Aktif { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    /// <summary>wwwroot altindaki gorece yol (/urun-resim/URN001.jpg). Resim yoksa null.</summary>
    public string? ResimYolu { get; set; }

    /// <summary>Resmin kaynagi ve lisansi. CC-BY-SA atifi icin ekranda gosterilir.</summary>
    public string? ResimKaynagi { get; set; }

    public DateTime? ResimTarihi { get; set; }
}
