namespace MarketOtomasyon.Models.Entities;

public sealed class IslemLog
{
    public long Id { get; set; }
    public int KullaniciId { get; set; }
    public string IslemTipi { get; set; } = "";
    public string HedefTipi { get; set; } = "";
    public int? HedefId { get; set; }
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string? Aciklama { get; set; }
    public DateTime Tarih { get; set; }
}
