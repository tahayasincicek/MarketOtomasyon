using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class KategoriRepository
{
    private readonly IDbConnectionFactory _factory;

    public KategoriRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAktifler = @"
SELECT Id, Kod, Ad, UstKategoriId, Aktif
FROM Kategori
WHERE Aktif = 1
ORDER BY Ad;";

    public async Task<IReadOnlyList<Kategori>> AktifleriGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Kategori>(new CommandDefinition(SqlAktifler, cancellationToken: ct));
        return liste.AsList();
    }
}
