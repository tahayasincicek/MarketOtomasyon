namespace MarketOtomasyon.Models.Entities;

public class Zayi
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public int KullaniciId { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Miktar { get; set; }
    public string Sebep { get; set; } = string.Empty;
}
