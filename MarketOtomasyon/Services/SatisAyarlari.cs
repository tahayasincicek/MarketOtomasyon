namespace MarketOtomasyon.Services;

/// <summary>
/// appsettings.json > "Satis" bolumu. Isletmeye gore degisen kararlar
/// koda gomulmez; market sahibi stok kontrolunu gevsetmek isteyebilir.
/// </summary>
public class SatisAyarlari
{
    /// <summary>Satisin stogu dusurulecegi depo.</summary>
    public string DepoKodu { get; set; } = "MRK";

    /// <summary>
    /// Stok bakiyesini asan satisa izin verilsin mi?
    /// false: satis engellenir (varsayilan).
    /// true : satis gecer, yalnizca uyari doner. Sayim hatasi yuzunden
    ///        kaydi eksik gorunen urunun satisi durdurmamasi icin.
    /// </summary>
    public bool NegatifStogaIzinVer { get; set; }
}
