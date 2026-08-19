using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Sepet tutarlarini hesaplar. Veritabani bilmez, saf hesaptir; dogrudan test edilebilir.
///
/// Temel varsayim: raf fiyatlari KDV DAHILDIR (Turkiye perakende uygulamasi).
/// Bu yuzden KDV toplamin uzerine eklenmez, icinden ayristirilir.
/// </summary>
public static class SepetHesaplayici
{
    /// <summary>Satir tutari: miktar x birim fiyat, kurusa yuvarlanip indirim dusulur.</summary>
    public static decimal SatirToplamHesapla(decimal miktar, decimal birimFiyat, decimal indirim = 0)
    {
        var brut = decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
        var net = brut - indirim;
        return net < 0 ? 0 : net;
    }

    /// <summary>
    /// KDV dahil tutarin icindeki KDV: tutar - (tutar / (1 + oran/100)).
    /// Ornek: 118 TL ve %18 icin 18 TL.
    /// </summary>
    public static decimal KdvAyristir(decimal kdvDahilTutar, decimal kdvOrani)
    {
        if (kdvOrani <= 0) return 0;

        var haric = kdvDahilTutar / (1 + kdvOrani / 100m);
        return decimal.Round(kdvDahilTutar - haric, 2, MidpointRounding.AwayFromZero);
    }

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
            satir.SatirToplam = SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, satir.IndirimTutari);

        var kirilim = KdvKirilimiHesapla(satirlar);

        // Toplam KDV, oran gruplarindan toplanir; satir satir yuvarlayip
        // toplamak grup toplamindan sapma uretebilirdi.
        var toplamKdv = kirilim.Sum(k => k.KdvTutari);
        var genelToplam = satirlar.Sum(s => s.SatirToplam);

        return new SepetVm
        {
            Satirlar = satirlar,
            KdvKirilimi = kirilim,
            GenelToplam = genelToplam,
            ToplamKdv = toplamKdv,
            AraToplam = genelToplam - toplamKdv,
            ToplamIndirim = satirlar.Sum(s => s.IndirimTutari)
        };
    }
}
