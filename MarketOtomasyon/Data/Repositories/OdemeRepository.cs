using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Fise baglanan odemeler. Bir fiste birden fazla odeme olabilir:
/// 40 TL nakit + 60 TL kart ayni fise iki satir olarak yazilir.
/// </summary>
public class OdemeRepository
{
    private readonly IDbConnectionFactory _factory;

    public OdemeRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlEkle = @"
INSERT INTO Odeme (FisId, Tip, Tutar, AlinanTutar, ParaUstu, OnayKodu)
OUTPUT INSERTED.Id
VALUES (@FisId, @Tip, @Tutar, @AlinanTutar, @ParaUstu, @OnayKodu);";

    private const string SqlFisOdemeleri = @"
SELECT Id, Tip, Tutar, AlinanTutar, ParaUstu, OnayKodu, Tarih
FROM Odeme
WHERE FisId = @fisId
ORDER BY Id;";

    // Odenen toplam: fise mahsup edilen tutarlar. Alinan nakit degil.
    private const string SqlOdenenToplam = @"
SELECT ISNULL(SUM(Tutar), 0) FROM Odeme WHERE FisId = @fisId;";

    private const string SqlSil = @"
DELETE FROM Odeme WHERE Id = @odemeId AND FisId = @fisId;";

    private const string SqlTumunuSil = @"
DELETE FROM Odeme WHERE FisId = @fisId;";

    public async Task<int> EkleAsync(IDbConnection conn, IDbTransaction tx, Odeme odeme, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlEkle, odeme, tx, cancellationToken: ct));

    public async Task<List<OdemeSatirVm>> FisOdemeleriAsync(int fisId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<OdemeSatirVm>(
            new CommandDefinition(SqlFisOdemeleri, new { fisId }, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<List<OdemeSatirVm>> FisOdemeleriAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
    {
        var liste = await conn.QueryAsync<OdemeSatirVm>(
            new CommandDefinition(SqlFisOdemeleri, new { fisId }, tx, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<decimal> OdenenToplamAsync(int fisId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(SqlOdenenToplam, new { fisId }, cancellationToken: ct));
    }

    public async Task<decimal> OdenenToplamAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(SqlOdenenToplam, new { fisId }, tx, cancellationToken: ct));

    public async Task<int> SilAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, int odemeId, CancellationToken ct = default)
        => await conn.ExecuteAsync(
            new CommandDefinition(SqlSil, new { fisId, odemeId }, tx, cancellationToken: ct));

    public async Task TumunuSilAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlTumunuSil, new { fisId }, tx, cancellationToken: ct));
}
