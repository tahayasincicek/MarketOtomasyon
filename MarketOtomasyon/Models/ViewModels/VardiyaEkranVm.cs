using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

public class VardiyaEkranVm
{
    /// <summary>Acik vardiya yoksa null; ekran "vardiya ac" formunu gosterir.</summary>
    public ZRaporVm? Acik { get; set; }

    public List<Vardiya> SonKapananlar { get; set; } = new();

    public decimal AcilisTutari { get; set; }
    public decimal SayilanTutar { get; set; }

    public string? Hata { get; set; }
}