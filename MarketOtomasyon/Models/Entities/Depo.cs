namespace MarketOtomasyon.Models.Entities;

public class Depo
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool Aktif { get; set; }
}
