using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

public class BarkodRepository : IBarkodRepository
{
    private readonly IDbConnectionFactory _factory;

    public BarkodRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Kasanin sicak yolu: tek sorguda urun karti, koli carpani ve guncel fiyat.
    /// Barkod okutuldugunda bundan baska sorgu calistirilmaz.
    /// </summary>
    private const string SqlBarkodCoz = @"
SELECT u.Id AS UrunId, u.Kod, u.Ad, u.Birim, u.KdvOrani, u.Tartili,
       b.Barkod, b.Carpan, b.Tip AS BarkodTip,
       gf.Fiyat
FROM UrunBarkod b
JOIN Urun u ON u.Id = b.UrunId AND u.Aktif = 1
LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE b.Barkod = @barkod;";

    private const string SqlUrunBarkodlari = @"
SELECT Id, UrunId, Barkod, Carpan, Tip
FROM UrunBarkod
WHERE UrunId = @urunId
ORDER BY Tip, Barkod;";

    private const string SqlBarkodVarMi = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM UrunBarkod WHERE Barkod = @barkod) THEN 1 ELSE 0 END;";

    private const string SqlEkle = @"
INSERT INTO UrunBarkod (UrunId, Barkod, Carpan, Tip)
VALUES (@UrunId, @Barkod, @Carpan, @Tip);";

    private const string SqlSil = @"
DELETE FROM UrunBarkod WHERE Id = @id AND UrunId = @urunId;";

    public async Task<BarkodCozumVm?> BarkodCozAsync(string barkod, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<BarkodCozumVm>(
            new CommandDefinition(SqlBarkodCoz, new { barkod }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<UrunBarkod>> UrunBarkodlariAsync(int urunId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<UrunBarkod>(
            new CommandDefinition(SqlUrunBarkodlari, new { urunId }, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<bool> BarkodVarMiAsync(string barkod, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlBarkodVarMi, new { barkod }, cancellationToken: ct));
    }

    public async Task EkleAsync(UrunBarkod barkod, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(SqlEkle, barkod, cancellationToken: ct));
    }

    /// <summary>UrunId de sarta konur: baska urunun barkodu yanlislikla silinemesin.</summary>
    public async Task<int> SilAsync(int id, int urunId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(
            new CommandDefinition(SqlSil, new { id, urunId }, cancellationToken: ct));
    }
}
