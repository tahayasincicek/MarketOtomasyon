namespace MarketOtomasyon.Models.ViewModels;

public sealed class StokPartiKalanVm
{
    public long StokPartiId { get; set; }
    public decimal KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }

    /* Siralama SQL tarafinda yapiliyor; bu iki alan hata mesajlari ve
       ileride SKT uyarilari icin tasiniyor. */
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }
}

public sealed class FifoTuketimVm
{
    public long StokPartiId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimMaliyet { get; set; }

    /* Depo transferi hedef depoda ayni partiyi yeniden acarken bu iki
       alani kullanir. Tasinmazlarsa hedefteki parti tarihsiz kalir ve
       FEFO sirasinin sonuna duser: Arka Depo'dan rafa tasinan sut en
       son satilan urune donusur. Hata vermez, sessizce yanlis calisir. */
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? LotNo { get; set; }
    public decimal ToplamMaliyet => decimal.Round(Miktar * BirimMaliyet, 4, MidpointRounding.AwayFromZero);
}

public sealed class FifoTuketimSonucu
{
    public bool Basarili => Hata is null;
    public string? Hata { get; init; }
    public IReadOnlyList<FifoTuketimVm> Tuketimler { get; init; } = [];
    public decimal ToplamMaliyet => Tuketimler.Sum(x => x.ToplamMaliyet);

    public static FifoTuketimSonucu Basarisiz(string hata) => new() { Hata = hata };
}

public sealed class KarMarjiSatirVm
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = string.Empty;
    public string UrunAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal SatilanMiktar { get; set; }
    public decimal NetSatis { get; set; }
    public decimal SatisMaliyeti { get; set; }
    public decimal Kar => decimal.Round(NetSatis - SatisMaliyeti, 2, MidpointRounding.AwayFromZero);
    public decimal KarMarji => NetSatis == 0
        ? 0
        : decimal.Round(Kar / NetSatis * 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed class KarMarjiRaporVm
{
    public DateTime Baslangic { get; set; } = DateTime.Today;
    public DateTime Bitis { get; set; } = DateTime.Today;
    public IReadOnlyList<KarMarjiSatirVm> Satirlar { get; set; } = [];
    public decimal ToplamNetSatis => Satirlar.Sum(x => x.NetSatis);
    public decimal ToplamMaliyet => Satirlar.Sum(x => x.SatisMaliyeti);
    public decimal ToplamKar => Satirlar.Sum(x => x.Kar);
    public decimal ToplamKarMarji => ToplamNetSatis == 0
        ? 0
        : decimal.Round(ToplamKar / ToplamNetSatis * 100m, 2, MidpointRounding.AwayFromZero);
}
