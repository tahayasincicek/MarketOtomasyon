namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Sepetteki (beklemedeki fisin) tek satiri.</summary>
public class SepetSatirVm
{
    /// <summary>FisSatir.Id - guncelleme ve silme bunun uzerinden yapilir.</summary>
    public int SatirId { get; set; }

    public int SatirNo { get; set; }
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimTutari { get; set; }

    /// <summary>Indirim bir kampanyadan geldiyse dolu; elle indirimde bos.</summary>
    public int? KampanyaId { get; set; }

    /// <summary>Kasada satirin altinda gosterilecek kampanya adi.</summary>
    public string? KampanyaAdi { get; set; }
    public decimal KdvOrani { get; set; }

    /// <summary>KDV haric satir tutari (matrah): miktar x birim fiyat - indirim.</summary>
    public decimal SatirNet { get; set; }

    /// <summary>Net tutar uzerinden hesaplanan KDV.</summary>
    public decimal SatirKdv { get; set; }

    /// <summary>Musteriden alinacak satir tutari: net + KDV.</summary>
    public decimal SatirToplam { get; set; }
}

/// <summary>Tek bir KDV oranina ait toplamlar. Fis fisinde oran bazli dokum icin.</summary>
public class KdvKirilimVm
{
    public decimal Oran { get; set; }

    /// <summary>Bu orandaki satirlarin KDV haric toplami.</summary>
    public decimal Matrah { get; set; }

    public decimal KdvTutari { get; set; }

    /// <summary>KDV dahil toplam.</summary>
    public decimal Toplam { get; set; }
}

/// <summary>Kasa ekraninin gordugu sepet: satirlar, toplamlar ve KDV dokumu.</summary>
public class SepetVm
{
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;

    public List<SepetSatirVm> Satirlar { get; set; } = [];
    public List<KdvKirilimVm> KdvKirilimi { get; set; } = [];

    /// <summary>KDV haric tutar.</summary>
    public decimal AraToplam { get; set; }

    public decimal ToplamKdv { get; set; }
    public decimal ToplamIndirim { get; set; }

    /// <summary>Musteriden alinacak tutar (KDV dahil).</summary>
    public decimal GenelToplam { get; set; }

    public int SatirSayisi => Satirlar.Count;
    public bool Bos => Satirlar.Count == 0;
}
