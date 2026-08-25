namespace MarketOtomasyon.Services;

/// <summary>
/// Mudur onayinin (manager override) kurallari. Veritabani bilmez,
/// saf hesaptir; dogrudan test edilebilir.
///
/// Onay bir OTURUM DEGILDIR: tek bir isleme baglidir, tek kullanimliktir.
/// Kasiyerin oturumu degismez, onay sonrasi kasiyer hala kasiyerdir.
/// Aksi halde bir kez alinan onay, o vardiya boyunca sinirsiz indirim
/// hakkina donusurdu.
/// </summary>
public static class MudurOnayiKurallari
{
    public const int SebepEnAzUzunluk = 5;
    public const int SebepEnFazlaUzunluk = 300;

    /// <summary>
    /// Onay sebebi zorunlu: denetimin asil degeri burada. Sonradan
    /// "neden %30 indirim verildi" sorusunun cevabi baska yerde yok.
    /// </summary>
    public static (bool Gecerli, string? Hata) SebepGecerliMi(string? sebep)
    {
        var temiz = sebep?.Trim();

        if (string.IsNullOrWhiteSpace(temiz))
            return (false, "Onay sebebi zorunludur.");

        if (temiz.Length < SebepEnAzUzunluk)
            return (false, $"Onay sebebi en az {SebepEnAzUzunluk} karakter olmalıdır.");

        if (temiz.Length > SebepEnFazlaUzunluk)
            return (false, $"Onay sebebi en fazla {SebepEnFazlaUzunluk} karakter olabilir.");

        return (true, null);
    }

    /// <summary>
    /// Onayin gerekip gerekmedigini ve onayla asilabilir olup olmadigini
    /// birlikte soyler.
    ///
    /// Kritik ayrim: mutlak limit onayla da asilamaz. IndirimYetkisi
    /// mutlak limiti rol kontrolunden ONCE denetliyor; override bu
    /// sirayi bozmamali, yoksa "mudur onayi" %100 indirimin kapisi olur.
    /// </summary>
    public static OnayDurumu Degerlendir(byte rol, decimal yuzde, decimal kasiyerLimiti)
    {
        if (yuzde <= 0)
            return OnayDurumu.Gecersiz;

        if (yuzde > IndirimYetkisi.MutlakLimitYuzde)
            return OnayDurumu.OnaylaDaAsilamaz;

        if (rol == IndirimYetkisi.RolMudur)
            return OnayDurumu.Gerekmez;

        return yuzde > kasiyerLimiti ? OnayDurumu.Gerekli : OnayDurumu.Gerekmez;
    }
}

public enum OnayDurumu
{
    /// <summary>Oran gecersiz (sifir veya negatif).</summary>
    Gecersiz,

    /// <summary>Yetki zaten yeterli; onay istenmez.</summary>
    Gerekmez,

    /// <summary>Kasiyer limiti asildi; mudur onayiyla yapilabilir.</summary>
    Gerekli,

    /// <summary>Mutlak limit asildi; mudur onayi da yetmez.</summary>
    OnaylaDaAsilamaz
}
