using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Depo transferi kayitlari. Yazma metotlari conn/tx disaridan alir:
/// transfer tek transaction'da yurur ve transaction'i TransferService yonetir.
/// </summary>
public sealed class TransferRepository
{
    private readonly IDbConnectionFactory _factory;

    public TransferRepository(IDbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Numara sequence'ten alinir. MAX(TransferNo)+1 es zamanli iki
    /// transferde ayni numarayi uretirdi; IadeRepository de ayni bicimi
    /// kullaniyor.
    /// </summary>
    private const string SqlEkle = @"
DECLARE @no INT = NEXT VALUE FOR TransferNoSeq;
DECLARE @transferNo NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMdd') + '-T-' + FORMAT(@no, '00000');

INSERT INTO StokTransfer (TransferNo, KaynakDepoId, HedefDepoId, KullaniciId, Aciklama)
OUTPUT INSERTED.Id, INSERTED.TransferNo
VALUES (@transferNo, @KaynakDepoId, @HedefDepoId, @KullaniciId, @Aciklama);";

    private const string SqlSatirEkle = @"
INSERT INTO StokTransferSatir (TransferId, UrunId, Miktar)
VALUES (@TransferId, @UrunId, @Miktar);";

    private const string SqlSonTransferler = @"
SELECT TOP (@adet)
       t.Id,
       t.TransferNo,
       t.Tarih,
       KaynakDepo = kd.Ad,
       HedefDepo  = hd.Ad,
       k.AdSoyad,
       t.Aciklama,
       SatirSayisi  = (SELECT COUNT(*)      FROM StokTransferSatir s WHERE s.TransferId = t.Id),
       ToplamMiktar = (SELECT ISNULL(SUM(s.Miktar), 0) FROM StokTransferSatir s WHERE s.TransferId = t.Id)
FROM StokTransfer t
JOIN Depo kd      ON kd.Id = t.KaynakDepoId
JOIN Depo hd      ON hd.Id = t.HedefDepoId
JOIN Kullanici k  ON k.Id  = t.KullaniciId
ORDER BY t.Tarih DESC, t.Id DESC;";

    public async Task<(int Id, string TransferNo)> EkleAsync(
        IDbConnection conn, IDbTransaction tx, StokTransfer transfer, CancellationToken ct = default)
    {
        var satir = await conn.QuerySingleAsync<(int Id, string TransferNo)>(
            new CommandDefinition(SqlEkle, transfer, tx, cancellationToken: ct));
        return satir;
    }

    public async Task SatirEkleAsync(
        IDbConnection conn, IDbTransaction tx, StokTransferSatir satir, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlSatirEkle, satir, tx, cancellationToken: ct));

    public async Task<IReadOnlyList<TransferGecmisSatirVm>> SonTransferlerAsync(
        int adet = 20, CancellationToken ct = default)
    {
        adet = Math.Clamp(adet, 1, 200);
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<TransferGecmisSatirVm>(
            new CommandDefinition(SqlSonTransferler, new { adet }, cancellationToken: ct));
        return liste.AsList();
    }
}
