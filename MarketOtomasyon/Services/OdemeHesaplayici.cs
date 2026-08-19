namespace MarketOtomasyon.Services;

/// <summary>
/// Odeme hesaplari ve kurallari. Veritabani bilmez, dogrudan test edilebilir.
/// </summary>
public static class OdemeHesaplayici
{
    public const byte TipNakit = 1;
    public const byte TipKart = 2;
    public const byte TipPuan = 3;

    /// <summary>Nakitte para ustu: alinan - mahsup edilen. Negatif olamaz.</summary>
    public static decimal ParaUstuHesapla(decimal tutar, decimal alinanTutar)
    {
        var ustu = alinanTutar - tutar;
        return ustu < 0 ? 0 : decimal.Round(ustu, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal KalanHesapla(decimal genelToplam, decimal odenen)
        => decimal.Round(genelToplam - odenen, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Odemenin kabul edilip edilemeyecegini soyler.
    /// Kural: tutar pozitif olmali, kalan borcu asmamali; nakitte alinan tutar
    /// mahsup edilecek tutardan az olamaz.
    /// </summary>
    public static (bool Gecerli, string? Hata) Dogrula(
        byte tip, decimal tutar, decimal? alinanTutar, decimal kalan)
    {
        if (tip is not (TipNakit or TipKart or TipPuan))
            return (false, "Gecersiz odeme tipi.");

        if (tutar <= 0)
            return (false, "Odeme tutari sifirdan buyuk olmalidir.");

        if (kalan <= 0)
            return (false, "Fisin odenmemis bakiyesi yok.");

        if (tutar > kalan)
            return (false, $"Odeme tutari kalan borcu ({kalan:0.00}) asamaz.");

        if (tip == TipNakit)
        {
            if (alinanTutar is null)
                return (false, "Nakit odemede alinan tutar girilmelidir.");

            if (alinanTutar < tutar)
                return (false, "Alinan tutar, mahsup edilecek tutardan az olamaz.");
        }

        return (true, null);
    }
}
