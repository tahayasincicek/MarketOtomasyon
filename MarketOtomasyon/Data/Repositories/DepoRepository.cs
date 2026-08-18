using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class DepoRepository
{
    private readonly IDbConnectionFactory _factory;

    public DepoRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAktifler = @"
SELECT Id, Kod, Ad, Aktif FROM Depo WHERE Aktif = 1 ORDER BY Kod;";

    public async Task<IReadOnlyList<Depo>> AktifleriGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Depo>(new CommandDefinition(SqlAktifler, cancellationToken: ct));
        return liste.AsList();
    }
}
