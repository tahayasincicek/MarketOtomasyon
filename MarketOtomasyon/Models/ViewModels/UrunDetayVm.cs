using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Urun detay ekrani: barkod yonetimi ve fiyat gecmisi bir arada.</summary>
public class UrunDetayVm
{
    public Urun Urun { get; set; } = new();
    public decimal? GuncelFiyat { get; set; }
    public IReadOnlyList<UrunBarkod> Barkodlar { get; set; } = [];
    public IReadOnlyList<FiyatGecmisiSatirVm> FiyatGecmisi { get; set; } = [];

    /// <summary>Barkod ekleme formunun kendi alanlari; hatali gonderimde geri doldurulur.</summary>
    public BarkodFormVm YeniBarkod { get; set; } = new();
}

public class FiyatGecmisiSatirVm
{
    public decimal Fiyat { get; set; }
    public DateTime BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public bool Guncel => BitisTarihi is null;
}

public class BarkodFormVm
{
    public int UrunId { get; set; }
    public string Barkod { get; set; } = string.Empty;
    public decimal Carpan { get; set; } = 1;

    /// <summary>1: tekli, 2: koli.</summary>
    public byte Tip { get; set; } = 1;
}
