namespace MarketOtomasyon.Models.Entities;

public class StokHareket
{
    public long Id { get; set; }
    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public DateTime Tarih { get; set; }

    /// <summary>1: giris, 2: cikis.</summary>
    public byte Yon { get; set; }

    /// <summary>Her zaman pozitif; yonu Yon belirler.</summary>
    public decimal Miktar { get; set; }

    /// <summary>1 satis, 2 iade, 3 mal kabul, 4 sayim, 5 zayi, 6 acilis.</summary>
    public byte KaynakTip { get; set; }

    public int? KaynakId { get; set; }
    public string? Aciklama { get; set; }
}
