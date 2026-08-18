namespace MarketOtomasyon.Models.Entities;

public class UrunFiyat
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public decimal Fiyat { get; set; }
    public DateTime BaslangicTarihi { get; set; }

    /// <summary>NULL ise guncel fiyat budur.</summary>
    public DateTime? BitisTarihi { get; set; }
}
