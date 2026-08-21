using System.Data;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

public class SayimService
{
    private const byte KaynakSayim = 4;
    private const byte KaynakZayi = 5;
    private const byte YonCikis = 2;

    private readonly IDbConnectionFactory _factory;
    private readonly SayimRepository _sayimRepository;
    private readonly StokRepository _stokRepository;

    public SayimService(
        IDbConnectionFactory factory,
        SayimRepository sayimRepository,
        StokRepository stokRepository)
    {
        _factory = factory;
        _sayimRepository = sayimRepository;
        _stokRepository = stokRepository;
    }

    /// <summary>
    /// Sayim basligi, satirlari ve fark hareketlerini tek transaction icinde kaydeder.
    /// Formdaki sistem miktarina guvenilmez; bakiye kayit aninda yeniden okunur.
    /// </summary>
    public async Task<SayimKayitSonucu> SayimKaydetAsync(
        SayimEkranVm form, int kullaniciId, CancellationToken ct = default)
    {
        if (form.DepoId <= 0)
            return SayimKayitSonucu.Basarisiz("Depo seciniz.");

        var satirlar = form.Satirlar.Where(s => s.SayilanMiktar.HasValue).ToList();
        if (satirlar.Count == 0)
            return SayimKayitSonucu.Basarisiz("En az bir urunun sayilan miktarini girin.");

        if (satirlar.Any(s => s.UrunId <= 0))
            return SayimKayitSonucu.Basarisiz("Gecersiz urun satiri.");

        if (satirlar.Any(s => s.SayilanMiktar < 0))
            return SayimKayitSonucu.Basarisiz("Sayilan miktar sifirdan kucuk olamaz.");

        if (satirlar.Select(s => s.UrunId).Distinct().Count() != satirlar.Count)
            return SayimKayitSonucu.Basarisiz("Ayni urun sayimda birden fazla kez bulunamaz.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        if (!await _sayimRepository.DepoAktifMiAsync(conn, tx, form.DepoId, ct))
            return SayimKayitSonucu.Basarisiz("Secilen depo bulunamadi veya aktif degil.");

        foreach (var satir in satirlar)
        {
            if (!await _sayimRepository.UrunAktifMiAsync(conn, tx, satir.UrunId, ct))
                return SayimKayitSonucu.Basarisiz("Sayim satirlarindan biri bulunamadi veya aktif degil.");
        }

        var sayim = new Sayim
        {
            DepoId = form.DepoId,
            KullaniciId = kullaniciId,
            Aciklama = Temizle(form.Aciklama)
        };
        sayim.Id = await _sayimRepository.SayimEkleAsync(conn, tx, sayim, ct);

        var hareketSayisi = 0;
        foreach (var satir in satirlar)
        {
            var sistemMiktari = await _stokRepository.BakiyeAsync(
                conn, tx, satir.UrunId, form.DepoId, ct);
            var sayilanMiktar = satir.SayilanMiktar!.Value;
            var duzeltme = SayimKurallari.DuzeltmeHesapla(sistemMiktari, sayilanMiktar);

            await _sayimRepository.SayimSatirEkleAsync(conn, tx, new SayimSatir
            {
                SayimId = sayim.Id,
                UrunId = satir.UrunId,
                SistemMiktari = sistemMiktari,
                SayilanMiktar = sayilanMiktar,
                Fark = duzeltme.Fark
            }, ct);

            if (!duzeltme.HareketGerekli) continue;

            await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
            {
                UrunId = satir.UrunId,
                DepoId = form.DepoId,
                Yon = duzeltme.Yon!.Value,
                Miktar = duzeltme.Miktar,
                KaynakTip = KaynakSayim,
                KaynakId = sayim.Id,
                Aciklama = $"Sayim #{sayim.Id}: sistem {sistemMiktari:0.###}, sayilan {sayilanMiktar:0.###}"
            }, ct);
            hareketSayisi++;
        }

        tx.Commit();
        return new SayimKayitSonucu
        {
            Basarili = true,
            SayimId = sayim.Id,
            SayilanSatirSayisi = satirlar.Count,
            DuzeltmeHareketiSayisi = hareketSayisi
        };
    }

    /// <summary>Zayi kaydi ile stok cikisini ayni transaction icinde yazar.</summary>
    public async Task<ZayiKayitSonucu> ZayiKaydetAsync(
        int urunId,
        int depoId,
        decimal miktar,
        string? sebep,
        int kullaniciId,
        CancellationToken ct = default)
    {
        if (urunId <= 0) return ZayiKayitSonucu.Basarisiz("Urun seciniz veya barkod okutunuz.");
        if (depoId <= 0) return ZayiKayitSonucu.Basarisiz("Depo seciniz.");
        if (miktar <= 0) return ZayiKayitSonucu.Basarisiz("Miktar sifirdan buyuk olmalidir.");

        var temizSebep = Temizle(sebep);
        if (temizSebep is null) return ZayiKayitSonucu.Basarisiz("Zayi/fire sebebi zorunludur.");
        if (temizSebep.Length > 200) return ZayiKayitSonucu.Basarisiz("Sebep en fazla 200 karakter olabilir.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        if (!await _sayimRepository.DepoAktifMiAsync(conn, tx, depoId, ct))
            return ZayiKayitSonucu.Basarisiz("Secilen depo bulunamadi veya aktif degil.");
        if (!await _sayimRepository.UrunAktifMiAsync(conn, tx, urunId, ct))
            return ZayiKayitSonucu.Basarisiz("Urun bulunamadi veya aktif degil.");

        var bakiye = await _stokRepository.BakiyeAsync(conn, tx, urunId, depoId, ct);
        if (bakiye < miktar)
            return ZayiKayitSonucu.Basarisiz(
                $"Yetersiz stok. Mevcut bakiye {bakiye:0.###}, istenen zayi {miktar:0.###}.");

        var zayi = new Zayi
        {
            UrunId = urunId,
            DepoId = depoId,
            KullaniciId = kullaniciId,
            Miktar = miktar,
            Sebep = temizSebep
        };
        zayi.Id = await _sayimRepository.ZayiEkleAsync(conn, tx, zayi, ct);

        await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
        {
            UrunId = urunId,
            DepoId = depoId,
            Yon = YonCikis,
            Miktar = miktar,
            KaynakTip = KaynakZayi,
            KaynakId = zayi.Id,
            Aciklama = $"Zayi #{zayi.Id}: {temizSebep}"
        }, ct);

        tx.Commit();
        return new ZayiKayitSonucu
        {
            Basarili = true,
            ZayiId = zayi.Id,
            YeniBakiye = bakiye - miktar
        };
    }

    private static string? Temizle(string? metin) =>
        string.IsNullOrWhiteSpace(metin) ? null : metin.Trim();
}
