using MarketOtomasyon.Security;

namespace MarketOtomasyon.Services;

/// <summary>
/// Personel yonetiminin guvenlik kurallari. Veritabani bilmez, saf
/// hesaptir; dogrudan test edilebilir.
///
/// Bu ekranin kodlamasi basit ama kurallari sinsi: yanlis bir islem
/// sistemi yonetilemez hale getirebilir. En onemlisi son mudur kurali -
/// sistemde aktif mudur kalmazsa personel ekranina bir daha kimse
/// giremez ve geri donusun tek yolu elle SQL calistirmaktir.
/// </summary>
public static class KullaniciKurallari
{
    /// <summary>
    /// Pasiflestirme uc ayri sebeple reddedilebilir. Sirasi onemli:
    /// once kendi hesabi (en sik hata), sonra son mudur (en agir sonuc),
    /// en son acik vardiya (duzeltilebilir durum).
    /// </summary>
    public static (bool Gecerli, string? Hata) PasifYapilabilirMi(
        int hedefKullaniciId,
        byte hedefRol,
        int islemiYapanId,
        int aktifMudurSayisi,
        bool acikVardiyasiVar)
    {
        if (hedefKullaniciId == islemiYapanId)
            return (false, "Kendi hesabınızı pasife alamazsınız.");

        if (hedefRol == Roller.MudurKodu && aktifMudurSayisi <= 1)
            return (false, "Sistemdeki son aktif müdür pasife alınamaz. " +
                           "Önce başka bir kullanıcıyı müdür yapın.");

        // Pasif kullanici giris yapamaz; acik vardiyasi olan kasiyer
        // pasife alinirsa vardiyasini kapatamaz. Z raporu uretilemez ve
        // kasa mutabakati asili kalir.
        if (acikVardiyasiVar)
            return (false, "Bu kullanıcının açık vardiyası var. " +
                           "Pasife almadan önce vardiyanın kapatılması gerekir.");

        return (true, null);
    }

    /// <summary>
    /// Rol degisikligi son muduru dusurmemeli. Mudurden kasiyere gecis
    /// bir muduru eksiltir; kasiyerden mudure gecis her zaman guvenlidir.
    /// </summary>
    public static (bool Gecerli, string? Hata) RolDegistirilebilirMi(
        int hedefKullaniciId,
        byte mevcutRol,
        byte yeniRol,
        int islemiYapanId,
        int aktifMudurSayisi)
    {
        if (yeniRol is not (Roller.KasiyerKodu or Roller.MudurKodu))
            return (false, "Geçersiz rol.");

        if (mevcutRol == yeniRol)
            return (false, "Kullanıcı zaten bu rolde.");

        // Kendi rolunu dusurmek, son mudur olmasa bile yetkiyi anlik
        // kaybettirir ve islemin ortasinda ekrandan atilmaya yol acar.
        if (hedefKullaniciId == islemiYapanId)
            return (false, "Kendi rolünüzü değiştiremezsiniz.");

        if (mevcutRol == Roller.MudurKodu
            && yeniRol == Roller.KasiyerKodu
            && aktifMudurSayisi <= 1)
            return (false, "Sistemdeki son aktif müdürün rolü değiştirilemez.");

        return (true, null);
    }

    /// <summary>
    /// Sifre sifirlama hedefin durumundan bagimsizdir; tek sart sifrenin
    /// kendisinin gecerli olmasi. Sifre hicbir yerde saklanmaz ve
    /// loglanmaz - IslemLog'a yalnizca "sifirlandi" bilgisi yazilir.
    /// </summary>
    public static (bool Gecerli, string? Hata) SifreGecerliMi(string? sifre)
    {
        if (string.IsNullOrWhiteSpace(sifre))
            return (false, "Şifre zorunludur.");

        if (sifre.Length < 8)
            return (false, "Şifre en az 8 karakter olmalıdır.");

        if (sifre.Length > 128)
            return (false, "Şifre en fazla 128 karakter olabilir.");

        if (!sifre.Any(char.IsDigit) || !sifre.Any(char.IsLetter))
            return (false, "Şifre en az bir harf ve bir rakam içermelidir.");

        return (true, null);
    }
}
