using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Yazdirilabilir fis goruntusu icin gereken her sey.</summary>
public class FisYazdirVm
{
    public Fis Fis { get; set; } = new();
    public List<SepetSatirVm> Satirlar { get; set; } = [];
    public List<KdvKirilimVm> KdvKirilimi { get; set; } = [];
    public List<OdemeSatirVm> Odemeler { get; set; } = [];

    public string KasiyerAdi { get; set; } = string.Empty;

    public decimal ToplamParaUstu => Odemeler.Sum(o => o.ParaUstu ?? 0);

    public string DurumAdi => Fis.Durum switch
    {
        1 => "BEKLEMEDE",
        2 => "SATIS FISI",
        9 => "IPTAL",
        _ => ""
    };
}
