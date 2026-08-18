namespace MarketOtomasyon.Models.Entities;

public class UrunBarkod
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public string Barkod { get; set; } = string.Empty;

    /// <summary>Koli barkodu okutulunca sepete eklenecek adet.</summary>
    public decimal Carpan { get; set; }

    /// <summary>1: tekli, 2: koli.</summary>
    public byte Tip { get; set; }
}
