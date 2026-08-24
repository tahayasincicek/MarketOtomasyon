using Dapper;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Rapor sorgulari. Hepsi tek gidiste calisir: rapor ekrani bes ayri
/// baglanti acmasin.
///
/// Tarih kolonlari UTC yazilir; gun ve saat kirilimlari YEREL saate
/// cevrilerek uretilir (AT TIME ZONE). Cevrilmezse UTC+3'te sabahin
/// ilk uc saati bir onceki gune duser ve yogunluk grafigi 3 saat kayar.
/// </summary>
public sealed class RaporRepository
{
    private readonly IDbConnectionFactory _factory;

    public RaporRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>Sunucunun saat dilimi degisse de rapor yerel saate gore ciksin.</summary>
    private const string YerelTarih =
        "(f.Tarih AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time')";

    private const string SqlRaporlar = $@"
/* ---------- 1) Gun bazinda ciro ----------
   Iade tutari o iadenin YAPILDIGI gune yazilir; satisin gunune degil.
   Kasadan para o gun cikar. */
WITH GunlukSatis AS (
    SELECT CAST({YerelTarih} AS DATE) AS Gun,
           COUNT(*)                   AS FisSayisi,
           SUM(f.GenelToplam)         AS Ciro
    FROM Fis f
    WHERE f.Durum = 2 AND f.Tarih >= @bas AND f.Tarih < @bit
    GROUP BY CAST({YerelTarih} AS DATE)
),
GunlukIade AS (
    SELECT CAST((i.Tarih AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') AS DATE) AS Gun,
           SUM(i.ToplamTutar) AS IadeToplam
    FROM Iade i
    WHERE i.Tarih >= @bas AND i.Tarih < @bit
    GROUP BY CAST((i.Tarih AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') AS DATE)
)
SELECT s.Gun,
       s.FisSayisi,
       s.Ciro,
       s.Ciro - ISNULL(i.IadeToplam, 0) AS NetCiro
FROM GunlukSatis s
LEFT JOIN GunlukIade i ON i.Gun = s.Gun
ORDER BY s.Gun;

/* ---------- 2) En cok satan urunler ----------
   Miktara gore degil CIRO'ya gore siralanir: 100 paket sakiz,
   3 kilo kiymadan daha az para getirir.

   Iade edilen miktar dusulur (FisSatir.IadeEdilenMiktar), yoksa iade
   edilmis urun hala ""en cok satan"" gorunur.

   SUM(...) OVER() pencere fonksiyonu: her satirin toplam cirodaki
   payini ayri bir sorgu acmadan verir. */
WITH SatirNet AS (
    SELECT fs.UrunId,
           (fs.Miktar - fs.IadeEdilenMiktar) AS NetMiktar,
           CASE WHEN fs.Miktar = 0 THEN 0
                ELSE fs.SatirToplam* (fs.Miktar - fs.IadeEdilenMiktar) / fs.Miktar
           END AS NetTutar
    FROM FisSatir fs
    JOIN Fis f ON f.Id = fs.FisId
    WHERE f.Durum = 2 AND f.Tarih >= @bas AND f.Tarih<@bit
),
UrunToplam AS(
    SELECT sn.UrunId,
           SUM(sn.NetMiktar) AS SatilanMiktar,
           SUM(sn.NetTutar) AS Ciro
    FROM SatirNet sn
    GROUP BY sn.UrunId
    HAVING SUM(sn.NetMiktar) > 0
)
SELECT TOP(@enCokSatanAdet)
       t.UrunId,
       u.Kod AS UrunKod,
       u.Ad AS UrunAd,
       u.Birim,
       t.SatilanMiktar,
       t.Ciro,
       CONVERT(DECIMAL(18,2),
           100.0 * t.Ciro / NULLIF(SUM(t.Ciro) OVER (), 0)) AS CiroPayi
FROM UrunToplam t
JOIN Urun u ON u.Id = t.UrunId
ORDER BY t.Ciro DESC;

    /* ---------- 3) Odeme tipi dagilimi ----------
       Odeme.Tutar = fise mahsup edilen tutar, alinan nakit degil.
       Para ustu musteriye geri verildigi icin dogru rakam budur. */
    SELECT o.Tip,
           COUNT(*) AS Adet,
           SUM(o.Tutar) AS Tutar
    FROM Odeme o
    JOIN Fis f ON f.Id = o.FisId
    WHERE f.Durum = 2 AND f.Tarih >= @bas AND f.Tarih<@bit
    GROUP BY o.Tip
    ORDER BY SUM(o.Tutar) DESC;

    /* ---------- 4) Saat bazli yogunluk ----------
       Saat YEREL saate cevrilir. Cevrilmezse grafik 3 saat kayar ve
       personel planlamasi yanlis saate yapilir. */
    SELECT DATEPART(HOUR, { YerelTarih}) AS Saat,
           COUNT(*)                     AS FisSayisi,
           SUM(f.GenelToplam)           AS Ciro
FROM Fis f
WHERE f.Durum = 2 AND f.Tarih >= @bas AND f.Tarih<@bit
GROUP BY DATEPART(HOUR, { YerelTarih})
ORDER BY Saat;

/* ---------- 5) Kritik stok ----------
   Olcut StokRepository ve OzetRepository ile AYNI olmali
   (bakiye <= min seviye). Ucu farkli sayi gosterirse rapora guven biter.
   Tarih araligindan bagimsizdir: stok anlik durumdur. */
SELECT u.Kod, u.Ad, u.Birim, u.MinStokSeviyesi,
       ISNULL((SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
               FROM StokHareket h WHERE h.UrunId = u.Id), 0) AS Bakiye
FROM Urun u
WHERE u.Aktif = 1
  AND ISNULL((SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
              FROM StokHareket h WHERE h.UrunId = u.Id), 0) <= u.MinStokSeviyesi
ORDER BY(u.MinStokSeviyesi - ISNULL((SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
                                      FROM StokHareket h WHERE h.UrunId = u.Id), 0)) DESC;";

    public async Task<RaporVm> RaporlariGetirAsync(
        DateTime baslangicUtc,
        DateTime bitisUtc,
        int enCokSatanAdet = 10,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var sonuc = await conn.QueryMultipleAsync(new CommandDefinition(
            SqlRaporlar,
            new { bas = baslangicUtc, bit = bitisUtc, enCokSatanAdet },
            cancellationToken: ct));

        return new RaporVm
        {
            GunlukCiro = (await sonuc.ReadAsync<GunlukCiroSatirVm>()).AsList(),
            EnCokSatanlar = (await sonuc.ReadAsync<EnCokSatanSatirVm>()).AsList(),
            OdemeDagilimi = (await sonuc.ReadAsync<OdemeTipiSatirVm>()).AsList(),
            SaatYogunlugu = (await sonuc.ReadAsync<SaatYogunlukSatirVm>()).AsList(),
            KritikStoklar = (await sonuc.ReadAsync<KritikStokSatirVm>()).AsList()
        };
    }
}