using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Alis faturasi ve satirlari. Yazma metotlari conn/tx disaridan alir:
/// fatura, satirlari ve tetikledigi mal kabulleriyle birlikte TEK
/// transaction'da yazilir (AlisFaturasiService).
/// </summary>
public sealed class AlisFaturasiRepository
{
    private readonly IDbConnectionFactory _factory;

    public AlisFaturasiRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlFaturaVarMi = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM AlisFaturasi WHERE TedarikciId = @tedarikciId AND FaturaNo = @faturaNo
) THEN 1 ELSE 0 END;";

    private const string SqlEkle = @"
INSERT INTO AlisFaturasi
    (TedarikciId, FaturaNo, FaturaTarihi, KullaniciId, DepoId, AraToplam, ToplamKdv, GenelToplam, Aciklama)
OUTPUT INSERTED.Id
VALUES
    (@TedarikciId, @FaturaNo, @FaturaTarihi, @KullaniciId, @DepoId, @AraToplam, @ToplamKdv, @GenelToplam, @Aciklama);";

    private const string SqlSatirEkle = @"
INSERT INTO AlisFaturasiSatir
    (FaturaId, SatirNo, UrunId, Miktar, BirimFiyat, KdvOrani, SatirMatrah, SatirKdv, SonKullanmaTarihi, LotNo)
OUTPUT INSERTED.Id
VALUES
    (@FaturaId, @SatirNo, @UrunId, @Miktar, @BirimFiyat, @KdvOrani, @SatirMatrah, @SatirKdv, @SonKullanmaTarihi, @LotNo);";

    private const string SqlSonFaturalar = @"
SELECT TOP (@adet)
       f.Id, f.FaturaNo, f.FaturaTarihi,
       TedarikciUnvan = t.Unvan,
       Depo = d.Ad,
       SatirSayisi = (SELECT COUNT(*) FROM AlisFaturasiSatir s WHERE s.FaturaId = f.Id),
       f.GenelToplam
FROM AlisFaturasi f
JOIN Tedarikci t ON t.Id = f.TedarikciId
JOIN Depo d      ON d.Id = f.DepoId
ORDER BY f.KayitTarihi DESC, f.Id DESC;";

    private const string SqlDetayBaslik = @"
SELECT f.Id, f.FaturaNo, f.FaturaTarihi,
       TedarikciUnvan = t.Unvan,
       Depo = d.Ad,
       KullaniciAdSoyad = k.AdSoyad,
       f.Aciklama, f.AraToplam, f.ToplamKdv, f.GenelToplam
FROM AlisFaturasi f
JOIN Tedarikci t  ON t.Id = f.TedarikciId
JOIN Depo d       ON d.Id = f.DepoId
JOIN Kullanici k  ON k.Id = f.KullaniciId
WHERE f.Id = @id;

SELECT u.Kod AS UrunKod, u.Ad AS UrunAd, s.Miktar, u.Birim,
       s.BirimFiyat, s.KdvOrani, s.SatirMatrah, s.SatirKdv,
       s.SonKullanmaTarihi, s.LotNo
FROM AlisFaturasiSatir s
JOIN Urun u ON u.Id = s.UrunId
WHERE s.FaturaId = @id
ORDER BY s.SatirNo;";

    public async Task<bool> FaturaVarMiAsync(int tedarikciId, string faturaNo, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            SqlFaturaVarMi, new { tedarikciId, faturaNo }, cancellationToken: ct));
    }

    public async Task<int> EkleAsync(
        IDbConnection conn, IDbTransaction tx, AlisFaturasi fatura, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlEkle, fatura, tx, cancellationToken: ct));

    /// <summary>Eklenen satirin Id'sini doner; StokParti.AlisFaturasiSatirId'ye yazilir.</summary>
    public async Task<int> SatirEkleAsync(
        IDbConnection conn, IDbTransaction tx, AlisFaturasiSatir satir, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlSatirEkle, satir, tx, cancellationToken: ct));

    public async Task<IReadOnlyList<AlisFaturasiGecmisSatirVm>> SonFaturalarAsync(
        int adet = 20, CancellationToken ct = default)
    {
        adet = Math.Clamp(adet, 1, 200);
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<AlisFaturasiGecmisSatirVm>(
            new CommandDefinition(SqlSonFaturalar, new { adet }, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<AlisFaturasiDetayVm?> DetayGetirAsync(int id, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var sonuc = await conn.QueryMultipleAsync(
            new CommandDefinition(SqlDetayBaslik, new { id }, cancellationToken: ct));

        var baslik = await sonuc.ReadSingleOrDefaultAsync<AlisFaturasiDetayVm>();
        if (baslik is null) return null;

        baslik.Satirlar = (await sonuc.ReadAsync<AlisFaturasiDetaySatirVm>()).AsList();
        return baslik;
    }
}
