using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Stok hareketleri. Bakiye kolonu yoktur; hareketlerin toplamindan okunur.
/// </summary>
public class StokRepository
{
    private readonly IDbConnectionFactory _factory;

    public StokRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlHareketEkle = @"
INSERT INTO StokHareket (UrunId, DepoId, Yon, Miktar, KaynakTip, KaynakId, Aciklama)
VALUES (@UrunId, @DepoId, @Yon, @Miktar, @KaynakTip, @KaynakId, @Aciklama);";

    private const string SqlBakiye = @"
SELECT ISNULL(SUM(CASE WHEN Yon = 1 THEN Miktar ELSE -Miktar END), 0)
FROM StokHareket
WHERE UrunId = @urunId AND DepoId = @depoId;";

    private const string SqlBakiyeListesi = @"
SELECT COUNT(*)
FROM Urun u
WHERE u.Aktif = 1
  AND (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@sadeceKritik = 0 OR ISNULL((
        SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
        FROM StokHareket h WHERE h.UrunId = u.Id), 0) <= u.MinStokSeviyesi);

SELECT u.Id AS UrunId, u.Kod, u.Ad, u.Birim, u.MinStokSeviyesi,
       ISNULL((SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
               FROM StokHareket h WHERE h.UrunId = u.Id), 0) AS ToplamBakiye
FROM Urun u
WHERE u.Aktif = 1
  AND (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@sadeceKritik = 0 OR ISNULL((
        SELECT SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END)
        FROM StokHareket h WHERE h.UrunId = u.Id), 0) <= u.MinStokSeviyesi)
ORDER BY u.Ad
OFFSET @atla ROWS FETCH NEXT @adet ROWS ONLY;";

    private const string SqlSonHareketler = @"
SELECT TOP (@adet)
       h.Id, h.Tarih, h.Yon, h.Miktar, h.KaynakTip, h.Aciklama,
       u.Kod AS UrunKod, u.Ad AS UrunAd, u.Birim, d.Ad AS DepoAd
FROM StokHareket h
JOIN Urun u ON u.Id = h.UrunId
JOIN Depo d ON d.Id = h.DepoId
ORDER BY h.Id DESC;";

    public async Task HareketEkleAsync(IDbConnection conn, IDbTransaction tx, StokHareket hareket, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlHareketEkle, hareket, tx, cancellationToken: ct));

    public async Task<decimal> BakiyeAsync(int urunId, int depoId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(SqlBakiye, new { urunId, depoId }, cancellationToken: ct));
    }

    public async Task<(IReadOnlyList<StokSatirVm> Satirlar, int ToplamKayit)> BakiyeListesiAsync(
        string? arama, bool sadeceKritik, int sayfa, int sayfaBoyutu, CancellationToken ct = default)
    {
        var parametreler = new
        {
            arama = string.IsNullOrWhiteSpace(arama) ? null : arama.Trim(),
            sadeceKritik = sadeceKritik ? 1 : 0,
            atla = (sayfa - 1) * sayfaBoyutu,
            adet = sayfaBoyutu
        };

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var sonuc = await conn.QueryMultipleAsync(
            new CommandDefinition(SqlBakiyeListesi, parametreler, cancellationToken: ct));

        var toplam = await sonuc.ReadSingleAsync<int>();
        var satirlar = (await sonuc.ReadAsync<StokSatirVm>()).AsList();
        return (satirlar, toplam);
    }

    public async Task<IReadOnlyList<StokHareketSatirVm>> SonHareketlerAsync(int adet, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<StokHareketSatirVm>(
            new CommandDefinition(SqlSonHareketler, new { adet }, cancellationToken: ct));
        return liste.AsList();
    }
}
