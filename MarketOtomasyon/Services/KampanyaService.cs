using System.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace MarketOtomasyon.Services;

/// <summary>
/// Kampanyalari sepete uygular. Kasiyerin bir dugmeye basmasi gerekmez;
/// sepet her degistiginde yeniden hesaplanir, cunku bir satirin eklenmesi
/// baska bir satirin kampanyasini (orn. tutar baraji) degistirebilir.
/// </summary>
public class KampanyaService
{
    private readonly KampanyaRepository _kampanyaRepository;
    private readonly FisRepository _fisRepository;
    private readonly IMemoryCache _cache;
    private const string KasaKampanyaCacheKey = "kasa-kampanya-tanimlari";

    public KampanyaService(
        KampanyaRepository kampanyaRepository,
        FisRepository fisRepository,
        IMemoryCache cache)
    {
        _kampanyaRepository = kampanyaRepository;
        _fisRepository = fisRepository;
        _cache = cache;
    }

    /// <summary>
    /// Fisin satirlarina kampanya indirimlerini yazar. Acik transaction
    /// icinde calisir: sepet degisikligiyle ayni islemde gecmeli.
    /// </summary>
    public async Task UygulaAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
    {
        var satirlar = await _fisRepository.SatirlariGetirAsync(conn, tx, fisId, ct);
        if (satirlar.Count == 0) return;

        // Kampanya tanimlari ve urun-kategori eslesmeleri her barkodda
        // degismeyen verilerdir. Azure SQL'e her urun tiklamasinda iki ayri
        // kez gitmek yerine kisa sureli bellek kopyasi kullanilir.
        var veri = await _cache.GetOrCreateAsync(KasaKampanyaCacheKey, async giris =>
        {
            giris.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var kampanyalar = await _kampanyaRepository.GecerliTanimlariGetirAsync(ct);
            var kategoriler = await _kampanyaRepository.UrunKategorileriAsync(ct);
            return new KampanyaCacheVerisi(kampanyalar, kategoriler);
        });

        if (veri is null) return;

        var sonuc = KampanyaHesaplayici.Hesapla(
            satirlar, veri.Kampanyalar, veri.UrunKategorileri, DateTime.UtcNow);
        var indirimler = sonuc.SatirIndirimleri.ToDictionary(s => s.SatirId);

        foreach (var satir in satirlar)
        {
            var yeni = indirimler.GetValueOrDefault(satir.SatirId);

            // Elle indirimli satira dokunulmaz (hesaplayici da zaten atlar).
            var elleIndirimli = satir.IndirimTutari > 0 && satir.KampanyaId is null;
            if (elleIndirimli) continue;

            var yeniIndirim = yeni?.Indirim ?? 0;
            var yeniKampanyaId = yeni?.KampanyaId;

            // Degismediyse veritabanina gitme.
            if (satir.IndirimTutari == yeniIndirim && satir.KampanyaId == yeniKampanyaId) continue;

            await _fisRepository.SatirKampanyaGuncelleAsync(conn, tx, fisId, satir.SatirId,
                yeniIndirim, yeniKampanyaId,
                SepetHesaplayici.SatirToplamHesapla(satir.Miktar, satir.BirimFiyat, yeniIndirim), ct);
        }
    }

    /// <summary>Kampanya kaydedilince kasadaki kisa sureli kopyayi gecersiz kilar.</summary>
    public void OnbellegiTemizle() => _cache.Remove(KasaKampanyaCacheKey);

    private sealed record KampanyaCacheVerisi(
        List<KampanyaTanimVm> Kampanyalar,
        Dictionary<int, int> UrunKategorileri);
}
