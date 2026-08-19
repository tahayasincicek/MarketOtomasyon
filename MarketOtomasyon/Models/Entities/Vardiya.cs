namespace MarketOtomasyon.Models.Entities;

public class Vardiya
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public DateTime AcilisTarihi { get; set; }
    public decimal AcilisTutari { get; set; }
    public DateTime? KapanisTarihi { get; set; }
    public decimal? SayilanTutar { get; set; }
    public decimal? BeklenenTutar { get; set; }
    public decimal? Fark { get; set; }

    /// <summary>1: acik, 2: kapali.</summary>
    public byte Durum { get; set; }
}
