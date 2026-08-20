namespace MarketOtomasyon.Models.ViewModels;

public class OdemeSatirVm
{
    public int Id { get; set; }
    public byte Tip { get; set; }
    public decimal Tutar { get; set; }
    public decimal? AlinanTutar { get; set; }
    public decimal? ParaUstu { get; set; }
    public string? OnayKodu { get; set; }
    public DateTime Tarih { get; set; }

    public string TipAdi => Tip switch
    {
        1 => "Nakit",
        2 => "Kart",
        3 => "Puan",
        _ => "Diger"
    };
}

/// <summary>Odeme ekraninin gordugu durum: ne kadari odendi, ne kadari kaldi.</summary>
public class OdemeDurumVm
{
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;

    /// <summary>Fisin tahsil edilmesi gereken tutari.</summary>
    public decimal GenelToplam { get; set; }

    public decimal Odenen { get; set; }
    public List<OdemeSatirVm> Odemeler { get; set; } = [];

    /// <summary>Kalan borc; sifirlaninca fis kapanir.</summary>
    public decimal Kalan => decimal.Round(GenelToplam - Odenen, 2, MidpointRounding.AwayFromZero);

    /// <summary>Fis odendi mi (Durum 2).</summary>
    public bool Tamamlandi { get; set; }

    /// <summary>Son nakit odemede musteriye verilecek para ustu.</summary>
    public decimal ToplamParaUstu { get; set; }

    /// <summary>Satis kapanirken olusan stok uyarilari (varsa).</summary>
    public List<string> Uyarilar { get; set; } = [];
}
