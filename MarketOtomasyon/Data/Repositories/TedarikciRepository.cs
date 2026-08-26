using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// Tedarikci karti. Yazma metotlari conn/tx disaridan alir; transaction
/// yonetimi servisin isi.
/// </summary>
public sealed class TedarikciRepository
{
    private readonly IDbConnectionFactory _factory;

    public TedarikciRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlListele = @"
SELECT Id, Kod, Unvan, VergiNo, Telefon, Aktif
FROM Tedarikci
WHERE (@arama IS NULL OR Unvan LIKE '%' + @arama + '%' OR Kod LIKE '%' + @arama + '%')
  AND (@sadeceAktif = 0 OR Aktif = 1)
ORDER BY Unvan;";

    private const string SqlGetir = @"
SELECT Id, Kod, Unvan, VergiNo, VergiDairesi, Telefon, Eposta, Adres, Aktif, OlusturmaTarihi
FROM Tedarikci
WHERE Id = @id;";

    private const string SqlAktifleriGetir = @"
SELECT Id, Kod, Unvan, VergiNo, VergiDairesi, Telefon, Eposta, Adres, Aktif, OlusturmaTarihi
FROM Tedarikci
WHERE Aktif = 1
ORDER BY Unvan;";

    private const string SqlKodVarMi = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Tedarikci WHERE Kod = @kod AND (@hariciId IS NULL OR Id <> @hariciId)
) THEN 1 ELSE 0 END;";

    private const string SqlEkle = @"
INSERT INTO Tedarikci (Kod, Unvan, VergiNo, VergiDairesi, Telefon, Eposta, Adres, Aktif)
OUTPUT INSERTED.Id
VALUES (@Kod, @Unvan, @VergiNo, @VergiDairesi, @Telefon, @Eposta, @Adres, @Aktif);";

    private const string SqlGuncelle = @"
UPDATE Tedarikci
SET Kod = @Kod, Unvan = @Unvan, VergiNo = @VergiNo, VergiDairesi = @VergiDairesi,
    Telefon = @Telefon, Eposta = @Eposta, Adres = @Adres, Aktif = @Aktif
WHERE Id = @Id;";

    public async Task<IReadOnlyList<TedarikciSatirVm>> ListeleAsync(
        string? arama, bool sadeceAktif, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<TedarikciSatirVm>(new CommandDefinition(
            SqlListele,
            new { arama = string.IsNullOrWhiteSpace(arama) ? null : arama.Trim(), sadeceAktif = sadeceAktif ? 1 : 0 },
            cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<Tedarikci?> GetirAsync(int id, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Tedarikci>(
            new CommandDefinition(SqlGetir, new { id }, cancellationToken: ct));
    }

    /// <summary>Fatura formundaki tedarikci secim listesi icin.</summary>
    public async Task<IReadOnlyList<Tedarikci>> AktifleriGetirAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Tedarikci>(
            new CommandDefinition(SqlAktifleriGetir, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<bool> KodVarMiAsync(string kod, int? hariciId = null, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlKodVarMi, new { kod, hariciId }, cancellationToken: ct));
    }

    public async Task<int> EkleAsync(IDbConnection conn, IDbTransaction tx, Tedarikci tedarikci, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(SqlEkle, tedarikci, tx, cancellationToken: ct));

    public async Task GuncelleAsync(IDbConnection conn, IDbTransaction tx, Tedarikci tedarikci, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(SqlGuncelle, tedarikci, tx, cancellationToken: ct));
}
