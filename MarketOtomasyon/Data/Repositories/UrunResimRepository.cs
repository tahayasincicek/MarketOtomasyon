using Dapper;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>Urun fotografi alanlarina erisim.</summary>
public class UrunResimRepository
{
    private readonly IDbConnectionFactory _factory;

    public UrunResimRepository(IDbConnectionFactory factory) => _factory = factory;

    public record EksikResimSatiri(int UrunId, string Kod, string Ad, string Barkod);

    // Yalnizca tekli barkodu olan aktif urunler. Koli barkodu ayri bir
    // ambalaji tarif eder, urunun kendi fotografini vermez.
    private const string SqlEksikler = @"
SELECT u.Id AS UrunId, u.Kod, u.Ad, b.Barkod
FROM Urun u
JOIN UrunBarkod b ON b.UrunId = u.Id AND b.Tip = 1
WHERE u.Aktif = 1
  AND u.ResimYolu IS NULL
  AND LEN(b.Barkod) = 13
ORDER BY u.Kod;";

    private const string SqlResimYaz = @"
UPDATE Urun
SET ResimYolu = @yol, ResimKaynagi = @kaynak, ResimTarihi = SYSUTCDATETIME()
WHERE Id = @urunId;";

    private const string SqlResimSil = @"
UPDATE Urun
SET ResimYolu = NULL, ResimKaynagi = NULL, ResimTarihi = NULL
WHERE Id = @urunId;";

    private const string SqlSayim = @"
SELECT
    (SELECT COUNT(*) FROM Urun WHERE Aktif = 1)                              AS Toplam,
    (SELECT COUNT(*) FROM Urun WHERE Aktif = 1 AND ResimYolu IS NOT NULL)    AS Resimli;";

    public async Task<List<EksikResimSatiri>> ResmiOlmayanlarAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<EksikResimSatiri>(
            new CommandDefinition(SqlEksikler, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task ResimYazAsync(int urunId, string yol, string kaynak, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            SqlResimYaz, new { urunId, yol, kaynak }, cancellationToken: ct));
    }

    public async Task ResimSilAsync(int urunId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            SqlResimSil, new { urunId }, cancellationToken: ct));
    }

    public async Task<(int Toplam, int Resimli)> SayimAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<(int, int)>(
            new CommandDefinition(SqlSayim, cancellationToken: ct));
    }
}
