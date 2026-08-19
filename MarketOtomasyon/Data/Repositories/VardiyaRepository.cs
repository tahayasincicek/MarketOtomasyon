using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class VardiyaRepository
{
    private readonly IDbConnectionFactory _factory;

    public VardiyaRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAcikVardiya = @"
SELECT TOP 1 Id, KullaniciId, AcilisTarihi, AcilisTutari, KapanisTarihi,
       SayilanTutar, BeklenenTutar, Fark, Durum
FROM Vardiya
WHERE KullaniciId = @kullaniciId AND Durum = 1
ORDER BY Id DESC;";

    private const string SqlAc = @"
INSERT INTO Vardiya (KullaniciId, AcilisTutari, Durum)
OUTPUT INSERTED.Id
VALUES (@kullaniciId, @acilisTutari, 1);";

    public async Task<Vardiya?> AcikVardiyaGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Vardiya>(
            new CommandDefinition(SqlAcikVardiya, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<int> AcAsync(int kullaniciId, decimal acilisTutari, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlAc, new { kullaniciId, acilisTutari }, cancellationToken: ct));
    }
}
