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

    /// <summary>Resmi cekilmemis urunlerde null; kasa ekrani yer tutucu gosterir.</summary>
    public string? ResimYolu { get; set; }

    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimTutari { get; set; }

    /// <summary>Indirim bir kampanyadan geldiyse dolu; elle indirimde bos.</summary>
    public int? KampanyaId { get; set; }

    /// <summary>
    /// Bu urunde suresi gecmis ve henuz dusulmemis stok miktari.
    ///
    /// Sepete engel DEGIL, bilgilendirmedir: satilan mal taze partiden
    /// cikiyor, ama raftaki ayni urunun bir kismi bozulmus demektir.
    /// Kasiyer rozeti gorup rafa bakabilir. Satisi durduran kontrol
    /// SatisService'te, odeme aninda.
    /// </summary>
    public decimal SuresiGecmisStok { get; set; }

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

    /// <summary>
    /// Bu istekte okutulan urun. Kasa ekrani "son okutulan" panelini buna gore
    /// doldurur. Sepetin son satirina bakmak yanlis olur: okutulan urun sepette
    /// zaten varsa mevcut satirina eklenir ve son satir bambaska bir urundur.
    /// Okutma disindaki isteklerde (miktar guncelleme, satir silme) null.
    /// </summary>
    public int? SonOkutulanUrunId { get; set; }

    public int SatirSayisi => Satirlar.Count;
    public bool Bos => Satirlar.Count == 0;
}
