using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
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


    private const string SqlIdIleGetir = @"
SELECT Id, KullaniciId, AcilisTarihi, AcilisTutari, KapanisTarihi,
       SayilanTutar, BeklenenTutar, Fark, Durum
FROM Vardiya
WHERE Id = @vardiyaId;";

    // Durum = 1 sarti: ayni vardiya iki kez kapatilamaz.
    private const string SqlKapat = @"
UPDATE Vardiya
SET KapanisTarihi = SYSUTCDATETIME(),
    SayilanTutar  = @sayilanTutar,
    BeklenenTutar = @beklenenTutar,
    Fark          = @fark,
    Durum         = 2
WHERE Id = @vardiyaId AND Durum = 1;";

    // Z raporu tek sorguda. Odeme.Tutar = fise mahsup edilen tutar,
    // alinan nakit degil; para ustu geri verildigi icin kasada kalan budur.
    private const string SqlZRapor = @"
SELECT
    v.Id                                AS VardiyaId,
    v.KullaniciId,
    v.AcilisTarihi,
    v.AcilisTutari,
    v.KapanisTarihi,
    v.SayilanTutar,
    v.BeklenenTutar                     AS KayitliBeklenen,
    v.Fark                              AS KayitliFark,
    v.Durum,
    k.AdSoyad                           AS KasiyerAdi,
    ISNULL(s.FisSayisi, 0)              AS FisSayisi,
    ISNULL(s.Ciro, 0)                   AS Ciro,
    ISNULL(s.ToplamIndirim, 0)          AS ToplamIndirim,
    ISNULL(s.ToplamKdv, 0)              AS ToplamKdv,
    ISNULL(o.Nakit, 0)                  AS NakitSatis,
    ISNULL(o.Kart, 0)                   AS KartSatis,
    ISNULL(o.Puan, 0)                   AS PuanSatis,
    ISNULL(i.IadeSayisi, 0)             AS IadeSayisi,
    ISNULL(i.IadeToplam, 0)             AS IadeToplam,
    ISNULL(i.NakitIade, 0)              AS NakitIade
FROM Vardiya v
JOIN Kullanici k ON k.Id = v.KullaniciId
OUTER APPLY (
    SELECT COUNT(*)              AS FisSayisi,
           SUM(f.GenelToplam)    AS Ciro,
           SUM(f.ToplamIndirim)  AS ToplamIndirim,
           SUM(f.ToplamKdv)      AS ToplamKdv
    FROM Fis f
    WHERE f.VardiyaId = v.Id AND f.Durum = 2
) s
OUTER APPLY (
    SELECT SUM(CASE WHEN o2.Tip = 1 THEN o2.Tutar END) AS Nakit,
           SUM(CASE WHEN o2.Tip = 2 THEN o2.Tutar END) AS Kart,
           SUM(CASE WHEN o2.Tip = 3 THEN o2.Tutar END) AS Puan
    FROM Odeme o2
    JOIN Fis f2 ON f2.Id = o2.FisId
    WHERE f2.VardiyaId = v.Id AND f2.Durum = 2
) o
OUTER APPLY (
    SELECT COUNT(*)                                               AS IadeSayisi,
           SUM(i2.ToplamTutar)                                    AS IadeToplam,
           SUM(CASE WHEN i2.OdemeTipi = 1 THEN i2.ToplamTutar END) AS NakitIade
    FROM Iade i2
    WHERE i2.VardiyaId = v.Id
) i
WHERE v.Id = @vardiyaId;";

    private const string SqlSonKapananlar = @"
SELECT TOP (@adet) Id, KullaniciId, AcilisTarihi, AcilisTutari, KapanisTarihi,
       SayilanTutar, BeklenenTutar, Fark, Durum
FROM Vardiya
WHERE Durum = 2
ORDER BY KapanisTarihi DESC;";

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

    public async Task<Vardiya?> GetirAsync(int vardiyaId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Vardiya>(
            new CommandDefinition(SqlIdIleGetir, new { vardiyaId }, cancellationToken: ct));
    }

    /// <summary>Etkilenen satir sayisi. 0 ise vardiya zaten kapaliydi.</summary>
    public async Task<int> KapatAsync(
        int vardiyaId, decimal sayilanTutar, decimal beklenenTutar, decimal fark,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            SqlKapat, new { vardiyaId, sayilanTutar, beklenenTutar, fark }, cancellationToken: ct));
    }

    public async Task<ZRaporVm?> ZRaporAsync(int vardiyaId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ZRaporVm>(
            new CommandDefinition(SqlZRapor, new { vardiyaId }, cancellationToken: ct));
    }

    public async Task<List<Vardiya>> SonKapananlarAsync(int adet = 20, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Vardiya>(
            new CommandDefinition(SqlSonKapananlar, new { adet }, cancellationToken: ct));
        return liste.AsList();
    }
}
