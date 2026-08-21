namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Urun listesindeki tek satir. Kategori adi ve guncel fiyat join ile gelir.</summary>
public class UrunListeSatirVm
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string KategoriAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal KdvOrani { get; set; }
    public bool Tartili { get; set; }
    public bool Aktif { get; set; }

    /// <summary>Hic fiyat girilmemis urunlerde null olur.</summary>
    public decimal? GuncelFiyat { get; set; }

    /// <summary>Resmi cekilmemis urunlerde null; ekran yer tutucu gosterir.</summary>
    public string? ResimYolu { get; set; }
}

/// <summary>Liste ekraninin tamami: filtre degerleri, satirlar ve sayfalama bilgisi.</summary>
public class UrunListeVm
{
    public string? Arama { get; set; }
    public int? KategoriId { get; set; }
    public bool SadeceAktif { get; set; } = true;

    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 20;
    public int ToplamKayit { get; set; }

    public IReadOnlyList<UrunListeSatirVm> Satirlar { get; set; } = [];
    public IReadOnlyList<Entities.Kategori> Kategoriler { get; set; } = [];

    public int ToplamSayfa => ToplamKayit == 0 ? 1 : (int)Math.Ceiling(ToplamKayit / (double)SayfaBoyutu);
    public bool OncekiVar => Sayfa > 1;
    public bool SonrakiVar => Sayfa < ToplamSayfa;
}
