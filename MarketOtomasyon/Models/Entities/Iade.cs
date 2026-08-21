namespace MarketOtomasyon.Models.Entities;

/// <summary>Bir fise yapilan para iadesinin basligi.</summary>
public class Iade
{
    public int Id { get; set; }
    public string IadeNo { get; set; } = string.Empty;
    public int FisId { get; set; }
    public int KullaniciId { get; set; }
    public DateTime Tarih { get; set; }
    public decimal ToplamTutar { get; set; }
    public byte OdemeTipi { get; set; }
    public string? Aciklama { get; set; }
}

