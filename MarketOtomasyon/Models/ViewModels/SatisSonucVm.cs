namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Satis tamamlama denemesinin sonucu.</summary>
public class SatisSonucVm
{
    public bool Basarili { get; set; }
    public string? Hata { get; set; }

    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;

    /// <summary>Stok bakiyesini asan satirlar; satis gectiyse de doldurulur.</summary>
    public List<string> Uyarilar { get; set; } = [];

    public static SatisSonucVm Basarisiz(string hata, List<string>? uyarilar = null)
        => new() { Basarili = false, Hata = hata, Uyarilar = uyarilar ?? [] };
}

/// <summary>Askidaki fis listesinde gosterilen satir.</summary>
public class BekleyenFisVm
{
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public decimal GenelToplam { get; set; }
    public int SatirSayisi { get; set; }
}
