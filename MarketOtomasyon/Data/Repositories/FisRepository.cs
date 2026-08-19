using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Fis basligi ve satirlari. Beklemedeki fis (Durum 1) kasadaki acik sepettir;
/// odeme alininca Durum 2'ye gecer. Yalnizca SQL calistirir.
/// </summary>
public class FisRepository
{
    private const byte DurumBeklemede = 1;

    private readonly IDbConnectionFactory _factory;

    public FisRepository(IDbConnectionFactory factory) => _factory = factory;

    // Fis numarasi SEQUENCE'ten alinir: es zamanli iki satista ayni numara uretilmez.
    private const string SqlFisAc = @"
DECLARE @no INT = NEXT VALUE FOR FisNoSeq;
DECLARE @fisNo NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMdd') + '-' + FORMAT(@no, '00000');

INSERT INTO Fis (FisNo, VardiyaId, KullaniciId, Durum)
OUTPUT INSERTED.Id, INSERTED.FisNo
VALUES (@fisNo, @vardiyaId, @kullaniciId, 1);";

    private const string SqlAcikFis = @"
SELECT TOP 1 Id, FisNo, VardiyaId, KullaniciId, MusteriId, Tarih,
       AraToplam, ToplamIndirim, ToplamKdv, GenelToplam, Durum
FROM Fis
WHERE VardiyaId = @vardiyaId AND Durum = 1
ORDER BY Id DESC;";

    private const string SqlSatirlar = @"
SELECT fs.Id AS SatirId, fs.SatirNo, fs.UrunId, u.Kod, u.Ad, u.Birim,
       fs.Miktar, fs.BirimFiyat, fs.IndirimTutari, fs.KdvOrani, fs.SatirToplam
FROM FisSatir fs
JOIN Urun u ON u.Id = fs.UrunId
WHERE fs.FisId = @fisId
ORDER BY fs.SatirNo;";

    // Ayni urun sepette varsa yeni satir acilmaz, mevcut satirin miktari artar.
    private const string SqlAyniUrunSatiri = @"
SELECT TOP 1 Id FROM FisSatir
WHERE FisId = @fisId AND UrunId = @urunId AND IndirimTutari = 0;";

    private const string SqlSonrakiSatirNo = @"
SELECT ISNULL(MAX(SatirNo), 0) + 1 FROM FisSatir WHERE FisId = @fisId;";

    private const string SqlSatirEkle = @"
INSERT INTO FisSatir (FisId, SatirNo, UrunId, Miktar, BirimFiyat, IndirimTutari, KdvOrani, SatirToplam)
OUTPUT INSERTED.Id
VALUES (@FisId, @SatirNo, @UrunId, @Miktar, @BirimFiyat, @IndirimTutari, @KdvOrani, @SatirToplam);";

    private const string SqlSatirMiktarGuncelle = @"
UPDATE FisSatir
SET Miktar = @miktar, SatirToplam = @satirToplam
WHERE Id = @satirId AND FisId = @fisId;";

    private const string SqlSatirSil = @"
DELETE FROM FisSatir WHERE Id = @satirId AND FisId = @fisId;";

    private const string SqlToplamlariGuncelle = @"
UPDATE Fis
SET AraToplam = @araToplam, ToplamIndirim = @toplamIndirim,
    ToplamKdv = @toplamKdv, GenelToplam = @genelToplam
WHERE Id = @fisId;";

    private const string SqlFisIptal = @"
DELETE FROM FisSatir WHERE FisId = @fisId;
UPDATE Fis SET Durum = 9, AraToplam = 0, ToplamIndirim = 0, ToplamKdv = 0, GenelToplam = 0
WHERE Id = @fisId AND Durum = 1;";

    public async Task<(int FisId, string FisNo)> FisAcAsync(
        IDbConnection conn, IDbTransaction tx, int vardiyaId, int kullaniciId, CancellationToken ct = default)
    {
        var satir = await conn.QuerySingleAsync<(int Id, string FisNo)>(
            new CommandDefinition(SqlFisAc, new { vardiyaId, kullaniciId }, tx, cancellationToken: ct));

        return (satir.Id, satir.FisNo);
    }

    public async Task<Fis?> AcikFisGetirAsync(int vardiyaId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Fis>(
            new CommandDefinition(SqlAcikFis, new { vardiyaId }, cancellationToken: ct));
    }

    public async Task<List<SepetSatirVm>> SatirlariGetirAsync(int fisId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<SepetSatirVm>(
            new CommandDefinition(SqlSatirlar, new { fisId }, cancellationToken: ct));
        return liste.AsList();
    }

    /// <summary>
    /// Acik bir transaction icinden okumak icin. Ayri baglanti acan surum
    /// kullanilirsa, transaction'in kilitledigi satirlari beklemeye takilir.
    /// </summary>
    public async Task<List<SepetSatirVm>> SatirlariGetirAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
    {
        var liste = await conn.QueryAsync<SepetSatirVm>(
            new CommandDefinition(SqlSatirlar, new { fisId }, tx, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<int?> AyniUrunSatiriBulAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, int urunId, CancellationToken ct = default)
        => await conn.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(SqlAyniUrunSatiri, new { fisId, urunId }, tx, cancellationToken: ct));

    public async Task<int> SatirEkleAsync(
        IDbConnection conn, IDbTransaction tx, FisSatir satir, CancellationToken ct = default)
    {
        satir.SatirNo = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlSonrakiSatirNo, new { fisId = satir.FisId }, tx, cancellationToken: ct));

        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlSatirEkle, satir, tx, cancellationToken: ct));
    }

    public async Task<int> SatirMiktarGuncelleAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, int satirId, decimal miktar, decimal satirToplam,
        CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlSatirMiktarGuncelle, new { fisId, satirId, miktar, satirToplam }, tx, cancellationToken: ct));

    public async Task<int> SatirSilAsync(
        IDbConnection conn, IDbTransaction tx, int fisId, int satirId, CancellationToken ct = default)
        => await conn.ExecuteAsync(
            new CommandDefinition(SqlSatirSil, new { fisId, satirId }, tx, cancellationToken: ct));

    public async Task ToplamlariGuncelleAsync(
        IDbConnection conn, IDbTransaction tx, int fisId,
        decimal araToplam, decimal toplamIndirim, decimal toplamKdv, decimal genelToplam,
        CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlToplamlariGuncelle,
            new { fisId, araToplam, toplamIndirim, toplamKdv, genelToplam }, tx, cancellationToken: ct));

    public async Task IptalEtAsync(IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlFisIptal, new { fisId }, tx, cancellationToken: ct));
}
