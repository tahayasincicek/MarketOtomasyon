using System.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>Parti açma ve transaction içi FIFO tüketim işlemleri.</summary>
public sealed class MaliyetService
{
    private readonly MaliyetRepository _maliyetRepository;

    public MaliyetService(MaliyetRepository maliyetRepository)
        => _maliyetRepository = maliyetRepository;

    public async Task<long> PartiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        decimal miktar,
        decimal birimMaliyet,
        string? aciklama,
        CancellationToken ct = default)
    {
        if (miktar <= 0)
            throw new ArgumentOutOfRangeException(nameof(miktar));
        if (birimMaliyet < 0)
            throw new ArgumentOutOfRangeException(nameof(birimMaliyet));

        return await _maliyetRepository.PartiEkleAsync(conn, tx, new StokParti
        {
            UrunId = urunId,
            DepoId = depoId,
            StokHareketId = stokHareketId,
            GirisMiktari = miktar,
            KalanMiktar = miktar,
            BirimMaliyet = birimMaliyet,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Mal kabul" : aciklama.Trim()
        }, ct);
    }

    public async Task<FifoTuketimSonucu> FifoTuketAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        int? fisSatirId,
        decimal miktar,
        CancellationToken ct = default)
    {
        var partiler = await _maliyetRepository.AcikPartileriGetirAsync(
            conn, tx, urunId, depoId, ct);
        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, miktar);
        if (!sonuc.Basarili) return sonuc;

        foreach (var tuketim in sonuc.Tuketimler)
            await _maliyetRepository.TuketimYazAsync(
                conn, tx, stokHareketId, fisSatirId, tuketim, ct);

        return sonuc;
    }

    public async Task<long> DuzeltmePartisiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        decimal miktar,
        string aciklama,
        CancellationToken ct = default)
    {
        var ortalamaMaliyet = await _maliyetRepository.OrtalamaAcikMaliyetAsync(
            conn, tx, urunId, depoId, ct);

        return await PartiAcAsync(
            conn, tx, urunId, depoId, stokHareketId, miktar, ortalamaMaliyet, aciklama, ct);
    }

    public async Task<long> IadePartisiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        int fisSatirId,
        decimal miktar,
        decimal varsayilanBirimMaliyet,
        string aciklama,
        CancellationToken ct = default)
    {
        var satisMaliyeti = await _maliyetRepository.FisSatirBirimMaliyetiAsync(
            conn, tx, fisSatirId, ct);
        var birimMaliyet = satisMaliyeti ?? Math.Max(0, varsayilanBirimMaliyet);

        return await PartiAcAsync(
            conn, tx, urunId, depoId, stokHareketId, miktar, birimMaliyet, aciklama, ct);
    }
}
