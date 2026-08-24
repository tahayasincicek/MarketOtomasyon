using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>FIFO stok partileri, tüketimleri ve maliyet raporu sorguları.</summary>
public sealed class MaliyetRepository
{
    private readonly IDbConnectionFactory _factory;

    public MaliyetRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlPartiEkle = @"
INSERT INTO StokParti
    (UrunId, DepoId, StokHareketId, GirisMiktari, KalanMiktar, BirimMaliyet, Aciklama)
OUTPUT INSERTED.Id
VALUES
    (@UrunId, @DepoId, @StokHareketId, @GirisMiktari, @KalanMiktar, @BirimMaliyet, @Aciklama);";

    private const string SqlAcikPartiler = @"
SELECT Id AS StokPartiId, KalanMiktar, BirimMaliyet
FROM StokParti WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE UrunId = @urunId
  AND DepoId = @depoId
  AND KalanMiktar > 0
ORDER BY GirisTarihi, Id;";

    private const string SqlTuketimYaz = @"
UPDATE StokParti
SET KalanMiktar = KalanMiktar - @Miktar
WHERE Id = @StokPartiId AND KalanMiktar >= @Miktar;

IF @@ROWCOUNT <> 1
    THROW 51001, N'FIFO partisi başka bir işlemde değişti.', 1;

INSERT INTO StokPartiTuketim
    (StokPartiId, StokHareketId, FisSatirId, Miktar, BirimMaliyet)
VALUES
    (@StokPartiId, @StokHareketId, @FisSatirId, @Miktar, @BirimMaliyet);";

    private const string SqlOrtalamaAcikMaliyet = @"
SELECT CONVERT(DECIMAL(18,4),
       COALESCE(SUM(KalanMiktar * BirimMaliyet) / NULLIF(SUM(KalanMiktar), 0), 0))
FROM StokParti WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE UrunId = @urunId AND DepoId = @depoId AND KalanMiktar > 0;";

    private const string SqlFisSatirBirimMaliyeti = @"
SELECT CONVERT(DECIMAL(18,4),
       SUM(ToplamMaliyet) / NULLIF(SUM(Miktar), 0))
FROM StokPartiTuketim
WHERE FisSatirId = @fisSatirId;";

    private const string SqlKarMarjiRaporu = @"
WITH SatirMaliyeti AS (
    SELECT FisSatirId,
           SUM(Miktar) AS MaliyetliMiktar,
           SUM(ToplamMaliyet) AS ToplamMaliyet
    FROM StokPartiTuketim
    WHERE FisSatirId IS NOT NULL
    GROUP BY FisSatirId
)
SELECT u.Id AS UrunId,
       u.Kod AS UrunKod,
       u.Ad AS UrunAd,
       u.Birim,
       SUM(fs.Miktar - fs.IadeEdilenMiktar) AS SatilanMiktar,
       CONVERT(DECIMAL(18,4), SUM(
           fs.SatirToplam
           * (fs.Miktar - fs.IadeEdilenMiktar) / NULLIF(fs.Miktar, 0)
           / NULLIF(1 + fs.KdvOrani / 100.0, 0)
       )) AS NetSatis,
       CONVERT(DECIMAL(18,4), SUM(
           sm.ToplamMaliyet
           * (fs.Miktar - fs.IadeEdilenMiktar) / NULLIF(fs.Miktar, 0)
       )) AS SatisMaliyeti
FROM Fis f
JOIN FisSatir fs ON fs.FisId = f.Id
JOIN Urun u ON u.Id = fs.UrunId
JOIN SatirMaliyeti sm ON sm.FisSatirId = fs.Id
WHERE f.Durum = 2
  AND f.Tarih >= @baslangicUtc
  AND f.Tarih < @bitisUtc
  AND fs.Miktar > fs.IadeEdilenMiktar
GROUP BY u.Id, u.Kod, u.Ad, u.Birim
ORDER BY u.Ad;";

    public async Task<long> PartiEkleAsync(
        IDbConnection conn, IDbTransaction tx, StokParti parti, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(SqlPartiEkle, parti, tx, cancellationToken: ct));

    public async Task<IReadOnlyList<StokPartiKalanVm>> AcikPartileriGetirAsync(
        IDbConnection conn, IDbTransaction tx, int urunId, int depoId, CancellationToken ct = default)
    {
        var partiler = await conn.QueryAsync<StokPartiKalanVm>(
            new CommandDefinition(SqlAcikPartiler, new { urunId, depoId }, tx, cancellationToken: ct));
        return partiler.AsList();
    }

    public async Task TuketimYazAsync(
        IDbConnection conn,
        IDbTransaction tx,
        long stokHareketId,
        int? fisSatirId,
        FifoTuketimVm tuketim,
        CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlTuketimYaz,
            new
            {
                tuketim.StokPartiId,
                stokHareketId,
                fisSatirId,
                tuketim.Miktar,
                tuketim.BirimMaliyet
            },
            tx,
            cancellationToken: ct));

    public async Task<decimal> OrtalamaAcikMaliyetAsync(
        IDbConnection conn, IDbTransaction tx, int urunId, int depoId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
            SqlOrtalamaAcikMaliyet,
            new { urunId, depoId },
            tx,
            cancellationToken: ct));

    public async Task<decimal?> FisSatirBirimMaliyetiAsync(
        IDbConnection conn, IDbTransaction tx, int fisSatirId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            SqlFisSatirBirimMaliyeti,
            new { fisSatirId },
            tx,
            cancellationToken: ct));

    public async Task<IReadOnlyList<KarMarjiSatirVm>> KarMarjiRaporuAsync(
        DateTime baslangicUtc, DateTime bitisUtc, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var satirlar = await conn.QueryAsync<KarMarjiSatirVm>(new CommandDefinition(
            SqlKarMarjiRaporu,
            new { baslangicUtc, bitisUtc },
            cancellationToken: ct));
        return satirlar.AsList();
    }
}
