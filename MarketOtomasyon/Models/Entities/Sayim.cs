namespace MarketOtomasyon.Models.Entities;

public class Sayim
{
    public int Id { get; set; }
    public int DepoId { get; set; }
    public int KullaniciId { get; set; }
    public DateTime Tarih { get; set; }
    public string? Aciklama { get; set; }
}
