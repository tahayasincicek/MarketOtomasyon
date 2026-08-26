namespace MarketOtomasyon.Services;

/// <summary>
/// Alis faturasi tutar hesaplari. Veritabanindan bagimsiz, saf hesaptir.
///
/// SATISIN TERSI YONDE calisir: SepetHesaplayici fiyatin KDV DAHIL
/// oldugunu varsayip KDV'yi tutarin icinden ayristirir (musteri etiketteki
/// tutari oder). Burada alis fiyati KDV HARICTIR ve KDV matrahin ustune
/// eklenir - alis KDV'si indirilebilir oldugu icin maliyete girmez.
///
///   SepetHesaplayici.KdvAyristir(120, 20)   == 20  (120 icinden cikar)
///   FaturaHesaplayici.SatirKdvHesapla(120, 20) == 24  (120'nin ustune eklenir)
///
/// Iki sinif kasitli olarak ayri dosyada: adlari ve yonleri farkli
/// olmali, karistirilmamali.
/// </summary>
public static class FaturaHesaplayici
{
    public static decimal SatirMatrahHesapla(decimal miktar, decimal birimFiyat)
        => decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);

    public static decimal SatirKdvHesapla(decimal matrah, decimal kdvOrani)
        => decimal.Round(matrah * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero);
}
