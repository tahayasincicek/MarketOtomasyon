using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

public class IadeRepository
{
    private readonly IDbConnectionFactory _factory;

    public IadeRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlFis = @"
SELECT Id AS FisId, FisNo, Tarih, GenelToplam, Durum
FROM Fis
WHERE FisNo = @fisNo;";

    private const string SqlFisKilitle = @"
SELECT Id AS FisId, FisNo, Tarih, GenelToplam, Durum
FROM Fis WITH (UPDLOCK, HOLDLOCK)
WHERE FisNo = @fisNo;";

    private const string SqlSatirlar = @"
SELECT fs.Id AS FisSatirId, fs.SatirNo, fs.UrunId, u.Kod, u.Ad, u.Birim,
       fs.Miktar, fs.BirimFiyat, fs.IndirimTutari, fs.KdvOrani, fs.SatirToplam,
       fs.IadeEdilenMiktar,
       ISNULL((SELECT SUM(iads.Tutar) FROM IadeSatir iads WHERE iads.FisSatirId = fs.Id), 0)
           AS DahaOnceIadeTutari
FROM FisSatir fs
JOIN Urun u ON u.Id = fs.UrunId
JOIN Fis f ON f.Id = fs.FisId
WHERE f.FisNo = @fisNo
ORDER BY fs.SatirNo;";

    private const string SqlSatirlarKilitle = @"
SELECT fs.Id AS FisSatirId, fs.SatirNo, fs.UrunId, u.Kod, u.Ad, u.Birim,
       fs.Miktar, fs.BirimFiyat, fs.IndirimTutari, fs.KdvOrani, fs.SatirToplam,
       fs.IadeEdilenMiktar,
       ISNULL((SELECT SUM(iads.Tutar) FROM IadeSatir iads WHERE iads.FisSatirId = fs.Id), 0)
           AS DahaOnceIadeTutari
FROM FisSatir fs WITH (UPDLOCK, HOLDLOCK)
JOIN Urun u ON u.Id = fs.UrunId
JOIN Fis f ON f.Id = fs.FisId
WHERE f.FisNo = @fisNo
ORDER BY fs.SatirNo;";

    private const string SqlIadeEkle = @"
DECLARE @no INT = NEXT VALUE FOR IadeNoSeq;
DECLARE @iadeNo NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMdd') + '-I-' + FORMAT(@no, '00000');

INSERT INTO Iade (IadeNo, FisId, KullaniciId, ToplamTutar, OdemeTipi, Aciklama)
OUTPUT INSERTED.Id, INSERTED.IadeNo
VALUES (@iadeNo, @FisId, @KullaniciId, @ToplamTutar, @OdemeTipi, @Aciklama);";

    private const string SqlIadeSatirEkle = @"
INSERT INTO IadeSatir
    (IadeId, FisSatirId, UrunId, Miktar, BirimFiyat, IndirimTutari, KdvOrani, Tutar)
VALUES
    (@IadeId, @FisSatirId, @UrunId, @Miktar, @BirimFiyat, @IndirimTutari, @KdvOrani, @Tutar);";

    // Kosullu guncelleme ikinci/es zamanli iadenin satilan miktari asmasini engeller.
    private const string SqlIadeMiktariArtir = @"
UPDATE FisSatir
SET IadeEdilenMiktar = IadeEdilenMiktar + @miktar
WHERE Id = @fisSatirId
  AND IadeEdilenMiktar + @miktar <= Miktar;";

    public async Task<IadeFisVm?> FisGetirAsync(string fisNo, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await FisVeSatirlariOkuAsync(conn, null, SqlFis, SqlSatirlar, fisNo, ct);
    }

    public async Task<IadeFisVm?> IadeIcinFisGetirAsync(
        IDbConnection conn, IDbTransaction tx, string fisNo, CancellationToken ct = default)
        => await FisVeSatirlariOkuAsync(conn, tx, SqlFisKilitle, SqlSatirlarKilitle, fisNo, ct);

    private static async Task<IadeFisVm?> FisVeSatirlariOkuAsync(
        IDbConnection conn,
        IDbTransaction? tx,
        string fisSql,
        string satirSql,
        string fisNo,
        CancellationToken ct)
    {
        var fis = await conn.QuerySingleOrDefaultAsync<IadeFisVm>(
            new CommandDefinition(fisSql, new { fisNo }, tx, cancellationToken: ct));
        if (fis is null) return null;

        var satirlar = await conn.QueryAsync<IadeFisSatirVm>(
            new CommandDefinition(satirSql, new { fisNo }, tx, cancellationToken: ct));
        fis.Satirlar = satirlar.AsList();
        return fis;
    }

    public async Task<(int IadeId, string IadeNo)> EkleAsync(
        IDbConnection conn, IDbTransaction tx, Iade iade, CancellationToken ct = default)
    {
        var sonuc = await conn.QuerySingleAsync<(int Id, string IadeNo)>(
            new CommandDefinition(SqlIadeEkle, iade, tx, cancellationToken: ct));
        return (sonuc.Id, sonuc.IadeNo);
    }

    public async Task SatirEkleAsync(
        IDbConnection conn, IDbTransaction tx, IadeSatir satir, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlIadeSatirEkle, satir, tx, cancellationToken: ct));

    public async Task<int> IadeMiktariArtirAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int fisSatirId,
        decimal miktar,
        CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlIadeMiktariArtir, new { fisSatirId, miktar }, tx, cancellationToken: ct));
}
