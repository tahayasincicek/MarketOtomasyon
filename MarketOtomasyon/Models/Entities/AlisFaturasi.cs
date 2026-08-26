namespace MarketOtomasyon.Models.Entities;

/// <summary>
/// Alis faturasinin basligi. Tedarikci, fatura no ve toplam tutarlar
/// burada; alinan urunler AlisFaturasiSatir'da.
/// </summary>
public sealed class AlisFaturasi
{
    public int Id { get; set; }
    public int TedarikciId { get; set; }
    public string FaturaNo { get; set; } = "";
    public DateTime FaturaTarihi { get; set; }
    public DateTime KayitTarihi { get; set; }
    public int KullaniciId { get; set; }
    public int DepoId { get; set; }
    public decimal AraToplam { get; set; }
    public decimal ToplamKdv { get; set; }
    public decimal GenelToplam { get; set; }
    public string? Aciklama { get; set; }
}

public sealed class AlisFaturasiSatir
{
    public int Id { get; set; }
    public int FaturaId { get; set; }
    public int SatirNo { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }

    /// <summary>KDV haric. Parti maliyetine giden deger budur.</summary>
    public decimal BirimFiyat { get; set; }

    public decimal KdvOrani { get; set; }
    public decimal SatirMatrah { get; set; }
    public decimal SatirKdv { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }
}
