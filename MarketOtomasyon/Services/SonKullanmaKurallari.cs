namespace MarketOtomasyon.Services;

/// <summary>
/// Son kullanma takibinin veritabanindan bagimsiz kurallari.
/// Dogrudan test edilebilsin diye ayri sinifta.
/// </summary>
public static class SonKullanmaKurallari
{
    /// <summary>Tarama penceresinin ust siniri.</summary>
    public const int EnFazlaGun = 365;

    /// <summary>
    /// Ekrandan gelen gun sayisini gecerli araliga kirpar.
    ///
    /// Deger querystring'den geliyor, yani kullanici elle degistirebilir.
    /// Negatif birakilsaydi sorgudaki ust sinir bugunun gerisine duser ve
    /// SURESI GECMIS partiler de listeden cikardi: ekran tam da gostermesi
    /// gereken seyi gizlerdi. Ust sinir ise tek sorguda tum katalogu
    /// cekmeyi engelliyor.
    /// </summary>
    public static int GunSayisiniKirp(int gunSayisi)
        => Math.Clamp(gunSayisi, 0, EnFazlaGun);
}
