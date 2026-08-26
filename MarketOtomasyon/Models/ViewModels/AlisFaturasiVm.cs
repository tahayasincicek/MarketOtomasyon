using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Fatura formundaki tek satir.</summary>
public sealed class AlisFaturasiSatirVm
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Birim { get; set; } = "";
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }   // KDV haric
    public decimal KdvOrani { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }

    // Ekranda gosterim icin; sunucu tarafinda FaturaHesaplayici ile
    // yeniden hesaplanir, istemciden gelen deger guvenilmez.
    public decimal SatirMatrah { get; set; }
    public decimal SatirKdv { get; set; }
}

public sealed class AlisFaturasiEkranVm
{
    public int TedarikciId { get; set; }
    public int DepoId { get; set; }
    public string FaturaNo { get; set; } = "";
    public DateTime FaturaTarihi { get; set; } = DateTime.Today;
    public string? Aciklama { get; set; }

    /// <summary>Barkod okutularak ya da urun secilerek eklenir.</summary>
    public string? Barkod { get; set; }

    public List<AlisFaturasiSatirVm> Satirlar { get; set; } = [];

    public IReadOnlyList<Tedarikci> Tedarikciler { get; set; } = [];
    public IReadOnlyList<Depo> Depolar { get; set; } = [];
    public IReadOnlyList<AlisFaturasiGecmisSatirVm> SonFaturalar { get; set; } = [];

    public string? Hata { get; set; }
}

public sealed class AlisFaturasiGecmisSatirVm
{
    public int Id { get; set; }
    public string FaturaNo { get; set; } = "";
    public DateTime FaturaTarihi { get; set; }
    public string TedarikciUnvan { get; set; } = "";
    public string Depo { get; set; } = "";
    public int SatirSayisi { get; set; }
    public decimal GenelToplam { get; set; }
}

public sealed class AlisFaturasiDetaySatirVm
{
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public decimal Miktar { get; set; }
    public string Birim { get; set; } = "";
    public decimal BirimFiyat { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal SatirMatrah { get; set; }
    public decimal SatirKdv { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }
}

public sealed class AlisFaturasiDetayVm
{
    public int Id { get; set; }
    public string FaturaNo { get; set; } = "";
    public DateTime FaturaTarihi { get; set; }
    public string TedarikciUnvan { get; set; } = "";
    public string Depo { get; set; } = "";
    public string KullaniciAdSoyad { get; set; } = "";
    public string? Aciklama { get; set; }
    public decimal AraToplam { get; set; }
    public decimal ToplamKdv { get; set; }
    public decimal GenelToplam { get; set; }
    public IReadOnlyList<AlisFaturasiDetaySatirVm> Satirlar { get; set; } = [];
}
