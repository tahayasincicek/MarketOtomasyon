namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Kampanya kosul tipleri.</summary>
public static class KosulTipi
{
    public const byte Urun = 1;
    public const byte Kategori = 2;
    public const byte SepetTutari = 3;
}

/// <summary>Kampanya sonuc tipleri.</summary>
public static class SonucTipi
{
    public const byte YuzdeIndirim = 1;
    public const byte TutarIndirimi = 2;
    public const byte NAlMOde = 3;
}

public class KampanyaKosulVm
{
    public int Id { get; set; }
    public byte Tip { get; set; }
    public int? UrunId { get; set; }
    public int? KategoriId { get; set; }

    /// <summary>"N al M ode"deki N. Yuzde/tutar indiriminde bos.</summary>
    public decimal? MinMiktar { get; set; }

    /// <summary>Tutar barajinda sepetin gecmesi gereken tutar.</summary>
    public decimal? MinTutar { get; set; }
}

public class KampanyaSonucVm
{
    public int Id { get; set; }
    public byte Tip { get; set; }
    public decimal? Yuzde { get; set; }
    public decimal? Tutar { get; set; }

    /// <summary>"N al M ode"deki M.</summary>
    public decimal? OdenecekMiktar { get; set; }
}

/// <summary>Kampanyanin bellek icindeki tam tanimi: baslik + kosullar + sonuc.</summary>
public class KampanyaTanimVm
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int Oncelik { get; set; } = 100;
    public bool DigerleriyleBirlesir { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }

    public List<KampanyaKosulVm> Kosullar { get; set; } = [];
    public List<KampanyaSonucVm> Sonuclar { get; set; } = [];

    /// <summary>Sepet geneline (tutar barajina) mi bakiyor, tek satira mi?</summary>
    public bool SepetSeviyesi => Kosullar.Any(k => k.Tip == KosulTipi.SepetTutari);

    public bool TarihGecerli(DateTime an)
        => Aktif && BaslangicTarihi <= an && (BitisTarihi is null || BitisTarihi >= an);
}

/// <summary>Bir satira uygulanan kampanya indirimi.</summary>
public class SatirIndirimVm
{
    public int SatirId { get; set; }
    public int KampanyaId { get; set; }
    public string KampanyaAdi { get; set; } = string.Empty;
    public decimal Indirim { get; set; }
}

/// <summary>Kampanya hesabinin sonucu.</summary>
public class KampanyaSonucuVm
{
    public List<SatirIndirimVm> SatirIndirimleri { get; set; } = [];
    public decimal ToplamIndirim => SatirIndirimleri.Sum(s => s.Indirim);
}
