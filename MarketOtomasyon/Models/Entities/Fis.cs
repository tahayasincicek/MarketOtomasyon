namespace MarketOtomasyon.Models.Entities;

public class Fis
{
    public int Id { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public int VardiyaId { get; set; }
    public int KullaniciId { get; set; }
    public int? MusteriId { get; set; }
    public DateTime Tarih { get; set; }

    /// <summary>KDV haric tutar.</summary>
    public decimal AraToplam { get; set; }

    public decimal ToplamIndirim { get; set; }
    public decimal ToplamKdv { get; set; }

    /// <summary>Musteriden alinacak tutar (KDV dahil).</summary>
    public decimal GenelToplam { get; set; }

    /// <summary>1: beklemede, 2: tamamlandi, 9: iptal.</summary>
    public byte Durum { get; set; }
}
