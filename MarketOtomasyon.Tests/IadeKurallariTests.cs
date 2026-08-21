using MarketOtomasyon.Services;

namespace MarketOtomasyon.Tests;

public class IadeKurallariTests
{
    [Fact]
    public void Kabul_UcUrunluFistenBirUrunIadeEdilir_IkinciDenemeReddedilir()
    {
        var satirlar = new[]
        {
            new { Miktar = 1m, SatirToplam = 15m, IadeEdilen = 0m },
            new { Miktar = 1m, SatirToplam = 32.50m, IadeEdilen = 0m },
            new { Miktar = 1m, SatirToplam = 89m, IadeEdilen = 0m }
        };

        var secilen = satirlar[1];
        var ilk = IadeKurallari.MiktarDogrula(secilen.Miktar, secilen.IadeEdilen, 1m);
        var iadeTutari = IadeKurallari.TutarHesapla(
            secilen.Miktar, secilen.SatirToplam, secilen.IadeEdilen, 0m, 1m);

        var ikinci = IadeKurallari.MiktarDogrula(secilen.Miktar, 1m, 1m);

        Assert.True(ilk.Gecerli);
        Assert.Equal(32.50m, iadeTutari);
        Assert.False(ikinci.Gecerli);
        Assert.Contains("tamami daha once iade", ikinci.Hata);
        Assert.Equal(0m, satirlar[0].IadeEdilen);
        Assert.Equal(0m, satirlar[2].IadeEdilen);
    }

    [Fact]
    public void IadeTutari_GuncelUrunFiyatindanDegilSatisAnindakiTutardanHesaplanir()
    {
        const decimal satisAnindakiBirimFiyat = 24.90m;
        const decimal bugunkuUrunFiyati = 39.90m;

        var tutar = IadeKurallari.TutarHesapla(
            satilanMiktar: 2m,
            satisAnindakiSatirToplami: 2m * satisAnindakiBirimFiyat,
            iadeEdilenMiktar: 0m,
            dahaOnceIadeTutari: 0m,
            istenenMiktar: 1m);

        Assert.Equal(satisAnindakiBirimFiyat, tutar);
        Assert.NotEqual(bugunkuUrunFiyati, tutar);
    }

    [Fact]
    public void KismiIade_IndirimiOransalDagitir_SonParcaKurusArtiginiKapatir()
    {
        var ilk = IadeKurallari.TutarHesapla(3m, 10m, 0m, 0m, 1m);
        var ikinci = IadeKurallari.TutarHesapla(3m, 10m, 1m, ilk, 2m);

        Assert.Equal(3.33m, ilk);
        Assert.Equal(6.67m, ikinci);
        Assert.Equal(10m, ilk + ikinci);
    }

    [Fact]
    public void IadeMiktari_KalanMiktariAsamaz()
    {
        var sonuc = IadeKurallari.MiktarDogrula(3m, 2m, 1.001m);

        Assert.False(sonuc.Gecerli);
        Assert.Contains("kalan miktari", sonuc.Hata);
    }

    [Fact]
    public void IadeSuresi_SonGunDahilAcik_SonrasindaKapali()
    {
        var satis = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.False(IadeKurallari.SureDolduMu(satis, 30, satis.AddDays(30)));
        Assert.True(IadeKurallari.SureDolduMu(satis, 30, satis.AddDays(30).AddTicks(1)));
    }

    [Fact]
    public void IadeAyarlari_VarsayilanSureOtuzGun()
    {
        Assert.Equal(30, new IadeAyarlari().SureGun);
    }
}
