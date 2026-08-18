namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Barkod okutuldugunda kasanin ihtiyac duydugu her sey, tek sorgudan.</summary>
public class BarkodCozumVm
{
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal KdvOrani { get; set; }
    public bool Tartili { get; set; }

    public string Barkod { get; set; } = string.Empty;

    /// <summary>Koli barkodunda sepete eklenecek adet; tekli barkodda 1.</summary>
    public decimal Carpan { get; set; }

    /// <summary>1: tekli, 2: koli.</summary>
    public byte BarkodTip { get; set; }

    /// <summary>Fiyati girilmemis urunde null olur; kasa bu durumda satisa izin vermemeli.</summary>
    public decimal? Fiyat { get; set; }
}
