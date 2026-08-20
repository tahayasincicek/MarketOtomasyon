using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class DepoRepository
{
    private readonly IDbConnectionFactory _factory;

    public DepoRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAktifler = @"
SELECT Id, Kod, Ad, Aktif FROM Depo WHERE Aktif = 1 ORDER BY Kod;";

    private const string SqlKodIleId = @"
SELECT TOP 1 Id FROM Depo WHERE Kod = @kod AND Aktif = 1;";

    public async Task<IReadOnlyList<Depo>> AktifleriGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Depo>(new CommandDefinition(SqlAktifler, cancellationToken: ct));
        return liste.AsList();
    }

    /// <summary>Kodu verilen aktif deponun Id'si; yoksa null.</summary>
    public async Task<int?> IdGetirAsync(string kod, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(SqlKodIleId, new { kod }, cancellationToken: ct));
    }
}
