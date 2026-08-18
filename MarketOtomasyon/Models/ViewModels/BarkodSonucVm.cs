namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Barkod okutuldugunda kasaya donen cevap.</summary>
public class BarkodSonucVm
{
    public bool Basarili { get; set; }

    /// <summary>Basarisizsa kasiyere gosterilecek metin.</summary>
    public string? Hata { get; set; }

    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal KdvOrani { get; set; }

    /// <summary>Sepete eklenecek miktar: tekli 1, koli carpan kadar, terazide barkoddaki gramaj.</summary>
    public decimal Miktar { get; set; }

    public decimal BirimFiyat { get; set; }
    public decimal SatirToplam { get; set; }

    /// <summary>Miktarin nereden geldigi; kasa ekraninda bilgi amacli gosterilir.</summary>
    public string BarkodTipi { get; set; } = string.Empty;

    public static BarkodSonucVm Basarisiz(string hata) => new() { Basarili = false, Hata = hata };
}
