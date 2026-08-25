using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class PartiKurallariTests
{
    // Sabit bir "bugun": testler gece yarisini gecince kirilmasin.
    private static readonly DateTime Bugun = new(2026, 8, 25);

    /* ---------- Son kullanma tarihi ---------- */

    [Fact]
    public void Skt_ZorunluDegilkenBosBirakilabilir()
    {
        var (gecerli, hata) = PartiKurallari.SonKullanmaGecerliMi(null, zorunluMu: false, Bugun);

        Assert.True(gecerli);
        Assert.Null(hata);
    }

    /// <summary>
    /// Bos birakilan tarih partiyi FEFO sirasinin SONUNA atar; yani sut
    /// girisinde tarihi unutmak, sutu "en son satilacak" partiye cevirir.
    /// Bayrak tam da bunu engellemek icin var.
    /// </summary>
    [Fact]
    public void Skt_ZorunlukenBosBirakilamaz()
    {
        var (gecerli, hata) = PartiKurallari.SonKullanmaGecerliMi(null, zorunluMu: true, Bugun);

        Assert.False(gecerli);
        Assert.Contains("zorunludur", hata);
    }

    [Fact]
    public void Skt_BugunDolanUrunKabulEdilir()
    {
        // Bugun dolan urun hala satilabilir; sinir dundur.
        var (gecerli, _) = PartiKurallari.SonKullanmaGecerliMi(Bugun, zorunluMu: true, Bugun);

        Assert.True(gecerli);
    }

    [Fact]
    public void Skt_GecmisTarihReddedilir()
    {
        var (gecerli, hata) = PartiKurallari.SonKullanmaGecerliMi(
            Bugun.AddDays(-1), zorunluMu: true, Bugun);

        Assert.False(gecerli);
        Assert.Contains("geçmiş", hata);
    }

    /// <summary>2026 yerine 2226 yazmak kolaydir; o parti sirada en sona duser.</summary>
    [Fact]
    public void Skt_CokUzakTarihReddedilir()
    {
        var (gecerli, hata) = PartiKurallari.SonKullanmaGecerliMi(
            Bugun.AddYears(PartiKurallari.EnFazlaRafOmruYil + 1), zorunluMu: true, Bugun);

        Assert.False(gecerli);
        Assert.Contains("yıldan uzak", hata);
    }

    [Fact]
    public void Skt_SinirdakiTarihKabulEdilir()
    {
        var (gecerli, _) = PartiKurallari.SonKullanmaGecerliMi(
            Bugun.AddYears(PartiKurallari.EnFazlaRafOmruYil), zorunluMu: true, Bugun);

        Assert.True(gecerli);
    }

    /// <summary>Saat tasiyan bir deger gelirse gun bazinda karsilastirilmali.</summary>
    [Fact]
    public void Skt_SaatBilgisiKarsilastirmayiBozmaz()
    {
        var bugunGecVakit = Bugun.AddHours(23).AddMinutes(59);
        var (gecerli, _) = PartiKurallari.SonKullanmaGecerliMi(
            bugunGecVakit, zorunluMu: true, Bugun);

        Assert.True(gecerli);
    }

    /* ---------- Lot ---------- */

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Lot_BosKabulEdilir(string? lot)
    {
        var (gecerli, _) = PartiKurallari.LotGecerliMi(lot);
        Assert.True(gecerli);
    }

    [Fact]
    public void Lot_CokUzunReddedilir()
    {
        var (gecerli, hata) = PartiKurallari.LotGecerliMi(
            new string('L', PartiKurallari.LotEnFazlaUzunluk + 1));

        Assert.False(gecerli);
        Assert.Contains("en fazla", hata);
    }

    [Fact]
    public void Lot_SinirdakiUzunlukKabulEdilir()
    {
        var (gecerli, _) = PartiKurallari.LotGecerliMi(
            new string('L', PartiKurallari.LotEnFazlaUzunluk));

        Assert.True(gecerli);
    }

    /* ---------- Hesaplayici siraya sadik mi ----------
       FEFO siralamasi SQL'de yapiliyor, burada test edilemez. Ama
       hesaplayicinin kendisine verilen sirayi bozmadigini dogrulayabiliriz:
       FEFO'ya gecis bu sinifa dokunmadan calismali. */

    [Fact]
    public void Hesaplayici_VerilenSirayiBozmaz()
    {
        // SKT'si yakin olan basta: FEFO siralamasinin urettigi liste.
        var partiler = new List<StokPartiKalanVm>
        {
            new() { StokPartiId = 3, KalanMiktar = 5,  BirimMaliyet = 12m,
                    SonKullanmaTarihi = new DateTime(2026, 9, 1),  LotNo = "L-YAKIN" },
            new() { StokPartiId = 2, KalanMiktar = 5,  BirimMaliyet = 10m,
                    SonKullanmaTarihi = new DateTime(2027, 12, 31), LotNo = "L-UZAK" },
            new() { StokPartiId = 1, KalanMiktar = 5,  BirimMaliyet = 8m,
                    SonKullanmaTarihi = null, LotNo = "L-YOK" }
        };

        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, 12m);

        Assert.True(sonuc.Basarili);
        Assert.Equal(3, sonuc.Tuketimler.Count);

        // Once SKT'si en yakin parti tuketilmeli, tarihsiz olan en son.
        Assert.Equal(3, sonuc.Tuketimler[0].StokPartiId);
        Assert.Equal(2, sonuc.Tuketimler[1].StokPartiId);
        Assert.Equal(1, sonuc.Tuketimler[2].StokPartiId);

        Assert.Equal(5m, sonuc.Tuketimler[0].Miktar);
        Assert.Equal(5m, sonuc.Tuketimler[1].Miktar);
        Assert.Equal(2m, sonuc.Tuketimler[2].Miktar);
    }
}
