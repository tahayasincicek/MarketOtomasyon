using Dapper;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Ana ekrandaki gunluk ozet. Tek sorgu: ana ekran her acilista calisir,
/// dort ayri gidis-donus yapmanin anlami yok.
/// </summary>
public class OzetRepository
{
    private readonly IDbConnectionFactory _factory;

    public OzetRepository(IDbConnectionFactory factory) => _factory = factory;

    // Gun sinirlari disaridan UTC olarak verilir: Tarih kolonlari
    // SYSUTCDATETIME ile yaziliyor, "bugun" ise yerel gundur.
    private const string SqlGunlukOzet = @"
SELECT
    ISNULL(s.FisSayisi, 0)   AS FisSayisi,
    ISNULL(s.Ciro, 0)        AS Ciro,
    ISNULL(o.Nakit, 0)       AS Nakit,
    ISNULL(o.Kart, 0)        AS Kart,
    ISNULL(i.IadeSayisi, 0)  AS IadeSayisi,
    ISNULL(i.IadeToplam, 0)  AS IadeToplam,
    ISNULL(k.KritikUrun, 0)  AS KritikUrun
FROM (SELECT 1 AS x) sabit
OUTER APPLY (
    SELECT COUNT(*) AS FisSayisi, SUM(f.GenelToplam) AS Ciro
    FROM Fis f
    WHERE f.Durum = 2 AND f.Tarih >= @bas AND f.Tarih < @bit
) s
OUTER APPLY (
    SELECT SUM(CASE WHEN od.Tip = 1 THEN od.Tutar END) AS Nakit,
           SUM(CASE WHEN od.Tip = 2 THEN od.Tutar END) AS Kart
    FROM Odeme od
    JOIN Fis f2 ON f2.Id = od.FisId
    WHERE f2.Durum = 2 AND f2.Tarih >= @bas AND f2.Tarih < @bit
) o
OUTER APPLY (
    SELECT COUNT(*) AS IadeSayisi, SUM(ia.ToplamTutar) AS IadeToplam
    FROM Iade ia
    WHERE ia.Tarih >= @bas AND ia.Tarih < @bit
) i
-- Kritik olcutu StokRepository ile ayni olmali (bakiye <= min seviye);
-- ana ekran ile stok ekrani farkli sayi gosterirse guven biter.
OUTER APPLY (
    SELECT COUNT(*) AS KritikUrun
    FROM Urun u
    WHERE u.Aktif = 1
      AND ISNULL((SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
                  FROM StokHareket h WHERE h.UrunId = u.Id), 0) <= u.MinStokSeviyesi
) k;";

    public async Task<GunlukOzetVm> GunlukOzetAsync(
        DateTime baslangicUtc, DateTime bitisUtc, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<GunlukOzetVm>(new CommandDefinition(
            SqlGunlukOzet, new { bas = baslangicUtc, bit = bitisUtc }, cancellationToken: ct));
    }
}
