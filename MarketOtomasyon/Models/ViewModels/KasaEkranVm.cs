namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Kasa ekraninda tek dokunusla sepete eklenecek urun.</summary>
public sealed class HizliUrunVm
{
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string Barkod { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public string? ResimYolu { get; set; }
    public int Sira { get; set; }
}

public sealed class KasaEkranVm
{
    /// <summary>Uygulama yeniden baslasa da veritabanindaki acik vardiya kullanilir.</summary>
    public int? AcikVardiyaId { get; set; }

    public bool VardiyaAcik => AcikVardiyaId is not null;

    public IReadOnlyList<HizliUrunVm> HizliUrunler { get; set; } = [];
}
