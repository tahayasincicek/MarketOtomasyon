using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Tedarikci ve alis faturasinin kurallari. Veritabani bilmez, saf
/// hesaptir; dogrudan test edilebilir.
/// </summary>
public static class TedarikciKurallari
{
    /// <summary>Fatura tarihi bugunden bu kadar geriye gidebilir.</summary>
    public const int FaturaEnFazlaGecmisYil = 1;

    public static (bool Gecerli, string? Hata) VergiNoGecerliMi(string? vergiNo)
    {
        if (string.IsNullOrWhiteSpace(vergiNo)) return (true, null);   // zorunlu degil

        var temiz = vergiNo.Trim();

        if (!temiz.All(char.IsAsciiDigit))
            return (false, "Vergi numarası yalnızca rakam içerebilir.");

        // VKN 10 hane, sahis firmalari icin TCKN 11 hane.
        return temiz.Length is 10 or 11
            ? (true, null)
            : (false, "Vergi numarası 10 (VKN) veya 11 (TCKN) haneli olmalıdır.");
    }

    /// <summary>
    /// Fatura tarihi gelecege donuk olamaz (henuz kesilmemis bir fatura
    /// girilemez) ve cok eski olamaz (parmak hatasi: 2026 yerine 2016).
    /// </summary>
    public static (bool Gecerli, string? Hata) FaturaTarihiGecerliMi(DateTime faturaTarihi, DateTime bugun)
    {
        var tarih = faturaTarihi.Date;
        var b = bugun.Date;

        if (tarih > b)
            return (false, "Fatura tarihi bugünden ileri olamaz.");

        if (tarih < b.AddYears(-FaturaEnFazlaGecmisYil))
            return (false, $"Fatura tarihi {FaturaEnFazlaGecmisYil} yıldan eski olamaz. " +
                           "Girdiğiniz tarihi kontrol edin.");

        return (true, null);
    }

    public static (bool Gecerli, string? Hata) SatirlarGecerliMi(
        IReadOnlyList<AlisFaturasiSatirVm>? satirlar)
    {
        if (satirlar is null || satirlar.Count == 0)
            return (false, "Faturaya en az bir ürün ekleyin.");

        foreach (var satir in satirlar)
        {
            if (satir.UrunId <= 0)
                return (false, "Geçersiz ürün.");

            if (satir.Miktar <= 0)
                return (false, $"{satir.UrunAd}: miktar sıfırdan büyük olmalıdır.");

            // Birim fiyat 0 olabilir: bedelsiz numune.
            if (satir.BirimFiyat < 0)
                return (false, $"{satir.UrunAd}: birim fiyat negatif olamaz.");

            if (satir.KdvOrani is < 0 or > 100)
                return (false, $"{satir.UrunAd}: KDV oranı geçersiz.");
        }

        return (true, null);
    }
}
