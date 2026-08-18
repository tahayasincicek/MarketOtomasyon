using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Services;

/// <summary>
/// Stok hareketlerini uretir. Stok her zaman hareket yazilarak degisir;
/// hicbir yerde bakiye kolonu guncellenmez.
/// </summary>
public class StokService
{
    /// <summary>KaynakTip: 3 = mal kabul.</summary>
    private const byte KaynakMalKabul = 3;
    private const byte YonGiris = 1;

    private readonly IDbConnectionFactory _factory;
    private readonly StokRepository _stokRepository;

    public StokService(IDbConnectionFactory factory, StokRepository stokRepository)
    {
        _factory = factory;
        _stokRepository = stokRepository;
    }

    /// <summary>Mal kabul: depoya giris hareketi yazar ve olusan yeni bakiyeyi doner.</summary>
    public async Task<decimal> MalKabulAsync(int urunId, int depoId, decimal miktar, string? aciklama, CancellationToken ct = default)
    {
        if (miktar <= 0)
            throw new ArgumentOutOfRangeException(nameof(miktar), "Mal kabul miktari sifirdan buyuk olmalidir.");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
        {
            UrunId = urunId,
            DepoId = depoId,
            Yon = YonGiris,
            Miktar = miktar,
            KaynakTip = KaynakMalKabul,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        }, ct);

        tx.Commit();

        return await _stokRepository.BakiyeAsync(urunId, depoId, ct);
    }
}
