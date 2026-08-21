namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Her ekranin ust seridinde duran kasiyer/vardiya bilgisi.</summary>
public class UstSeritVm
{
    public string KasiyerAdi { get; set; } = string.Empty;
    public int? VardiyaId { get; set; }
    public DateTime? AcilisTarihi { get; set; }

    public bool VardiyaAcik => VardiyaId is not null;
}
