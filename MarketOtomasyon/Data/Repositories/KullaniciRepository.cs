using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class KullaniciRepository
{
    private readonly IDbConnectionFactory _factory;

    public KullaniciRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAdSoyad = @"
SELECT AdSoyad FROM Kullanici WHERE Id = @kullaniciId;";

    private const string SqlRol = @"
SELECT Rol FROM Kullanici WHERE Id = @kullaniciId AND Aktif = 1;";

    private const string SqlKullaniciAdiyla = @"
SELECT Id, KullaniciAdi, AdSoyad, SifreHash, Rol, Aktif
FROM Kullanici
WHERE KullaniciAdi = @kullaniciAdi AND Aktif = 1;";

    private const string SqlSifreHashGuncelle = @"
UPDATE Kullanici SET SifreHash = @sifreHash
WHERE Id = @kullaniciId AND Aktif = 1;";

    /// <summary>Kullanici yoksa veya pasifse null doner.</summary>
    public async Task<byte?> RolGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<byte?>(
            new CommandDefinition(SqlRol, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<string?> AdSoyadGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(SqlAdSoyad, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<Kullanici?> KullaniciAdiylaGetirAsync(
        string kullaniciAdi,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Kullanici>(
            new CommandDefinition(SqlKullaniciAdiyla, new { kullaniciAdi }, cancellationToken: ct));
    }

    public async Task SifreHashGuncelleAsync(
        int kullaniciId,
        string sifreHash,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            SqlSifreHashGuncelle,
            new { kullaniciId, sifreHash },
            cancellationToken: ct));
    }
}
