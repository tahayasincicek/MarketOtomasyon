namespace MarketOtomasyon.Models.ViewModels;

/// <summary>
/// Son kullanma takip ekranindaki tek bir parti satiri.
///
/// Satir bazinda PARTI tutuluyor, urun degil: ayni urunun raftaki iki
/// partisinden biri yarin dolarken digeri uc ay sonra dolabilir. Urun
/// bazinda ozetlenseydi hangi kutunun cekilecegi belirsiz kalirdi.
/// </summary>
public sealed class SonKullanmaSatirVm
{
    public long StokPartiId { get; set; }
    public int UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public int DepoId { get; set; }
    public string DepoAd { get; set; } = string.Empty;

    public decimal KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public DateTime SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }
    public string? TedarikciUnvan { get; set; }

    /// <summary>Bugune gore kalan gun; negatif ise sure gecmis.</summary>
    public int KalanGun { get; set; }

    public bool SuresiGecmis => KalanGun < 0;

    /// <summary>Raftan cekilmesi gereken malin parasal buyuklugu.</summary>
    public decimal RiskTutari
        => decimal.Round(KalanMiktar * BirimMaliyet, 2, MidpointRounding.AwayFromZero);
}

public sealed class SonKullanmaEkranVm
{
    /// <summary>Kac gun ilerisi taranacak. Varsayilan 30.</summary>
    public int GunSayisi { get; set; } = 30;

    public int? DepoId { get; set; }
    public IReadOnlyList<Entities.Depo> Depolar { get; set; } = [];
    public IReadOnlyList<SonKullanmaSatirVm> Satirlar { get; set; } = [];

    public IEnumerable<SonKullanmaSatirVm> SuresiGecmisler
        => Satirlar.Where(s => s.SuresiGecmis);

    public IEnumerable<SonKullanmaSatirVm> Yaklasanlar
        => Satirlar.Where(s => !s.SuresiGecmis);

    public decimal SuresiGecmisTutar => SuresiGecmisler.Sum(s => s.RiskTutari);
    public decimal YaklasanTutar => Yaklasanlar.Sum(s => s.RiskTutari);
}
