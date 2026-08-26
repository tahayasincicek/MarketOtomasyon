using MarketOtomasyon.Web;

namespace MarketOtomasyon.Tests;

/// <summary>
/// Ters vekil ayarlarinin ayristirilmasi.
///
/// Bu testlerin korudugu sey: gecersiz bir adres sessizce "guvenilir"
/// listesine girmemeli ve gecerli bir ag sessizce atlanmamali. Ilki
/// guvenlik acigi, ikincisi "ayar acik ama calismiyor" seklinde
/// gorunen bir yonlendirme dongusu demek.
/// </summary>
public class TersProxyAyarlariTests
{
    private static TersProxyAyarlari Ayar(string[]? proxyler = null, string[]? aglar = null)
        => new()
        {
            Etkin = true,
            GuvenilenProxyler = proxyler ?? [],
            GuvenilenAglar = aglar ?? []
        };

    /* ---------- Kaynak belirtilmemis durumu ---------- */

    [Fact]
    public void KaynakBelirtilmemis_IkisiDeBosken()
        => Assert.True(Ayar().KaynakBelirtilmemis);

    [Fact]
    public void KaynakBelirtilmis_TekProxyYeterli()
        => Assert.False(Ayar(proxyler: ["10.0.0.5"]).KaynakBelirtilmemis);

    [Fact]
    public void KaynakBelirtilmis_TekAgYeterli()
        => Assert.False(Ayar(aglar: ["10.0.0.0/8"]).KaynakBelirtilmemis);

    [Fact]
    public void EtkinAmaKaynakYoksa_DogrulamaReddeder()
    {
        var hatalar = Ayar().DogrulamaHatalari();

        Assert.Single(hatalar);
    }

    [Fact]
    public void TumProxylerAcikcaSecilirse_KaynakOlmadanGecerli()
    {
        var ayar = Ayar();
        ayar.TumProxylereGuven = true;

        Assert.Empty(ayar.DogrulamaHatalari());
    }

    [Fact]
    public void TumProxylerVeAdresListesi_BirlikteReddedilir()
    {
        var ayar = Ayar(proxyler: ["10.0.0.5"]);
        ayar.TumProxylereGuven = true;

        Assert.Single(ayar.DogrulamaHatalari());
    }

    /* ---------- Proxy adresleri ---------- */

    [Fact]
    public void Proxy_GecerliAdreslerCozulur()
    {
        var (gecerli, gecersiz) = Ayar(proxyler: ["10.0.0.5", "192.168.1.1", "::1"]).ProxyleriCoz();

        Assert.Equal(3, gecerli.Count);
        Assert.Empty(gecersiz);
    }

    [Fact]
    public void Proxy_BosluklarKirpilir()
    {
        var (gecerli, gecersiz) = Ayar(proxyler: ["  10.0.0.5  "]).ProxyleriCoz();

        Assert.Single(gecerli);
        Assert.Empty(gecersiz);
    }

    [Fact]
    public void Proxy_GecersizAdresAyriListeyeDuser()
    {
        // Sessizce atlanmamali: cagiran taraf uyari loglayabilmeli,
        // yoksa yanlis yazilmis bir adres fark edilmeden kalir.
        var (gecerli, gecersiz) = Ayar(proxyler: ["10.0.0.5", "bu-bir-adres-degil"]).ProxyleriCoz();

        Assert.Single(gecerli);
        Assert.Single(gecersiz);
        Assert.Equal("bu-bir-adres-degil", gecersiz[0]);
    }

    [Fact]
    public void Proxy_GecersizAdresYapilandirmayiReddettirir()
        => Assert.Contains(
            Ayar(proxyler: ["bu-bir-adres-degil"]).DogrulamaHatalari(),
            hata => hata.Contains("Geçersiz proxy adresi"));

    /* ---------- Ag (CIDR) ---------- */

    [Theory]
    [InlineData("10.0.0.0/8")]
    [InlineData("192.168.0.0/16")]
    [InlineData("172.16.0.0/12")]
    [InlineData("10.0.0.5/32")]
    public void Ag_GecerliCidrCozulur(string cidr)
    {
        var (gecerli, gecersiz) = Ayar(aglar: [cidr]).AglariCoz();

        Assert.Single(gecerli);
        Assert.Empty(gecersiz);
    }

    [Theory]
    [InlineData("10.0.0.0")]        // onek yok
    [InlineData("10.0.0.0/")]       // onek bos
    [InlineData("10.0.0.0/33")]     // IPv4 icin fazla
    [InlineData("10.0.0.0/-1")]     // negatif
    [InlineData("olmayan/8")]       // adres gecersiz
    [InlineData("10.0.0.0/8/16")]   // fazladan parca
    public void Ag_GecersizCidrAyriListeyeDuser(string cidr)
    {
        var (gecerli, gecersiz) = Ayar(aglar: [cidr]).AglariCoz();

        Assert.Empty(gecerli);
        Assert.Single(gecersiz);
    }

    [Fact]
    public void Ag_IPv6SinirlariDogru()
    {
        // IPv6'da /128 gecerli, IPv4'te degil. Ayni ust sinir
        // kullanilsaydi ya IPv6 aglari reddedilir ya da IPv4'te
        // anlamsiz onekler kabul edilirdi.
        var (gecerli, _) = Ayar(aglar: ["::/0", "fd00::/8", "::1/128"]).AglariCoz();

        Assert.Equal(3, gecerli.Count);

        var (bos, gecersiz) = Ayar(aglar: ["10.0.0.0/64"]).AglariCoz();

        Assert.Empty(bos);
        Assert.Single(gecersiz);
    }

    [Fact]
    public void Ag_GecerliVeGecersizKarisikOlabilir()
    {
        var (gecerli, gecersiz) = Ayar(aglar: ["10.0.0.0/8", "bozuk", "192.168.0.0/16"]).AglariCoz();

        Assert.Equal(2, gecerli.Count);
        Assert.Single(gecersiz);
    }

    /* ---------- Varsayilan ---------- */

    [Fact]
    public void Varsayilan_KAPALI()
    {
        // Ozellik varsayilan olarak kapali olmali: vekil yokken acik
        // olursa istemcinin gonderdigi X-Forwarded-Proto basligina
        // guvenilir ve uygulama baglantiyi sifreli saniyor.
        Assert.False(new TersProxyAyarlari().Etkin);
    }

    [Fact]
    public void KapaliykenEksikKaynak_HataDegildir()
        => Assert.Empty(new TersProxyAyarlari().DogrulamaHatalari());
}
