using System.Data;
using Dapper;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>Son kullanma tarihi yaklasan ve gecmis parti sorgulari.</summary>
public sealed class SonKullanmaRepository
{
    private readonly IDbConnectionFactory _factory;

    public SonKullanmaRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Suresi gecmis olanlar da listeye girer: alt sinir yok, yalnizca
    /// ust sinir var. Ekranin isi zaten "cekilmesi gerekenler"i
    /// gostermek; suresi gecmisi disarida biraksaydik is listesi asil
    /// aciliyeti kacirirdi.
    ///
    /// KalanMiktar > 0 sarti kritik: tuketilmis ya da zayi'ye alinmis
    /// partiler listede kalmamali, aksi halde kullanici ayni isi
    /// tekrar tekrar yapmaya calisir.
    /// </summary>
    private const string SqlListe = @"
SELECT sp.Id            AS StokPartiId,
       u.Id             AS UrunId,
       u.Kod,
       u.Ad,
       u.Birim,
       d.Id             AS DepoId,
       d.Ad             AS DepoAd,
       sp.KalanMiktar,
       sp.BirimMaliyet,
       sp.SonKullanmaTarihi,
       sp.LotNo,
       t.Unvan          AS TedarikciUnvan,
       DATEDIFF(DAY, @bugun, sp.SonKullanmaTarihi) AS KalanGun
FROM StokParti sp
JOIN Urun u ON u.Id = sp.UrunId
JOIN Depo d ON d.Id = sp.DepoId
LEFT JOIN Tedarikci t ON t.Id = sp.TedarikciId
WHERE sp.KalanMiktar > 0
  AND sp.SonKullanmaTarihi IS NOT NULL
  AND sp.SonKullanmaTarihi <= DATEADD(DAY, @gunSayisi, @bugun)
  AND (@depoId IS NULL OR sp.DepoId = @depoId)
ORDER BY sp.SonKullanmaTarihi, u.Ad;";

    /// <summary>
    /// Zayi yazilmadan once partinin son hali. Transaction ICINDE ve
    /// UPDLOCK ile okunur: iki kullanici ayni partiyi ayni anda zayi'ye
    /// almaya kalkarsa ikincisi birincinin dusurdugu miktari gorur.
    /// </summary>
    private const string SqlParti = @"
SELECT sp.Id            AS StokPartiId,
       u.Id             AS UrunId,
       u.Kod,
       u.Ad,
       u.Birim,
       d.Id             AS DepoId,
       d.Ad             AS DepoAd,
       sp.KalanMiktar,
       sp.BirimMaliyet,
       sp.SonKullanmaTarihi,
       sp.LotNo,
       NULL             AS TedarikciUnvan,
       DATEDIFF(DAY, CAST(SYSUTCDATETIME() AS DATE), sp.SonKullanmaTarihi) AS KalanGun
FROM StokParti sp WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
JOIN Urun u ON u.Id = sp.UrunId
JOIN Depo d ON d.Id = sp.DepoId
WHERE sp.Id = @stokPartiId;";

    public async Task<IReadOnlyList<SonKullanmaSatirVm>> ListeleAsync(
        int gunSayisi, int? depoId, DateTime bugun, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var satirlar = await conn.QueryAsync<SonKullanmaSatirVm>(new CommandDefinition(
            SqlListe,
            new { gunSayisi, depoId, bugun = bugun.Date },
            cancellationToken: ct));
        return satirlar.AsList();
    }

    public async Task<SonKullanmaSatirVm?> PartiGetirAsync(
        IDbConnection conn, IDbTransaction tx, long stokPartiId, CancellationToken ct = default)
        => await conn.QuerySingleOrDefaultAsync<SonKullanmaSatirVm>(new CommandDefinition(
            SqlParti,
            new { stokPartiId },
            tx,
            cancellationToken: ct));
}
