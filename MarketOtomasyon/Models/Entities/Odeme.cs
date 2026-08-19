namespace MarketOtomasyon.Models.Entities;

public class Odeme
{
    public int Id { get; set; }
    public int FisId { get; set; }

    /// <summary>1: nakit, 2: kart, 3: puan.</summary>
    public byte Tip { get; set; }

    /// <summary>Fise mahsup edilen tutar.</summary>
    public decimal Tutar { get; set; }

    /// <summary>Nakitte musteriden fiilen alinan tutar; kartta null.</summary>
    public decimal? AlinanTutar { get; set; }

    public decimal? ParaUstu { get; set; }

    /// <summary>Kart odemelerinde POS onay kodu.</summary>
    public string? OnayKodu { get; set; }

    public DateTime Tarih { get; set; }
}
