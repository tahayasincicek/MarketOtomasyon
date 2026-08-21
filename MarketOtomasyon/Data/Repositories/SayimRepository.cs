using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

public class SayimRepository
{
    private readonly IDbConnectionFactory _factory;

    public SayimRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlUrunler = @"
SELECT u.Id AS UrunId, u.Kod, u.Ad, u.Birim,
       ISNULL(SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END), 0) AS SistemMiktari
FROM Urun u
LEFT JOIN StokHareket h ON h.UrunId = u.Id AND h.DepoId = @depoId
WHERE u.Aktif = 1
GROUP BY u.Id, u.Kod, u.Ad, u.Birim
ORDER BY u.Ad;";

    private const string SqlUrunAktifMi = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Urun WHERE Id = @urunId AND Aktif = 1)
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;";

    private const string SqlDepoAktifMi = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Depo WHERE Id = @depoId AND Aktif = 1)
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;";

    private const string SqlSayimEkle = @"
INSERT INTO Sayim (DepoId, KullaniciId, Aciklama)
OUTPUT INSERTED.Id
VALUES (@DepoId, @KullaniciId, @Aciklama);";

    private const string SqlSayimSatirEkle = @"
INSERT INTO SayimSatir (SayimId, UrunId, SistemMiktari, SayilanMiktar, Fark)
VALUES (@SayimId, @UrunId, @SistemMiktari, @SayilanMiktar, @Fark);";

    private const string SqlZayiEkle = @"
INSERT INTO Zayi (UrunId, DepoId, KullaniciId, Miktar, Sebep)
OUTPUT INSERTED.Id
VALUES (@UrunId, @DepoId, @KullaniciId, @Miktar, @Sebep);";

    public async Task<List<SayimGirisSatirVm>> SayimUrunleriniGetirAsync(
        int depoId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<SayimGirisSatirVm>(
            new CommandDefinition(SqlUrunler, new { depoId }, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<bool> UrunAktifMiAsync(
        IDbConnection conn, IDbTransaction tx, int urunId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlUrunAktifMi, new { urunId }, tx, cancellationToken: ct));

    public async Task<bool> DepoAktifMiAsync(
        IDbConnection conn, IDbTransaction tx, int depoId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlDepoAktifMi, new { depoId }, tx, cancellationToken: ct));

    public async Task<int> SayimEkleAsync(
        IDbConnection conn, IDbTransaction tx, Sayim sayim, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlSayimEkle, sayim, tx, cancellationToken: ct));

    public async Task SayimSatirEkleAsync(
        IDbConnection conn, IDbTransaction tx, SayimSatir satir, CancellationToken ct = default)
        => await conn.ExecuteAsync(
            new CommandDefinition(SqlSayimSatirEkle, satir, tx, cancellationToken: ct));

    public async Task<int> ZayiEkleAsync(
        IDbConnection conn, IDbTransaction tx, Zayi zayi, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlZayiEkle, zayi, tx, cancellationToken: ct));
}
