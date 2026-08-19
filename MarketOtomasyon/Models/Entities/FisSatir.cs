namespace MarketOtomasyon.Models.Entities;

public class FisSatir
{
    public int Id { get; set; }
    public int FisId { get; set; }
    public int SatirNo { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }

    /// <summary>Satis anindaki fiyat. Urun kartindan okunmaz, burada saklanir.</summary>
    public decimal BirimFiyat { get; set; }

    public decimal IndirimTutari { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal SatirToplam { get; set; }
    public decimal IadeEdilenMiktar { get; set; }
    public int? KampanyaId { get; set; }
}
