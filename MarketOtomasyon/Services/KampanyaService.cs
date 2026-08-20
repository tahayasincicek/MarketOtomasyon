using System.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

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

    public KampanyaService(KampanyaRepository kampanyaRepository, FisRepository fisRepository)
    {
        _kampanyaRepository = kampanyaRepository;
        _fisRepository = fisRepository;
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

        var kampanyalar = await _kampanyaRepository.GecerliTanimlariGetirAsync(ct);
        var urunKategorileri = await _kampanyaRepository.UrunKategorileriAsync(ct);

        var sonuc = KampanyaHesaplayici.Hesapla(satirlar, kampanyalar, urunKategorileri, DateTime.UtcNow);
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
}
