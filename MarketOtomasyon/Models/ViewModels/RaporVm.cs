namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Gun bazinda ciro egrisi.</summary>
public class GunlukCiroSatirVm
{
    public DateTime Gun { get; set; }
    public int FisSayisi { get; set; }
    public decimal Ciro { get; set; }
    public decimal NetCiro { get; set; }
}

/// <summary>En cok satan urunler. Miktar degil CIRO'ya gore siralanir.</summary>
public class EnCokSatanSatirVm
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = string.Empty;
    public string UrunAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal SatilanMiktar { get; set; }
    public decimal Ciro { get; set; }

    /// <summary>Bu urunun toplam cirodaki payi (%). Pencere fonksiyonuyla hesaplanir.</summary>
    public decimal CiroPayi { get; set; }
}

public class OdemeTipiSatirVm
{
    public byte Tip { get; set; }
    public int Adet { get; set; }
    public decimal Tutar { get; set; }

    public string TipAdi => Tip switch
    {
        1 => "Nakit",
        2 => "Kart",
        3 => "Puan",
        _ => "Diğer"
    };
}

/// <summary>Saat bazli yogunluk. Saat YEREL saattir (bkz. rehber 0/A).</summary>
public class SaatYogunlukSatirVm
{
    public int Saat { get; set; }
    public int FisSayisi { get; set; }
    public decimal Ciro { get; set; }

    public string SaatAraligi => $"{Saat:00}:00";
}

public class KritikStokSatirVm
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal MinStokSeviyesi { get; set; }
    public decimal Bakiye { get; set; }

    /// <summary>Minimuma donmek icin alinmasi gereken miktar.</summary>
    public decimal EksikMiktar => MinStokSeviyesi - Bakiye;
}

/// <summary>Rapor ekraninin tamami. Tek sorgu turunda doldurulur.</summary>
public class RaporVm
{
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }

    public IReadOnlyList<GunlukCiroSatirVm> GunlukCiro { get; set; } = [];
    public IReadOnlyList<EnCokSatanSatirVm> EnCokSatanlar { get; set; } = [];
    public IReadOnlyList<OdemeTipiSatirVm> OdemeDagilimi { get; set; } = [];
    public IReadOnlyList<SaatYogunlukSatirVm> SaatYogunlugu { get; set; } = [];
    public IReadOnlyList<KritikStokSatirVm> KritikStoklar { get; set; } = [];

    public decimal ToplamCiro => GunlukCiro.Sum(g => g.Ciro);
    public int ToplamFis => GunlukCiro.Sum(g => g.FisSayisi);
    public decimal OrtalamaSepet => ToplamFis == 0 ? 0 : ToplamCiro / ToplamFis;

    public bool VeriVarMi => ToplamFis > 0;
}