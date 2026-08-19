using Dapper;

namespace MarketOtomasyon.Data.Repositories;

public class KullaniciRepository
{
    private readonly IDbConnectionFactory _factory;

    public KullaniciRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlRol = @"
SELECT Rol FROM Kullanici WHERE Id = @kullaniciId AND Aktif = 1;";

    /// <summary>Kullanici yoksa veya pasifse null doner.</summary>
    public async Task<byte?> RolGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<byte?>(
            new CommandDefinition(SqlRol, new { kullaniciId }, cancellationToken: ct));
    }
}
