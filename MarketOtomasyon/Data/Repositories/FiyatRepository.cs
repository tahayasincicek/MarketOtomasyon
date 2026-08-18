using System.Data;
using Dapper;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// UrunFiyat tablosu. Fiyat degisince eski satir silinmez, BitisTarihi doldurulur.
/// </summary>
public class FiyatRepository
{
    private readonly IDbConnectionFactory _factory;

    public FiyatRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlGuncelFiyat = @"
SELECT Fiyat FROM vw_GuncelFiyat WHERE UrunId = @urunId;";

    private const string SqlAcikFiyatiKapat = @"
UPDATE UrunFiyat
SET BitisTarihi = SYSUTCDATETIME()
WHERE UrunId = @urunId AND BitisTarihi IS NULL;";

    private const string SqlFiyatEkle = @"
INSERT INTO UrunFiyat (UrunId, Fiyat)
VALUES (@urunId, @fiyat);";

    public async Task<decimal?> GuncelFiyatAsync(int urunId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<decimal?>(
            new CommandDefinition(SqlGuncelFiyat, new { urunId }, cancellationToken: ct));
    }

    public async Task AcikFiyatiKapatAsync(IDbConnection conn, IDbTransaction tx, int urunId, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlAcikFiyatiKapat, new { urunId }, tx, cancellationToken: ct));

    public async Task FiyatEkleAsync(IDbConnection conn, IDbTransaction tx, int urunId, decimal fiyat, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlFiyatEkle, new { urunId, fiyat }, tx, cancellationToken: ct));
}
