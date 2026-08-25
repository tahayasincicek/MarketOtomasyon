namespace MarketOtomasyon.Services;

/// <summary>
/// Mal kabulde parti bilgilerinin kurallari. Veritabani bilmez, saf
/// hesaptir; dogrudan test edilebilir.
///
/// "bugun" disaridan parametre olarak alinir, DateTime.Today cagrilmaz:
/// aksi halde testte bugunu kontrol edemez, gece yarisini gecen kosuda
/// rastgele kirilan testler yazmis olurduk.
/// </summary>
public static class PartiKurallari
{
    /// <summary>
    /// Parmak hatasi tavani. 2026 yerine 2226 yazmak kolaydir ve o parti
    /// FEFO sirasinin en sonuna dusup yillarca satilmaz.
    /// </summary>
    public const int EnFazlaRafOmruYil = 10;

    public const int LotEnFazlaUzunluk = 50;

    /// <summary>
    /// Son kullanma tarihini dogrular.
    ///
    /// zorunluMu, Urun.SonKullanmaZorunlu bayragindan gelir. Bos birakilan
    /// tarih partiyi FEFO sirasinin SONUNA atar; yani sut girisinde tarihi
    /// unutmak, sutu "en son satilacak" partiye cevirir. Bayrak bu hatayi
    /// girişte yakalar.
    /// </summary>
    public static (bool Gecerli, string? Hata) SonKullanmaGecerliMi(
        DateTime? sonKullanmaTarihi, bool zorunluMu, DateTime bugun)
    {
        if (sonKullanmaTarihi is null)
            return zorunluMu
                ? (false, "Bu ürün için son kullanma tarihi zorunludur.")
                : (true, null);

        var tarih = sonKullanmaTarihi.Value.Date;

        // Bugun dolan urun hala satilabilir; sinir dun.
        if (tarih < bugun.Date)
            return (false, "Son kullanma tarihi geçmiş ürün mal kabul edilemez.");

        if (tarih > bugun.Date.AddYears(EnFazlaRafOmruYil))
            return (false, $"Son kullanma tarihi {EnFazlaRafOmruYil} yıldan uzak olamaz. " +
                           "Girdiğiniz tarihi kontrol edin.");

        return (true, null);
    }

    public static (bool Gecerli, string? Hata) LotGecerliMi(string? lotNo)
    {
        if (string.IsNullOrWhiteSpace(lotNo)) return (true, null);

        return lotNo.Trim().Length > LotEnFazlaUzunluk
            ? (false, $"Lot numarası en fazla {LotEnFazlaUzunluk} karakter olabilir.")
            : (true, null);
    }
}
