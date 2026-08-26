namespace MarketOtomasyon.Models.Entities;

public sealed class Tedarikci
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Unvan { get; set; } = "";
    public string? VergiNo { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public bool Aktif { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}
