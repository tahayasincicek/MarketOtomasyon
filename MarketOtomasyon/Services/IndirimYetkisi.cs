namespace MarketOtomasyon.Services;

/// <summary>
/// Manuel indirimde kimin ne kadar indirim yapabilecegini belirler.
/// Kasiyer sinirli indirim verebilir; sinirin ustu mudur onayi ister.
/// </summary>
public static class IndirimYetkisi
{
    public const byte RolKasiyer = 1;
    public const byte RolMudur = 2;

    /// <summary>Kasiyerin onaysiz verebilecegi en yuksek satir indirimi.</summary>
    public const decimal KasiyerSatirLimitiYuzde = 10m;

    /// <summary>Kasiyerin onaysiz verebilecegi en yuksek fis indirimi.</summary>
    public const decimal KasiyerFisLimitiYuzde = 5m;

    /// <summary>Mudur dahil kimse bunun uzerine cikamaz; bedava satis manuel indirimle yapilmaz.</summary>
    public const decimal MutlakLimitYuzde = 50m;

    public static (bool Yeterli, string? Hata) SatirIndirimiKontrol(byte rol, decimal yuzde)
        => Kontrol(rol, yuzde, KasiyerSatirLimitiYuzde, "Satir");

    public static (bool Yeterli, string? Hata) FisIndirimiKontrol(byte rol, decimal yuzde)
        => Kontrol(rol, yuzde, KasiyerFisLimitiYuzde, "Fis");

    private static (bool Yeterli, string? Hata) Kontrol(byte rol, decimal yuzde, decimal kasiyerLimiti, string kapsam)
    {
        if (yuzde <= 0)
            return (false, "Indirim orani sifirdan buyuk olmalidir.");

        if (yuzde > MutlakLimitYuzde)
            return (false, $"Indirim %{MutlakLimitYuzde:0.##} oranini asamaz.");

        if (rol == RolMudur)
            return (true, null);

        if (yuzde > kasiyerLimiti)
            return (false, $"{kapsam} indiriminde %{kasiyerLimiti:0.##} ustu mudur onayi gerektirir.");

        return (true, null);
    }
}
