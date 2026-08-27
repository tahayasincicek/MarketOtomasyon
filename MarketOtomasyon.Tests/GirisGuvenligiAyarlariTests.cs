using MarketOtomasyon.Web;

namespace MarketOtomasyon.Tests;

public sealed class GirisGuvenligiAyarlariTests
{
    [Fact]
    public void VarsayilanAyarlarGecerlidir()
    {
        Assert.Empty(new GirisGuvenligiAyarlari().DogrulamaHatalari());
    }

    [Theory]
    [InlineData(0, 60, 6)]
    [InlineData(5, 0, 6)]
    [InlineData(5, 60, 61)]
    public void GecersizSinirlarReddedilir(int izin, int pencere, int dilim)
    {
        var ayarlar = new GirisGuvenligiAyarlari
        {
            IzinSayisi = izin,
            PencereSaniye = pencere,
            DilimSayisi = dilim
        };

        Assert.NotEmpty(ayarlar.DogrulamaHatalari());
    }
}
