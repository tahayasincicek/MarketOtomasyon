using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Satis ayarlarinin varsayilanlari. Bunlar isletmenin para/stok
/// davranisini belirledigi icin yanlislikla degismedigi test edilir.
/// </summary>
public class SatisAyarlariTests
{
    [Fact]
    public void Varsayilan_StokAsimiEngellenir()
    {
        var ayarlar = new SatisAyarlari();

        // Guvenli taraf: ayar acilmadikca bakiyeyi asan satis gecmez.
        Assert.False(ayarlar.NegatifStogaIzinVer);
    }

    [Fact]
    public void Varsayilan_SatisDeposuMarketRafi()
    {
        Assert.Equal("MRK", new SatisAyarlari().DepoKodu);
    }
}
