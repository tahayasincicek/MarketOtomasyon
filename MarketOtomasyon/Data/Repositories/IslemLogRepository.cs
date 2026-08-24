using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

public sealed class IslemLogRepository
{
    private readonly IDbConnectionFactory _factory;

    public IslemLogRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlEkle = @"
INSERT INTO IslemLog
    (KullaniciId, IslemTipi, HedefTipi, HedefId, EskiDeger, YeniDeger, Aciklama)
VALUES
    (@KullaniciId, @IslemTipi, @HedefTipi, @HedefId, @EskiDeger, @YeniDeger, @Aciklama);";

    private const string SqlSonKayitlar = @"
SELECT TOP (@adet)
       l.Id, l.Tarih, k.KullaniciAdi, k.AdSoyad,
       l.IslemTipi, l.HedefTipi, l.HedefId,
       l.EskiDeger, l.YeniDeger, l.Aciklama
FROM IslemLog l
JOIN Kullanici k ON k.Id = l.KullaniciId
ORDER BY l.Tarih DESC, l.Id DESC;";

    public async Task EkleAsync(
        IDbConnection conn,
        IDbTransaction tx,
        IslemLog kayit,
        CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlEkle, kayit, tx, cancellationToken: ct));

    public async Task<IReadOnlyList<IslemLogSatirVm>> SonKayitlarAsync(
        int adet = 200,
        CancellationToken ct = default)
    {
        adet = Math.Clamp(adet, 1, 1000);
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var kayitlar = await conn.QueryAsync<IslemLogSatirVm>(
            new CommandDefinition(SqlSonKayitlar, new { adet }, cancellationToken: ct));
        return kayitlar.AsList();
    }
}
