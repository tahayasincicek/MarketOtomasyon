using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Sepet tutarlarini hesaplar. Veritabani bilmez, saf hesaptir; dogrudan test edilebilir.
///
/// Fiyat modeli: UrunFiyat'ta saklanan fiyat KDV HARICTIR.
/// Satir once net tutara indirgenir (miktar x fiyat - indirim), KDV bu net
/// tutarin uzerine eklenir. Ornek: 10 x 100 TL, %10 indirim, %20 KDV
///   net  = 1000 - 100 = 900
///   kdv  = 900 x 0,20 = 180
///   toplam = 1080
/// </summary>
public static class SepetHesaplayici
{
    /// <summary>Satirin KDV haric tutari (matrah): miktar x birim fiyat - indirim.</summary>
    public static decimal SatirNetHesapla(decimal miktar, decimal birimFiyat, decimal indirim = 0)
    {
        var brut = decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
        var net = brut - indirim;
        return net < 0 ? 0 : net;
    }

    /// <summary>Net tutar uzerinden KDV.</summary>
    public static decimal KdvHesapla(decimal netTutar, decimal kdvOrani)
    {
        if (kdvOrani <= 0) return 0;
        return decimal.Round(netTutar * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Musteriden alinacak satir tutari: net + KDV.</summary>
    public static decimal SatirToplamHesapla(decimal miktar, decimal birimFiyat, decimal kdvOrani, decimal indirim = 0)
    {
        var net = SatirNetHesapla(miktar, birimFiyat, indirim);
        return net + KdvHesapla(net, kdvOrani);
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
                var matrah = g.Sum(s => SatirNetHesapla(s.Miktar, s.BirimFiyat, s.IndirimTutari));
                var kdv = g.Sum(s => KdvHesapla(SatirNetHesapla(s.Miktar, s.BirimFiyat, s.IndirimTutari), g.Key));

                return new KdvKirilimVm
                {
                    Oran = g.Key,
                    Matrah = matrah,
                    KdvTutari = kdv,
                    Toplam = matrah + kdv
                };
            })
            .OrderBy(k => k.Oran)
            .ToList();

    /// <summary>Satir tutarlarini yeniden hesaplar ve sepet toplamlarini uretir.</summary>
    public static SepetVm Topla(List<SepetSatirVm> satirlar)
    {
        foreach (var satir in satirlar)
        {
            satir.SatirNet = SatirNetHesapla(satir.Miktar, satir.BirimFiyat, satir.IndirimTutari);
            satir.SatirKdv = KdvHesapla(satir.SatirNet, satir.KdvOrani);
            satir.SatirToplam = satir.SatirNet + satir.SatirKdv;
        }

        var kirilim = KdvKirilimiHesapla(satirlar);

        return new SepetVm
        {
            Satirlar = satirlar,
            KdvKirilimi = kirilim,
            AraToplam = satirlar.Sum(s => s.SatirNet),
            ToplamKdv = satirlar.Sum(s => s.SatirKdv),
            ToplamIndirim = satirlar.Sum(s => s.IndirimTutari),
            GenelToplam = satirlar.Sum(s => s.SatirToplam)
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
