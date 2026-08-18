using Dapper;
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Data.Repositories;

public class UrunRepository
{
    private readonly IDbConnectionFactory _factory;

    public UrunRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAktifUrunSayisi = @"
SELECT COUNT(*) FROM Urun WHERE Aktif = 1;";

    private const string SqlHepsi = @"
SELECT Id, Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif, OlusturmaTarihi
FROM Urun
WHERE Aktif = 1
ORDER BY Ad;";

    public async Task<int> AktifUrunSayisiAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlAktifUrunSayisi, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Urun>> HepsiniGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Urun>(new CommandDefinition(SqlHepsi, cancellationToken: ct));
        return liste.AsList();
    }
}
