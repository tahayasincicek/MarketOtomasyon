using Microsoft.Extensions.Configuration;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Ayar dosyalarinin okunabilirligini korur.
///
/// Bu dosyalar derlemeye girmez; bozulduklarinda hata ancak uygulama
/// ayaga kalkarken ortaya cikar. Uretim ayarindaki bir yazim hatasini
/// deploy sirasinda ogrenmek gec.
/// </summary>
public class AyarDosyalariTests
{
    /// <summary>
    /// Ayar dosyalari .csproj tarafindan cikti altindaki "ayarlar"
    /// klasorune kopyalanir. Boylece test, calisma dizininden bagimsiz
    /// olarak onlari bulur.
    /// </summary>
    private static IConfigurationRoot Oku(string dosyaAdi) =>
        new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "ayarlar"))
            .AddJsonFile(dosyaAdi, optional: false)
            .Build();

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData("appsettings.Development.json.ornek")]
    public void AyarDosyasi_GecerliJsonVeOkunabilir(string dosyaAdi)
    {
        // Production ve ornek dosyalar aciklama satirlari iceriyor.
        // .NET'in JSON saglayicisi yorumlari atlar; bu test o davranisa
        // guvendigimizi sabitler.
        var ayar = Oku(dosyaAdi);

        Assert.NotNull(ayar);
    }

    [Fact]
    public void Uretim_BaglantiDizesiDosyada_BULUNMAMALI()
    {
        // Isin ozu: uretim sifresi kaynak koda girmemeli. Biri
        // kolaylik olsun diye dizeyi buraya yazarsa bu test duser.
        var ayar = Oku("appsettings.Production.json");

        Assert.True(
            string.IsNullOrEmpty(ayar.GetConnectionString("MarketDb")),
            "appsettings.Production.json içine bağlantı dizesi yazılmış. " +
            "Üretim bilgileri ConnectionStrings__MarketDb ortam değişkeninden gelmeli.");
    }

    [Fact]
    public void Temel_AyarDosyasindaBaglantiDizesiOlmamali()
    {
        // appsettings.json her ortamda yuklenir; buraya yazilan bir dize
        // uretimde de gecerli olur ve ortam degiskenini anlamsizlastirir.
        var ayar = Oku("appsettings.json");

        Assert.True(string.IsNullOrEmpty(ayar.GetConnectionString("MarketDb")));
    }

    [Fact]
    public void GelistirmeSablonu_BaglantiDizesiIceriyor()
    {
        /* Sablon test edilir, gercek Development dosyasi degil: o
           .gitignore'da oldugu icin temiz bir klonda ya da CI'da
           bulunmaz. Sablonun ise her zaman calisan bir ornek sunmasi
           gerekir - yeni gelen onu kopyalayip basliyor. */
        var ayar = Oku("appsettings.Development.json.ornek");

        Assert.False(string.IsNullOrWhiteSpace(ayar.GetConnectionString("MarketDb")));
    }
}
