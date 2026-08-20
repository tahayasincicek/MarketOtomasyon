using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Sepete uygulanacak kampanya indirimlerini hesaplar. Veritabani bilmez,
/// dogrudan birim testlenebilir.
///
/// Cakisma kurali (musteri lehine):
///   1. Her satir icin uygun kampanyalar bulunur, EN YUKSEK indirimi
///      veren secilir. Esitlik varsa onceligi kucuk olan kazanir.
///   2. Sepet seviyesi kampanya (tutar baraji), satir indirimleri
///      dusuldukten SONRAKI tutara bakar; yine en avantajlisi secilir
///      ve satirlara brut oraninda dagitilir.
///   3. Elle indirim verilmis satira kampanya uygulanmaz; kasiyerin
///      bilincli karari otomatik kuralla ezilmemeli.
/// </summary>
public static class KampanyaHesaplayici
{
    public static KampanyaSonucuVm Hesapla(
        IReadOnlyList<SepetSatirVm> satirlar,
        IReadOnlyList<KampanyaTanimVm> kampanyalar,
        IReadOnlyDictionary<int, int> urunKategorileri,
        DateTime an)
    {
        var sonuc = new KampanyaSonucuVm();

        var gecerliler = kampanyalar.Where(k => k.TarihGecerli(an)).ToList();
        if (gecerliler.Count == 0 || satirlar.Count == 0) return sonuc;

        // Elle indirimli satirlar kampanya disidir (bkz. kural 3).
        var uygunSatirlar = satirlar.Where(s => s.IndirimTutari == 0 || s.KampanyaId is not null).ToList();

        var satirKampanyalari = gecerliler.Where(k => !k.SepetSeviyesi).ToList();
        var sepetKampanyalari = gecerliler.Where(k => k.SepetSeviyesi).ToList();

        // --- 1. Satir bazli kampanyalar ---
        var birlesmeyenSatirlar = new HashSet<int>();

        foreach (var satir in uygunSatirlar)
        {
            var enIyi = EnIyiSatirKampanyasi(satir, satirKampanyalari, urunKategorileri);
            if (enIyi is null) continue;

            sonuc.SatirIndirimleri.Add(enIyi);

            var kampanya = satirKampanyalari.First(k => k.Id == enIyi.KampanyaId);
            if (!kampanya.DigerleriyleBirlesir) birlesmeyenSatirlar.Add(satir.SatirId);
        }

        // --- 2. Sepet seviyesi kampanya ---
        if (sepetKampanyalari.Count == 0) return sonuc;

        // Baraj, satir indirimleri dusuldukten sonraki tutara bakar.
        var satirIndirimleri = sonuc.SatirIndirimleri.ToDictionary(s => s.SatirId, s => s.Indirim);
        var netToplam = satirlar.Sum(s =>
            SepetHesaplayici.BrutHesapla(s.Miktar, s.BirimFiyat)
            - s.IndirimTutari
            - satirIndirimleri.GetValueOrDefault(s.SatirId));

        var enIyiSepet = EnIyiSepetKampanyasi(sepetKampanyalari, netToplam);
        if (enIyiSepet is null) return sonuc;

        // Sepet indirimi yalnizca birlesebilen satirlara dagitilir.
        var dagitimSatirlari = uygunSatirlar.Where(s => !birlesmeyenSatirlar.Contains(s.SatirId)).ToList();
        if (dagitimSatirlari.Count == 0) return sonuc;

        var dagitim = SepetHesaplayici.FisIndiriminiDagit(dagitimSatirlari, enIyiSepet.Value.Indirim);

        foreach (var (satirId, pay) in dagitim)
        {
            if (pay <= 0) continue;

            var mevcut = sonuc.SatirIndirimleri.FirstOrDefault(s => s.SatirId == satirId);
            if (mevcut is not null)
            {
                // Ayni satirda hem satir hem sepet kampanyasi varsa toplanir;
                // satirda gorunen kampanya adi sonuncusu olur.
                mevcut.Indirim += pay;
                continue;
            }

            sonuc.SatirIndirimleri.Add(new SatirIndirimVm
            {
                SatirId = satirId,
                KampanyaId = enIyiSepet.Value.Kampanya.Id,
                KampanyaAdi = enIyiSepet.Value.Kampanya.Ad,
                Indirim = pay
            });
        }

        return sonuc;
    }

    // ---------- Satir bazli ----------

    private static SatirIndirimVm? EnIyiSatirKampanyasi(
        SepetSatirVm satir,
        IReadOnlyList<KampanyaTanimVm> kampanyalar,
        IReadOnlyDictionary<int, int> urunKategorileri)
    {
        SatirIndirimVm? enIyi = null;
        var enIyiOncelik = int.MaxValue;

        foreach (var kampanya in kampanyalar)
        {
            if (!SatiraUyuyorMu(satir, kampanya, urunKategorileri)) continue;

            var indirim = SatirIndirimiHesapla(satir, kampanya);
            if (indirim <= 0) continue;

            // Musteri lehine: en yuksek indirim kazanir, esitlikte oncelik.
            var dahaIyi = enIyi is null
                || indirim > enIyi.Indirim
                || (indirim == enIyi.Indirim && kampanya.Oncelik < enIyiOncelik);

            if (!dahaIyi) continue;

            enIyi = new SatirIndirimVm
            {
                SatirId = satir.SatirId,
                KampanyaId = kampanya.Id,
                KampanyaAdi = kampanya.Ad,
                Indirim = indirim
            };
            enIyiOncelik = kampanya.Oncelik;
        }

        return enIyi;
    }

    private static bool SatiraUyuyorMu(
        SepetSatirVm satir,
        KampanyaTanimVm kampanya,
        IReadOnlyDictionary<int, int> urunKategorileri)
    {
        foreach (var kosul in kampanya.Kosullar)
        {
            var uyuyor = kosul.Tip switch
            {
                KosulTipi.Urun => kosul.UrunId == satir.UrunId,
                KosulTipi.Kategori => urunKategorileri.TryGetValue(satir.UrunId, out var kat)
                                      && kat == kosul.KategoriId,
                _ => false
            };

            if (!uyuyor) return false;

            // "N al M ode" icin satirdaki miktar N'e ulasmali.
            if (kosul.MinMiktar is not null && satir.Miktar < kosul.MinMiktar) return false;
        }

        return kampanya.Kosullar.Count > 0;
    }

    private static decimal SatirIndirimiHesapla(SepetSatirVm satir, KampanyaTanimVm kampanya)
    {
        var brut = SepetHesaplayici.BrutHesapla(satir.Miktar, satir.BirimFiyat);
        var toplam = 0m;

        foreach (var sonuc in kampanya.Sonuclar)
        {
            toplam += sonuc.Tip switch
            {
                SonucTipi.YuzdeIndirim => Yuvarla(brut * (sonuc.Yuzde ?? 0) / 100m),
                SonucTipi.TutarIndirimi => sonuc.Tutar ?? 0,
                SonucTipi.NAlMOde => NAlMOdeIndirimi(satir, kampanya, sonuc),
                _ => 0
            };
        }

        return toplam > brut ? brut : toplam;
    }

    /// <summary>
    /// "N al M ode": her N adetlik grupta (N - M) adet bedava.
    /// 7 adet, 3 al 2 ode -> 2 tam grup, 2 adet bedava, 5 adet ucret.
    /// Gruba girmeyen artik (7 % 3 = 1) tam fiyat oder.
    /// </summary>
    private static decimal NAlMOdeIndirimi(
        SepetSatirVm satir, KampanyaTanimVm kampanya, KampanyaSonucVm sonuc)
    {
        var n = kampanya.Kosullar.FirstOrDefault(k => k.MinMiktar is not null)?.MinMiktar ?? 0;
        var m = sonuc.OdenecekMiktar ?? 0;

        if (n <= 0 || m <= 0 || m >= n) return 0;

        var grupSayisi = Math.Floor(satir.Miktar / n);
        if (grupSayisi <= 0) return 0;

        var bedavaAdet = grupSayisi * (n - m);
        return Yuvarla(bedavaAdet * satir.BirimFiyat);
    }

    // ---------- Sepet bazli ----------

    private static (KampanyaTanimVm Kampanya, decimal Indirim)? EnIyiSepetKampanyasi(
        IReadOnlyList<KampanyaTanimVm> kampanyalar, decimal netToplam)
    {
        (KampanyaTanimVm Kampanya, decimal Indirim)? enIyi = null;

        foreach (var kampanya in kampanyalar)
        {
            var baraj = kampanya.Kosullar.FirstOrDefault(k => k.Tip == KosulTipi.SepetTutari)?.MinTutar ?? 0;
            if (netToplam < baraj) continue;

            var indirim = 0m;
            foreach (var sonuc in kampanya.Sonuclar)
            {
                indirim += sonuc.Tip switch
                {
                    SonucTipi.YuzdeIndirim => Yuvarla(netToplam * (sonuc.Yuzde ?? 0) / 100m),
                    SonucTipi.TutarIndirimi => sonuc.Tutar ?? 0,
                    _ => 0
                };
            }

            if (indirim <= 0) continue;
            if (indirim > netToplam) indirim = netToplam;

            var dahaIyi = enIyi is null
                || indirim > enIyi.Value.Indirim
                || (indirim == enIyi.Value.Indirim && kampanya.Oncelik < enIyi.Value.Kampanya.Oncelik);

            if (dahaIyi) enIyi = (kampanya, indirim);
        }

        return enIyi;
    }

    private static decimal Yuvarla(decimal deger)
        => decimal.Round(deger, 2, MidpointRounding.AwayFromZero);
}
