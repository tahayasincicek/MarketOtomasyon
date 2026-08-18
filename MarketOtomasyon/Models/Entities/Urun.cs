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
}
