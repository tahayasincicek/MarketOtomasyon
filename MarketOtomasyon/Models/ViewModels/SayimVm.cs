using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

public class SayimEkranVm
{
    public int DepoId { get; set; }
    public string? Aciklama { get; set; }
    public IReadOnlyList<Depo> Depolar { get; set; } = [];
    public List<SayimGirisSatirVm> Satirlar { get; set; } = [];
    public IReadOnlyList<StokHareketSatirVm> SonHareketler { get; set; } = [];
}

public class SayimGirisSatirVm
{
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal SistemMiktari { get; set; }

    /// <summary>Null olan satir sayima dahil edilmez.</summary>
    public decimal? SayilanMiktar { get; set; }

    public decimal? Fark => SayilanMiktar.HasValue
        ? SayilanMiktar.Value - SistemMiktari
        : null;
}

public class ZayiEkranVm
{
    public string? Barkod { get; set; }
    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public decimal Miktar { get; set; }
    public string? Sebep { get; set; }
    public IReadOnlyList<Depo> Depolar { get; set; } = [];
    public IReadOnlyList<Urun> Urunler { get; set; } = [];
    public IReadOnlyList<StokHareketSatirVm> SonHareketler { get; set; } = [];
}

public class SayimKayitSonucu
{
    public bool Basarili { get; set; }
    public string? Hata { get; set; }
    public int SayimId { get; set; }
    public int SayilanSatirSayisi { get; set; }
    public int DuzeltmeHareketiSayisi { get; set; }

    public static SayimKayitSonucu Basarisiz(string hata) => new() { Hata = hata };
}

public class ZayiKayitSonucu
{
    public bool Basarili { get; set; }
    public string? Hata { get; set; }
    public int ZayiId { get; set; }
    public decimal YeniBakiye { get; set; }

    public static ZayiKayitSonucu Basarisiz(string hata) => new() { Hata = hata };
}
