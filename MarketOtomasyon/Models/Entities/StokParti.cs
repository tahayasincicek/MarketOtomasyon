namespace MarketOtomasyon.Models.Entities;

/// <summary>Tek maliyetle depoya giren ve FIFO sırasında tüketilen stok katmanı.</summary>
public class StokParti
{
    public long Id { get; set; }
    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public long? StokHareketId { get; set; }
    public DateTime GirisTarihi { get; set; }
    public decimal GirisMiktari { get; set; }
    public decimal KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public string? Aciklama { get; set; }
}
