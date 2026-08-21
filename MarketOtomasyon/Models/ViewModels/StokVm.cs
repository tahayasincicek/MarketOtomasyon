using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

public class StokSatirVm
{
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal MinStokSeviyesi { get; set; }
    public decimal ToplamBakiye { get; set; }

    /// <summary>Resmi cekilmemis urunlerde null; ekran yer tutucu gosterir.</summary>
    public string? ResimYolu { get; set; }

    public bool Kritik => ToplamBakiye <= MinStokSeviyesi;
}

public class StokHareketSatirVm
{
    public long Id { get; set; }
    public DateTime Tarih { get; set; }
    public byte Yon { get; set; }
    public decimal Miktar { get; set; }
    public byte KaynakTip { get; set; }
    public string? Aciklama { get; set; }
    public string UrunKod { get; set; } = string.Empty;
    public string UrunAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;

    public string YonAdi => Yon == 1 ? "Giris" : "Cikis";

    public string KaynakAdi => KaynakTip switch
    {
        1 => "Satis",
        2 => "Iade",
        3 => "Mal kabul",
        4 => "Sayim",
        5 => "Zayi",
        6 => "Acilis",
        _ => "Diger"
    };
}

public class StokListeVm
{
    public string? Arama { get; set; }
    public bool SadeceKritik { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 20;
    public int ToplamKayit { get; set; }
    public IReadOnlyList<StokSatirVm> Satirlar { get; set; } = [];

    public int ToplamSayfa => ToplamKayit == 0 ? 1 : (int)Math.Ceiling(ToplamKayit / (double)SayfaBoyutu);
    public bool OncekiVar => Sayfa > 1;
    public bool SonrakiVar => Sayfa < ToplamSayfa;
}

/// <summary>Basit mal kabul formu.</summary>
public class MalKabulVm
{
    /// <summary>Barkod okutularak ya da urun secilerek doldurulur.</summary>
    public string? Barkod { get; set; }

    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public decimal Miktar { get; set; }
    public string? Aciklama { get; set; }

    public IReadOnlyList<Depo> Depolar { get; set; } = [];
    public IReadOnlyList<StokHareketSatirVm> SonHareketler { get; set; } = [];
}
