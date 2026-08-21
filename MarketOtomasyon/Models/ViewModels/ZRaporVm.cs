namespace MarketOtomasyon.Models.ViewModels;

/// <summary>
/// Vardiya ozeti. Vardiya acikken de okunabilir (X raporu),
/// kapandiktan sonra okununca Z raporu olur.
/// </summary>
public class ZRaporVm
{
    public int VardiyaId { get; set; }
    public int KullaniciId { get; set; }
    public string KasiyerAdi { get; set; } = string.Empty;
    public DateTime AcilisTarihi { get; set; }
    public decimal AcilisTutari { get; set; }
    public DateTime? KapanisTarihi { get; set; }
    public decimal? SayilanTutar { get; set; }
    public byte Durum { get; set; }

    public int FisSayisi { get; set; }
    public decimal Ciro { get; set; }
    public decimal ToplamIndirim { get; set; }
    public decimal ToplamKdv { get; set; }

    public decimal NakitSatis { get; set; }
    public decimal KartSatis { get; set; }
    public decimal PuanSatis { get; set; }

    public int IadeSayisi { get; set; }
    public decimal IadeToplam { get; set; }
    public decimal NakitIade { get; set; }

    /// <summary>Kapanista Vardiya satirina yazilan degerler. Vardiya acikken null.</summary>
    public decimal? KayitliBeklenen { get; set; }
    public decimal? KayitliFark { get; set; }

    public bool Acik => Durum == 1;

    /// <summary>Kasada olmasi gereken nakit: acilis + nakit satis - nakit iade.</summary>
    public decimal AnlikBeklenen => AcilisTutari + NakitSatis - NakitIade;

    /// <summary>
    /// Vardiya acikken canli hesaplanir. Kapandiktan sonra kapanis anindaki
    /// deger gosterilir: o vardiyaya sonradan iade duserse gecmis rapor degismesin.
    /// </summary>
    public decimal BeklenenTutar => Acik ? AnlikBeklenen : KayitliBeklenen ?? AnlikBeklenen;

    /// <summary>Sayilan - beklenen. Artida ise kasa fazlasi, eksideyse kasa acigi.</summary>
    public decimal? Fark => Acik
        ? (SayilanTutar is null ? null : SayilanTutar.Value - BeklenenTutar)
        : KayitliFark ?? (SayilanTutar is null ? null : SayilanTutar.Value - BeklenenTutar);

    public decimal NetCiro => Ciro - IadeToplam;
}