namespace MarketOtomasyon.Services;

/// <summary>Veritabanindan bagimsiz, test edilebilir iade hesap ve miktar kurallari.</summary>
public static class IadeKurallari
{
    public static (bool Gecerli, string? Hata) MiktarDogrula(
        decimal satilanMiktar, decimal iadeEdilenMiktar, decimal istenenMiktar)
    {
        if (istenenMiktar <= 0)
            return (false, "İade miktarı sıfırdan büyük olmalıdır.");

        var kalan = Math.Max(0, satilanMiktar - iadeEdilenMiktar);
        if (istenenMiktar > kalan)
            return (false, kalan == 0
                ? "Bu satırın tamamı daha önce iade edilmiş."
                : $"İade miktarı kalan miktarı ({kalan:0.###}) aşamaz.");

        return (true, null);
    }

    /// <summary>
    /// Iade, urun kartinin bugunku fiyatindan degil fis satirinda saklanan
    /// satis tutarindan hesaplanir. Son parca iadede kurus artigi kapatilir.
    /// </summary>
    public static decimal TutarHesapla(
        decimal satilanMiktar,
        decimal satisAnindakiSatirToplami,
        decimal iadeEdilenMiktar,
        decimal dahaOnceIadeTutari,
        decimal istenenMiktar)
    {
        var (gecerli, hata) = MiktarDogrula(satilanMiktar, iadeEdilenMiktar, istenenMiktar);
        if (!gecerli) throw new ArgumentOutOfRangeException(nameof(istenenMiktar), hata);

        var kalanMiktar = satilanMiktar - iadeEdilenMiktar;
        if (istenenMiktar == kalanMiktar)
            return decimal.Round(satisAnindakiSatirToplami - dahaOnceIadeTutari, 2,
                MidpointRounding.AwayFromZero);

        return decimal.Round(satisAnindakiSatirToplami * istenenMiktar / satilanMiktar, 2,
            MidpointRounding.AwayFromZero);
    }

    public static bool SureDolduMu(DateTime satisTarihiUtc, int sureGun, DateTime simdiUtc)
        => simdiUtc > satisTarihiUtc.AddDays(sureGun);
}
