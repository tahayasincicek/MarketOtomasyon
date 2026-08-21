namespace MarketOtomasyon.Models.Entities;

/// <summary>Iade aninda satis satirindan alinan fiyat ve indirim anlik goruntusu.</summary>
public class IadeSatir
{
    public int Id { get; set; }
    public int IadeId { get; set; }
    public int FisSatirId { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimTutari { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal Tutar { get; set; }
}

