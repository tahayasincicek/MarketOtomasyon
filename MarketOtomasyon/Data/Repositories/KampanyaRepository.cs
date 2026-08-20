using System.Data;
using Dapper;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Kampanya tanimlari. Bir kampanya uc tabloya yayildigi icin okuma
/// QueryMultiple ile tek gidiste yapilir; kasada her satir eklemede
/// kampanyalar yeniden okundugundan sorgu sayisi onemli.
/// </summary>
public class KampanyaRepository
{
    private readonly IDbConnectionFactory _factory;

    public KampanyaRepository(IDbConnectionFactory factory) => _factory = factory;

    // Tarihi gecmis ve pasif kampanyalar hic okunmaz.
    private const string SqlGecerliTanimlar = @"
SELECT Id, Kod, Ad, Oncelik, DigerleriyleBirlesir, Aktif, BaslangicTarihi, BitisTarihi
FROM Kampanya
WHERE Aktif = 1
  AND BaslangicTarihi <= SYSUTCDATETIME()
  AND (BitisTarihi IS NULL OR BitisTarihi >= SYSUTCDATETIME())
ORDER BY Oncelik, Id;

SELECT k.Id, k.KampanyaId, k.Tip, k.UrunId, k.KategoriId, k.MinMiktar, k.MinTutar
FROM KampanyaKosul k
JOIN Kampanya kmp ON kmp.Id = k.KampanyaId
WHERE kmp.Aktif = 1
  AND kmp.BaslangicTarihi <= SYSUTCDATETIME()
  AND (kmp.BitisTarihi IS NULL OR kmp.BitisTarihi >= SYSUTCDATETIME());

SELECT s.Id, s.KampanyaId, s.Tip, s.Yuzde, s.Tutar, s.OdenecekMiktar
FROM KampanyaSonuc s
JOIN Kampanya kmp ON kmp.Id = s.KampanyaId
WHERE kmp.Aktif = 1
  AND kmp.BaslangicTarihi <= SYSUTCDATETIME()
  AND (kmp.BitisTarihi IS NULL OR kmp.BitisTarihi >= SYSUTCDATETIME());";

    private const string SqlHepsi = @"
SELECT Id, Kod, Ad, Oncelik, DigerleriyleBirlesir, Aktif, BaslangicTarihi, BitisTarihi
FROM Kampanya
ORDER BY Aktif DESC, Oncelik, Ad;

SELECT Id, KampanyaId, Tip, UrunId, KategoriId, MinMiktar, MinTutar FROM KampanyaKosul;
SELECT Id, KampanyaId, Tip, Yuzde, Tutar, OdenecekMiktar FROM KampanyaSonuc;";

    private const string SqlTek = @"
SELECT Id, Kod, Ad, Oncelik, DigerleriyleBirlesir, Aktif, BaslangicTarihi, BitisTarihi
FROM Kampanya WHERE Id = @id;

SELECT Id, KampanyaId, Tip, UrunId, KategoriId, MinMiktar, MinTutar
FROM KampanyaKosul WHERE KampanyaId = @id;

SELECT Id, KampanyaId, Tip, Yuzde, Tutar, OdenecekMiktar
FROM KampanyaSonuc WHERE KampanyaId = @id;";

    // Kampanya kategoriye bakabildigi icin urun-kategori esleme tablosu gerekir.
    private const string SqlUrunKategorileri = @"
SELECT Id AS UrunId, KategoriId FROM Urun WHERE Aktif = 1;";

    private const string SqlKodVarMi = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Kampanya WHERE Kod = @kod AND (@hariciId IS NULL OR Id <> @hariciId)
) THEN 1 ELSE 0 END;";

    private const string SqlEkle = @"
INSERT INTO Kampanya (Kod, Ad, Oncelik, BaslangicTarihi, BitisTarihi, Aktif, DigerleriyleBirlesir)
OUTPUT INSERTED.Id
VALUES (@Kod, @Ad, @Oncelik, @BaslangicTarihi, @BitisTarihi, @Aktif, @DigerleriyleBirlesir);";

    private const string SqlGuncelle = @"
UPDATE Kampanya
SET Kod = @Kod, Ad = @Ad, Oncelik = @Oncelik, BaslangicTarihi = @BaslangicTarihi,
    BitisTarihi = @BitisTarihi, Aktif = @Aktif, DigerleriyleBirlesir = @DigerleriyleBirlesir
WHERE Id = @Id;";

    private const string SqlKosulSil = @"DELETE FROM KampanyaKosul WHERE KampanyaId = @kampanyaId;";
    private const string SqlSonucSil = @"DELETE FROM KampanyaSonuc WHERE KampanyaId = @kampanyaId;";

    private const string SqlKosulEkle = @"
INSERT INTO KampanyaKosul (KampanyaId, Tip, UrunId, KategoriId, MinMiktar, MinTutar)
VALUES (@KampanyaId, @Tip, @UrunId, @KategoriId, @MinMiktar, @MinTutar);";

    private const string SqlSonucEkle = @"
INSERT INTO KampanyaSonuc (KampanyaId, Tip, Yuzde, Tutar, OdenecekMiktar)
VALUES (@KampanyaId, @Tip, @Yuzde, @Tutar, @OdenecekMiktar);";

    public async Task<List<KampanyaTanimVm>> GecerliTanimlariGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await TanimlariOkuAsync(conn, SqlGecerliTanimlar, null, ct);
    }

    public async Task<List<KampanyaTanimVm>> HepsiniGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await TanimlariOkuAsync(conn, SqlHepsi, null, ct);
    }

    public async Task<KampanyaTanimVm?> GetirAsync(int id, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await TanimlariOkuAsync(conn, SqlTek, new { id }, ct);
        return liste.FirstOrDefault();
    }

    /// <summary>UrunId -> KategoriId eslemesi; kategori bazli kampanyalar icin.</summary>
    public async Task<Dictionary<int, int>> UrunKategorileriAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var satirlar = await conn.QueryAsync<(int UrunId, int KategoriId)>(
            new CommandDefinition(SqlUrunKategorileri, cancellationToken: ct));

        return satirlar.ToDictionary(s => s.UrunId, s => s.KategoriId);
    }

    public async Task<bool> KodVarMiAsync(string kod, int? hariciId = null, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlKodVarMi, new { kod, hariciId }, cancellationToken: ct));
    }

    /// <summary>
    /// Kampanyayi kosul ve sonuclariyla birlikte yazar. Guncellemede eski
    /// kosul/sonuc satirlari silinip yeniden yazilir: kampanya tipi
    /// degistiginde artakalan satir kalmasin.
    /// </summary>
    public async Task<int> KaydetAsync(
        IDbConnection conn, IDbTransaction tx, KampanyaTanimVm kampanya, CancellationToken ct = default)
    {
        int id;

        if (kampanya.Id == 0)
        {
            id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlEkle, kampanya, tx, cancellationToken: ct));
        }
        else
        {
            id = kampanya.Id;
            await conn.ExecuteAsync(new CommandDefinition(SqlGuncelle, kampanya, tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(SqlKosulSil, new { kampanyaId = id }, tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(SqlSonucSil, new { kampanyaId = id }, tx, cancellationToken: ct));
        }

        foreach (var kosul in kampanya.Kosullar)
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlKosulEkle, new
            {
                KampanyaId = id,
                kosul.Tip,
                kosul.UrunId,
                kosul.KategoriId,
                kosul.MinMiktar,
                kosul.MinTutar
            }, tx, cancellationToken: ct));
        }

        foreach (var sonuc in kampanya.Sonuclar)
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlSonucEkle, new
            {
                KampanyaId = id,
                sonuc.Tip,
                sonuc.Yuzde,
                sonuc.Tutar,
                sonuc.OdenecekMiktar
            }, tx, cancellationToken: ct));
        }

        return id;
    }

    private static async Task<List<KampanyaTanimVm>> TanimlariOkuAsync(
        IDbConnection conn, string sql, object? parametreler, CancellationToken ct)
    {
        using var sonuc = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parametreler, cancellationToken: ct));

        var kampanyalar = (await sonuc.ReadAsync<KampanyaTanimVm>()).AsList();
        var kosullar = (await sonuc.ReadAsync<(int Id, int KampanyaId, byte Tip, int? UrunId, int? KategoriId, decimal? MinMiktar, decimal? MinTutar)>()).AsList();
        var sonuclar = (await sonuc.ReadAsync<(int Id, int KampanyaId, byte Tip, decimal? Yuzde, decimal? Tutar, decimal? OdenecekMiktar)>()).AsList();

        foreach (var kampanya in kampanyalar)
        {
            kampanya.Kosullar = kosullar
                .Where(k => k.KampanyaId == kampanya.Id)
                .Select(k => new KampanyaKosulVm
                {
                    Id = k.Id,
                    Tip = k.Tip,
                    UrunId = k.UrunId,
                    KategoriId = k.KategoriId,
                    MinMiktar = k.MinMiktar,
                    MinTutar = k.MinTutar
                })
                .ToList();

            kampanya.Sonuclar = sonuclar
                .Where(s => s.KampanyaId == kampanya.Id)
                .Select(s => new KampanyaSonucVm
                {
                    Id = s.Id,
                    Tip = s.Tip,
                    Yuzde = s.Yuzde,
                    Tutar = s.Tutar,
                    OdenecekMiktar = s.OdenecekMiktar
                })
                .ToList();
        }

        return kampanyalar;
    }
}
