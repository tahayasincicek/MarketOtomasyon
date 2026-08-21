namespace MarketOtomasyon.Models.Entities;

public class SayimSatir
{
    public long Id { get; set; }
    public int SayimId { get; set; }
    public int UrunId { get; set; }
    public decimal SistemMiktari { get; set; }
    public decimal SayilanMiktar { get; set; }
    public decimal Fark { get; set; }
}
