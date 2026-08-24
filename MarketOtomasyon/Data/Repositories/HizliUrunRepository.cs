using Dapper;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Kasa ekranindaki hizli urun tuslarini okur. Barkod ve fiyat tabloda
/// kopyalanmaz; her acilista urunun guncel tekli barkodu ve fiyati kullanilir.
/// </summary>
public sealed class HizliUrunRepository
{
    private readonly IDbConnectionFactory _factory;

    public HizliUrunRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlListele = @"
SELECT TOP (@adet)
       u.Id AS UrunId,
       u.Kod,
       u.Ad,
       u.Birim,
       b.Barkod,
       gf.Fiyat,
       u.ResimYolu,
       hu.Sira
FROM HizliUrun hu
JOIN Urun u ON u.Id = hu.UrunId AND u.Aktif = 1
CROSS APPLY (
    SELECT TOP (1) ub.Barkod
    FROM UrunBarkod ub
    WHERE ub.UrunId = u.Id AND ub.Tip = 1
    ORDER BY ub.Id
) b
JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE hu.Aktif = 1
ORDER BY hu.Sira, hu.Id;";

    public async Task<IReadOnlyList<HizliUrunVm>> ListeleAsync(
        int adet = 12,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<HizliUrunVm>(
            new CommandDefinition(SqlListele, new { adet }, cancellationToken: ct));

        return liste.AsList();
    }
}
