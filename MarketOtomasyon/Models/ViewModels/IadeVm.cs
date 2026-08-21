namespace MarketOtomasyon.Models.ViewModels;

public class IadeAramaVm
{
    public string? FisNo { get; set; }
    public IadeFisVm? Fis { get; set; }
    public IadeFormVm Form { get; set; } = new();
    public string? Hata { get; set; }
}

public class IadeFisVm
{
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public decimal GenelToplam { get; set; }
    public byte Durum { get; set; }
    public DateTime IadeSonTarihi { get; set; }
    public List<IadeFisSatirVm> Satirlar { get; set; } = [];

    public bool IadeEdilebilir => Durum == 2
                                  && DateTime.UtcNow <= IadeSonTarihi
                                  && Satirlar.Any(s => s.KalanMiktar > 0);
}

public class IadeFisSatirVm
{
    public int FisSatirId { get; set; }
    public int SatirNo { get; set; }
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimTutari { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal SatirToplam { get; set; }
    public decimal IadeEdilenMiktar { get; set; }
    public decimal DahaOnceIadeTutari { get; set; }

    public decimal KalanMiktar => Math.Max(0, Miktar - IadeEdilenMiktar);
}

public class IadeFormVm
{
    public string FisNo { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public List<IadeTalepSatirVm> Satirlar { get; set; } = [];
}

public class IadeTalepSatirVm
{
    public int FisSatirId { get; set; }
    public decimal Miktar { get; set; }
}

public class IadeSonucVm
{
    public bool Basarili { get; set; }
    public string? Hata { get; set; }
    public int IadeId { get; set; }
    public string IadeNo { get; set; } = string.Empty;
    public decimal ToplamTutar { get; set; }

    public static IadeSonucVm Basarisiz(string hata) => new() { Hata = hata };
}

