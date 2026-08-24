using System.Data;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.Extensions.Options;

namespace MarketOtomasyon.Services;

/// <summary>
/// Satisi kapatir: stok hareketlerini yazar ve fisi Tamamlandi durumuna alir.
///
/// Kritik kural: fis durumu, satirlar, odemeler ve stok hareketleri TEK
/// transaction icinde islenir. Stok kontrolu takilirsa o ana kadar yazilan
/// odeme de geri alinir; yarim satis diye bir sey olusmaz.
/// </summary>
public class SatisService
{
    private const byte DurumBeklemede = 1;
    private const byte DurumTamamlandi = 2;
    private const byte DurumIptal = 9;

    private const byte YonCikis = 2;
    private const byte KaynakSatis = 1;

    private readonly IDbConnectionFactory _factory;
    private readonly FisRepository _fisRepository;
    private readonly OdemeRepository _odemeRepository;
    private readonly StokRepository _stokRepository;
    private readonly DepoRepository _depoRepository;
    private readonly MaliyetService _maliyetService;
    private readonly SatisAyarlari _ayarlar;

    public SatisService(
        IDbConnectionFactory factory,
        FisRepository fisRepository,
        OdemeRepository odemeRepository,
        StokRepository stokRepository,
        DepoRepository depoRepository,
        MaliyetService maliyetService,
        IOptions<SatisAyarlari> ayarlar)
    {
        _factory = factory;
        _fisRepository = fisRepository;
        _odemeRepository = odemeRepository;
        _stokRepository = stokRepository;
        _depoRepository = depoRepository;
        _maliyetService = maliyetService;
        _ayarlar = ayarlar.Value;
    }

    /// <summary>
    /// Odemesi tamamlanmis bir fisi kapatir. Kendi transaction'ini acar;
    /// odeme akisinin disindan (orn. yarim kalmis bir fisi kapatmak icin)
    /// cagrilabilir.
    /// </summary>
    public async Task<SatisSonucVm> TamamlaAsync(int fisId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.GetirAsync(fisId, ct);
        if (fis is null) return SatisSonucVm.Basarisiz("Fiş bulunamadı.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var sonuc = await TamamlaAsync(conn, tx, fis, ct);

        if (sonuc.Basarili) tx.Commit(); else tx.Rollback();
        return sonuc;
    }

    /// <summary>
    /// Satis kapatmanin cekirdegi. Acik bir transaction icinde calisir;
    /// odeme alinirken son odemeyle ayni transaction'da cagrilir ki
    /// odeme ve stok hareketi birlikte gecsin ya da birlikte geri alinsin.
    /// Cagiran commit/rollback'ten sorumludur.
    /// </summary>
    public async Task<SatisSonucVm> TamamlaAsync(
        IDbConnection conn, IDbTransaction tx, Fis fis, CancellationToken ct = default)
    {
        if (fis.Durum != DurumBeklemede)
            return SatisSonucVm.Basarisiz("Fiş zaten kapanmış veya iptal edilmiş.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(conn, tx, fis.Id, ct);
        if (satirlar.Count == 0)
            return SatisSonucVm.Basarisiz("Boş fiş tamamlanamaz.");

        var odenen = await _odemeRepository.OdenenToplamAsync(conn, tx, fis.Id, ct);
        if (odenen < fis.GenelToplam)
            return SatisSonucVm.Basarisiz(
                $"Ödeme tamamlanmadı. Kalan: {OdemeHesaplayici.KalanHesapla(fis.GenelToplam, odenen):0.00}");

        var depoId = await _depoRepository.IdGetirAsync(_ayarlar.DepoKodu, ct);
        if (depoId is null)
            return SatisSonucVm.Basarisiz($"Satış deposu bulunamadı: {_ayarlar.DepoKodu}");

        // Once tum satirlarin stogu kontrol edilir; yarisini yazip ortada
        // birakmamak icin hareketler ancak kontrol bittikten sonra islenir.
        var uyarilar = new List<string>();
        foreach (var satir in satirlar)
        {
            var bakiye = await _stokRepository.BakiyeAsync(conn, tx, satir.UrunId, depoId.Value, ct);
            if (bakiye >= satir.Miktar) continue;

            uyarilar.Add($"{satir.Ad}: stok {bakiye:0.###}, satılan {satir.Miktar:0.###}");
        }

        if (uyarilar.Count > 0 && !_ayarlar.NegatifStogaIzinVer)
            return SatisSonucVm.Basarisiz("Stok yetersiz: " + string.Join("; ", uyarilar), uyarilar);

        foreach (var satir in satirlar)
        {
            var hareketId = await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
            {
                UrunId = satir.UrunId,
                DepoId = depoId.Value,
                Yon = YonCikis,
                Miktar = satir.Miktar,
                KaynakTip = KaynakSatis,
                KaynakId = fis.Id,
                Aciklama = $"Satış {fis.FisNo}"
            }, ct);

            var maliyetSonucu = await _maliyetService.FifoTuketAsync(
                conn,
                tx,
                satir.UrunId,
                depoId.Value,
                hareketId,
                satir.SatirId,
                satir.Miktar,
                ct);

            if (!maliyetSonucu.Basarili)
            {
                var maliyetHatasi = $"{satir.Ad}: {maliyetSonucu.Hata}";
                if (!_ayarlar.NegatifStogaIzinVer)
                    return SatisSonucVm.Basarisiz(maliyetHatasi);

                uyarilar.Add(maliyetHatasi);
            }
        }

        await _fisRepository.DurumGuncelleAsync(conn, tx, fis.Id, DurumTamamlandi, ct);

        return new SatisSonucVm
        {
            Basarili = true,
            FisId = fis.Id,
            FisNo = fis.FisNo,
            Uyarilar = uyarilar
        };
    }

    // ---------- Askiya alma / geri cagirma ----------

    /// <summary>
    /// Kasadaki acik sepeti bir kenara alir; kasiyer sonraki musteriye gecebilir.
    /// Askidaki fis hala Beklemede'dir, stogu etkilemez.
    /// </summary>
    public async Task<(bool Basarili, string? Hata)> AskiyaAlAsync(int vardiyaId, CancellationToken ct = default)
    {
        var fis = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        if (fis is null) return (false, "Askıya alınacak açık sepet yok.");

        var satirlar = await _fisRepository.SatirlariGetirAsync(fis.Id, ct);
        if (satirlar.Count == 0) return (false, "Boş sepet askıya alınmaz.");

        // Odeme baslamissa askiya alinamaz; alinan para ortada kalirdi.
        var odenen = await _odemeRepository.OdenenToplamAsync(fis.Id, ct);
        if (odenen > 0)
            return (false, "Ödemesi başlamış fiş askıya alınamaz. Önce ödemeyi iptal edin.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _fisRepository.AskidaGuncelleAsync(conn, tx, fis.Id, true, ct);
        tx.Commit();

        return (true, null);
    }

    /// <summary>
    /// Askidaki fisi kasaya geri getirir. Kasada dolu bir sepet varsa o da
    /// askiya alinir; bos ise iptal edilir ki kullanilmayan fis birikmesin.
    /// </summary>
    public async Task<(bool Basarili, string? Hata)> GeriCagirAsync(
        int vardiyaId, int fisId, CancellationToken ct = default)
    {
        var hedef = await _fisRepository.GetirAsync(fisId, ct);
        if (hedef is null || hedef.Durum != DurumBeklemede)
            return (false, "Fiş bulunamadı veya artık beklemede değil.");

        if (hedef.VardiyaId != vardiyaId)
            return (false, "Fiş başka bir vardiyaya ait.");

        var mevcut = await _fisRepository.AcikFisGetirAsync(vardiyaId, ct);
        var mevcutSatirSayisi = mevcut is null
            ? 0
            : (await _fisRepository.SatirlariGetirAsync(mevcut.Id, ct)).Count;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        if (mevcut is not null && mevcut.Id != fisId)
        {
            if (mevcutSatirSayisi > 0)
                await _fisRepository.AskidaGuncelleAsync(conn, tx, mevcut.Id, true, ct);
            else
                await _fisRepository.DurumGuncelleAsync(conn, tx, mevcut.Id, DurumIptal, ct);
        }

        await _fisRepository.AskidaGuncelleAsync(conn, tx, fisId, false, ct);
        tx.Commit();

        return (true, null);
    }

    public async Task<List<BekleyenFisVm>> BekleyenleriGetirAsync(int vardiyaId, CancellationToken ct = default)
        => await _fisRepository.BekleyenleriGetirAsync(vardiyaId, ct);
}
