using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Urun tablosuna erisim. Yalnizca SQL calistirir; is kurali UrunService icinde.
/// Yazma metotlari disaridan conn/tx alir cunku transaction yonetimi servisin isi.
/// </summary>
public class UrunRepository
{
    private readonly IDbConnectionFactory _factory;

    public UrunRepository(IDbConnectionFactory factory) => _factory = factory;

    // Iki sonuc kumesi tek gidiste: once toplam kayit, sonra sayfa satirlari.
    private const string SqlListele = @"
SELECT COUNT(*)
FROM Urun u
WHERE (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@kategoriId IS NULL OR u.KategoriId = @kategoriId)
  AND (@sadeceAktif = 0 OR u.Aktif = 1);

SELECT u.Id, u.Kod, u.Ad, k.Ad AS KategoriAd, u.Birim, u.KdvOrani, u.Tartili, u.Aktif,
       u.ResimYolu, gf.Fiyat AS GuncelFiyat
FROM Urun u
JOIN Kategori k ON k.Id = u.KategoriId
LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@kategoriId IS NULL OR u.KategoriId = @kategoriId)
  AND (@sadeceAktif = 0 OR u.Aktif = 1)
ORDER BY u.Ad
OFFSET @atla ROWS FETCH NEXT @adet ROWS ONLY;";

    // Kampanya formundaki urun secim listesi icin; sayfalama gerekmez.
    private const string SqlAktifListe = @"
SELECT Id, Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif, OlusturmaTarihi,
       ResimYolu, ResimKaynagi, ResimTarihi
FROM Urun
WHERE Aktif = 1
ORDER BY Ad;";

    private const string SqlGetir = @"
SELECT Id, Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif, OlusturmaTarihi,
       ResimYolu, ResimKaynagi, ResimTarihi
FROM Urun
WHERE Id = @id;";

    private const string SqlKodVarMi = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Urun WHERE Kod = @kod AND (@hariciId IS NULL OR Id <> @hariciId)
) THEN 1 ELSE 0 END;";

    private const string SqlEkle = @"
INSERT INTO Urun (Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif)
OUTPUT INSERTED.Id
VALUES (@Kod, @Ad, @KategoriId, @Birim, @KdvOrani, @MinStokSeviyesi, @Tartili, @Aktif);";

    private const string SqlGuncelle = @"
UPDATE Urun
SET Kod = @Kod, Ad = @Ad, KategoriId = @KategoriId, Birim = @Birim,
    KdvOrani = @KdvOrani, MinStokSeviyesi = @MinStokSeviyesi,
    Tartili = @Tartili, Aktif = @Aktif
WHERE Id = @Id;";

    public async Task<(IReadOnlyList<UrunListeSatirVm> Satirlar, int ToplamKayit)> ListeleAsync(
        string? arama, int? kategoriId, bool sadeceAktif, int sayfa, int sayfaBoyutu, CancellationToken ct = default)
    {
        var parametreler = new
        {
            arama = string.IsNullOrWhiteSpace(arama) ? null : arama.Trim(),
            kategoriId,
            sadeceAktif = sadeceAktif ? 1 : 0,
            atla = (sayfa - 1) * sayfaBoyutu,
            adet = sayfaBoyutu
        };

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var sonuc = await conn.QueryMultipleAsync(
            new CommandDefinition(SqlListele, parametreler, cancellationToken: ct));

        var toplam = await sonuc.ReadSingleAsync<int>();
        var satirlar = (await sonuc.ReadAsync<UrunListeSatirVm>()).AsList();
        return (satirlar, toplam);
    }

    public async Task<IReadOnlyList<Urun>> AktifleriGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Urun>(new CommandDefinition(SqlAktifListe, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<Urun?> GetirAsync(int id, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Urun>(
            new CommandDefinition(SqlGetir, new { id }, cancellationToken: ct));
    }

    /// <summary>Kod benzersizlik kontrolu. Duzenlemede kendi kaydini haric tutmak icin hariciId verilir.</summary>
    public async Task<bool> KodVarMiAsync(string kod, int? hariciId = null, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlKodVarMi, new { kod, hariciId }, cancellationToken: ct));
    }

    public async Task<int> EkleAsync(IDbConnection conn, IDbTransaction tx, Urun urun, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlEkle, urun, tx, cancellationToken: ct));

    public async Task GuncelleAsync(IDbConnection conn, IDbTransaction tx, Urun urun, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlGuncelle, urun, tx, cancellationToken: ct));
}
