namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Ana ekrandaki gunun rakamlari.</summary>
public class GunlukOzetVm
{
    public int FisSayisi { get; set; }
    public decimal Ciro { get; set; }
    public decimal Nakit { get; set; }
    public decimal Kart { get; set; }
    public int IadeSayisi { get; set; }
    public decimal IadeToplam { get; set; }
    public int KritikUrun { get; set; }

    public decimal NetCiro => Ciro - IadeToplam;

    /// <summary>Fis basi ortalama sepet tutari. Fis yoksa 0.</summary>
    public decimal OrtalamaSepet => FisSayisi == 0 ? 0 : Ciro / FisSayisi;
}

public class AnaEkranVm
{
    public GunlukOzetVm Ozet { get; set; } = new();
    public DateTime Gun { get; set; }

    /// <summary>Acik vardiya yoksa null; ana ekran uyari seridi gosterir.</summary>
    public int? AcikVardiyaId { get; set; }

    public bool VardiyaAcik => AcikVardiyaId is not null;
}
