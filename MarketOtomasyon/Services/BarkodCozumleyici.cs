namespace MarketOtomasyon.Services;

/// <summary>
/// Barkod metninin kendi icindeki bilgiyi cozer. Veritabanina hic dokunmaz,
/// bu yuzden dogrudan birim testlenebilir.
/// </summary>
public static class BarkodCozumleyici
{
    /// <summary>Terazi barkodlarinin sabit oneki. 28 ve 29 magaza ici kullanima ayrilmistir.</summary>
    private static readonly string[] TeraziOnekleri = ["28", "29"];

    private const int TeraziAnahtarUzunlugu = 7;   // onek (2) + urun kodu (5)
    private const int TeraziGramajUzunlugu = 5;

    /// <summary>
    /// EAN-13 kontrol hanesi dogrulamasi. Soldan 1., 3., 5. ... haneler 1 ile,
    /// 2., 4., 6. ... haneler 3 ile carpilir; toplamin 10'a tamamlayani son hanedir.
    /// </summary>
    public static bool Ean13Gecerli(string? barkod)
    {
        if (barkod is null || barkod.Length != 13) return false;
        if (!barkod.All(char.IsAsciiDigit)) return false;

        var toplam = 0;
        for (var i = 0; i < 12; i++)
        {
            var hane = barkod[i] - '0';
            toplam += (i % 2 == 0) ? hane : hane * 3;
        }

        var beklenen = (10 - (toplam % 10)) % 10;
        return beklenen == barkod[12] - '0';
    }

    /// <summary>Verilen 12 haneye ait kontrol hanesini uretir. Terazi barkodu simule etmek icin kullanilir.</summary>
    public static char Ean13KontrolHanesi(string ilk12Hane)
    {
        ArgumentException.ThrowIfNullOrEmpty(ilk12Hane);
        if (ilk12Hane.Length != 12 || !ilk12Hane.All(char.IsAsciiDigit))
            throw new ArgumentException("12 haneli rakam dizisi bekleniyor.", nameof(ilk12Hane));

        var toplam = 0;
        for (var i = 0; i < 12; i++)
        {
            var hane = ilk12Hane[i] - '0';
            toplam += (i % 2 == 0) ? hane : hane * 3;
        }

        return (char)('0' + (10 - (toplam % 10)) % 10);
    }

    /// <summary>13 haneli ve terazi onekiyle basliyor mu?</summary>
    public static bool TeraziBarkoduMu(string? barkod) =>
        barkod is { Length: 13 }
        && barkod.All(char.IsAsciiDigit)
        && TeraziOnekleri.Any(o => barkod.StartsWith(o, StringComparison.Ordinal));

    /// <summary>
    /// Terazi barkodunu arama anahtari ve miktara ayirir.
    /// Gramaj gram cinsindedir; kilograma cevrilir (1250 -> 1.250 kg).
    /// </summary>
    public static (string Anahtar, decimal MiktarKg) TeraziAyristir(string barkod)
    {
        if (!TeraziBarkoduMu(barkod))
            throw new ArgumentException("Terazi barkodu degil.", nameof(barkod));

        var anahtar = barkod[..TeraziAnahtarUzunlugu];
        var gramaj = int.Parse(barkod.Substring(TeraziAnahtarUzunlugu, TeraziGramajUzunlugu));

        return (anahtar, gramaj / 1000m);
    }
}
