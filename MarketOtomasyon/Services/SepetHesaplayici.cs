using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Sepet tutarlarini hesaplar. Veritabani bilmez, saf hesaptir; dogrudan test edilebilir.
///
/// Fiyat modeli: UrunFiyat'ta saklanan fiyat KDV DAHILDIR (Turkiye perakende
/// uygulamasi: musteri etiketteki tutari oder). KDV bu tutarin uzerine
/// eklenmez, icinden ayristirilir. Ornek: 10 x 100 TL, %10 indirim, %20 KDV
///   tahsil edilecek = 1000 - 100 = 900
///   icindeki kdv    = 900 - 900/1,20 = 150
///   matrah          = 750
/// </summary>
public static class SepetHesaplayici
{
    /// <summary>Musteriden alinacak satir tutari (KDV dahil): miktar x birim fiyat - indirim.</summary>
    public static decimal SatirToplamHesapla(decimal miktar, decimal birimFiyat, decimal indirim = 0)
    {
        var brut = decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
        var toplam = brut - indirim;
        return toplam < 0 ? 0 : toplam;
    }

    /// <summary>
    /// KDV dahil tutarin icindeki KDV: tutar - (tutar / (1 + oran/100)).
    /// 120 TL ve %20 icin 20 TL doner.
    /// </summary>
    public static decimal KdvAyristir(decimal kdvDahilTutar, decimal kdvOrani)
    {
        if (kdvOrani <= 0) return 0;

        var haric = kdvDahilTutar / (1 + kdvOrani / 100m);
        return decimal.Round(kdvDahilTutar - haric, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Indirimsiz brut tutar. Indirim tavanini denetlemek icin kullanilir.</summary>
    public static decimal BrutHesapla(decimal miktar, decimal birimFiyat)
        => decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Satirlari KDV oranina gore gruplar. Fis fisinde her oran icin ayri
    /// matrah/KDV satiri basilmasi gerektigi icin bu dokum sarttir.
    ///
    /// Satir tutarlari burada yeniden hesaplanir: metot, cagrilmadan once
    /// Topla calistirilmis olmasina bagli kalmamalidir.
    /// </summary>
    public static List<KdvKirilimVm> KdvKirilimiHesapla(IEnumerable<SepetSatirVm> satirlar) =>
        satirlar
            .GroupBy(s => s.KdvOrani)
            .Select(g =>
            {
                // KDV grup toplamindan ayristirilir: satir satir yuvarlayip toplamak
                // grup toplamindan sapma uretebilirdi.
                var toplam = g.Sum(s => SatirToplamHesapla(s.Miktar, s.BirimFiyat, s.IndirimTutari));
                var kdv = KdvAyristir(toplam, g.Key);

                return new KdvKirilimVm
                {
                    Oran = g.Key,
                    Toplam = toplam,
                    KdvTutari = kdv,
                    Matrah = toplam - kdv
                };
            })
            .OrderBy(k => k.Oran)
            .ToList();

    /// <summary>Satir tutarlarini yeniden hesaplar ve sepet toplamlarini uretir.</summary>
    public static SepetVm Topla(List<SepetSatirVm> satirlar)
    {
        foreach (var satir in satirlar)
        {
            satir.SatirToplam = SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, satir.IndirimTutari);
            satir.SatirKdv = KdvAyristir(satir.SatirToplam, satir.KdvOrani);
            satir.SatirNet = satir.SatirToplam - satir.SatirKdv;
        }

        var kirilim = KdvKirilimiHesapla(satirlar);

        // Toplam KDV ve matrah kirilimdan alinir; satir satir ayristirip toplamak
        // yuvarlama yuzunden fisin oran dokumuyle bir kurus sapabilirdi.
        var genelToplam = satirlar.Sum(s => s.SatirToplam);
        var toplamKdv = kirilim.Sum(k => k.KdvTutari);

        return new SepetVm
        {
            Satirlar = satirlar,
            KdvKirilimi = kirilim,
            AraToplam = genelToplam - toplamKdv,
            ToplamKdv = toplamKdv,
            ToplamIndirim = satirlar.Sum(s => s.IndirimTutari),
            GenelToplam = genelToplam
        };
    }

    /// <summary>
    /// Fis bazli indirimi satirlara brut tutarlari oraninda dagitir.
    ///
    /// Dagitmak sart: her satirin KDV orani farkli olabilir, indirim tek bir
    /// yerde tutulursa hangi orandan ne kadar KDV dusecegi belirsiz kalir.
    /// Yuvarlama artigi en buyuk satira eklenir; toplam birebir tutsun.
    /// </summary>
    public static Dictionary<int, decimal> FisIndiriminiDagit(
        IReadOnlyList<SepetSatirVm> satirlar, decimal indirimTutari)
    {
        var dagitim = satirlar.ToDictionary(s => s.SatirId, _ => 0m);
        if (indirimTutari <= 0 || satirlar.Count == 0) return dagitim;

        var brutler = satirlar.ToDictionary(s => s.SatirId, s => BrutHesapla(s.Miktar, s.BirimFiyat));
        var toplamBrut = brutler.Values.Sum();
        if (toplamBrut <= 0) return dagitim;

        if (indirimTutari > toplamBrut) indirimTutari = toplamBrut;

        foreach (var satir in satirlar)
            dagitim[satir.SatirId] = decimal.Round(indirimTutari * brutler[satir.SatirId] / toplamBrut, 2,
                MidpointRounding.AwayFromZero);

        var artik = indirimTutari - dagitim.Values.Sum();
        if (artik != 0)
        {
            var enBuyuk = satirlar.OrderByDescending(s => brutler[s.SatirId]).First().SatirId;
            dagitim[enBuyuk] += artik;
        }

        return dagitim;
    }
}
