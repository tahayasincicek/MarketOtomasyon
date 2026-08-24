namespace MarketOtomasyon.Models.Entities;

/// <summary>Bir stok çıkışının hangi FIFO partisinden karşılandığını gösterir.</summary>
public class StokPartiTuketim
{
    public long Id { get; set; }
    public long StokPartiId { get; set; }
    public long StokHareketId { get; set; }
    public int? FisSatirId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public decimal ToplamMaliyet { get; set; }
    public DateTime Tarih { get; set; }
}
