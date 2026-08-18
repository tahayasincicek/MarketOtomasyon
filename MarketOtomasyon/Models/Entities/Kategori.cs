namespace MarketOtomasyon.Models.Entities;

public class Kategori
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int? UstKategoriId { get; set; }
    public bool Aktif { get; set; }
}
