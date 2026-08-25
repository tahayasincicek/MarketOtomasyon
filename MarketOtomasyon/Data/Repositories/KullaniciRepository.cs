using System.Data;
using Dapper;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

public class KullaniciRepository
{
    private readonly IDbConnectionFactory _factory;

    public KullaniciRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SqlAdSoyad = @"
SELECT AdSoyad FROM Kullanici WHERE Id = @kullaniciId;";

    private const string SqlRol = @"
SELECT Rol FROM Kullanici WHERE Id = @kullaniciId AND Aktif = 1;";

    private const string SqlKullaniciAdiyla = @"
SELECT Id, KullaniciAdi, AdSoyad, SifreHash, Rol, Aktif
FROM Kullanici
WHERE KullaniciAdi = @kullaniciAdi AND Aktif = 1;";

    private const string SqlSifreHashGuncelle = @"
UPDATE Kullanici SET SifreHash = @sifreHash
WHERE Id = @kullaniciId AND Aktif = 1;";

    /* ---------- Personel yonetimi ---------- */

    /// <summary>
    /// Acik vardiya alt sorguyla getirilir: mudur, pasiflestirmeye karar
    /// vermeden once kimin kasada oldugunu ayni listede gormeli.
    /// Pasifler de listelenir, aktifler once gelir.
    /// </summary>
    private const string SqlPersonelListesi = @"
SELECT k.Id, k.KullaniciAdi, k.AdSoyad, k.Rol, k.Aktif,
       AcikVardiyaId = (SELECT TOP (1) v.Id
                        FROM Vardiya v
                        WHERE v.KullaniciId = k.Id AND v.Durum = 1
                        ORDER BY v.Id DESC)
FROM Kullanici k
ORDER BY k.Aktif DESC, k.AdSoyad;";

    private const string SqlIdIleGetir = @"
SELECT Id, KullaniciAdi, AdSoyad, SifreHash, Rol, Aktif
FROM Kullanici
WHERE Id = @kullaniciId;";

    private const string SqlKullaniciAdiVarMi = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Kullanici WHERE KullaniciAdi = @kullaniciAdi
) THEN 1 ELSE 0 END;";

    /// <summary>Pasif mudur sayilmaz: kural aktif mudur sayisina bakar.</summary>
    private const string SqlAktifMudurSayisi = @"
SELECT COUNT(*) FROM Kullanici WHERE Rol = @mudurKodu AND Aktif = 1;";

    private const string SqlEkle = @"
INSERT INTO Kullanici (KullaniciAdi, AdSoyad, SifreHash, Rol, Aktif)
OUTPUT INSERTED.Id
VALUES (@KullaniciAdi, @AdSoyad, @SifreHash, @Rol, @Aktif);";

    private const string SqlAktiflikGuncelle = @"
UPDATE Kullanici SET Aktif = @aktif WHERE Id = @kullaniciId;";

    private const string SqlRolGuncelle = @"
UPDATE Kullanici SET Rol = @rol WHERE Id = @kullaniciId;";

    /// <summary>
    /// Sifirlamada Aktif sarti YOK. SifreHashGuncelleAsync girisin kendi
    /// rehash akisi icin yazildi ve yalnizca aktif kullaniciyi gunceller;
    /// mudur ise pasif bir hesabin sifresini de sifirlayabilmeli
    /// (personel geri ise donunce hesap yeniden acilir).
    /// </summary>
    private const string SqlSifreSifirla = @"
UPDATE Kullanici SET SifreHash = @sifreHash WHERE Id = @kullaniciId;";

    /// <summary>Kullanici yoksa veya pasifse null doner.</summary>
    public async Task<byte?> RolGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<byte?>(
            new CommandDefinition(SqlRol, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<string?> AdSoyadGetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(SqlAdSoyad, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<Kullanici?> KullaniciAdiylaGetirAsync(
        string kullaniciAdi,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Kullanici>(
            new CommandDefinition(SqlKullaniciAdiyla, new { kullaniciAdi }, cancellationToken: ct));
    }

    public async Task SifreHashGuncelleAsync(
        int kullaniciId,
        string sifreHash,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            SqlSifreHashGuncelle,
            new { kullaniciId, sifreHash },
            cancellationToken: ct));
    }

    /* ---------- Personel yonetimi ----------
       Yazma metodlari conn/tx disaridan alir: her degisiklik IslemLog
       kaydiyla ayni transaction'da yazilmali, yoksa denetim izi ile
       gercek durum birbirini tutmaz. */

    public async Task<IReadOnlyList<PersonelSatirVm>> PersonelListesiAsync(
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<PersonelSatirVm>(
            new CommandDefinition(SqlPersonelListesi, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task<Kullanici?> GetirAsync(int kullaniciId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Kullanici>(
            new CommandDefinition(SqlIdIleGetir, new { kullaniciId }, cancellationToken: ct));
    }

    public async Task<bool> KullaniciAdiVarMiAsync(string kullaniciAdi, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SqlKullaniciAdiVarMi, new { kullaniciAdi }, cancellationToken: ct));
    }

    public async Task<int> AktifMudurSayisiAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            SqlAktifMudurSayisi,
            new { mudurKodu = Security.Roller.MudurKodu },
            cancellationToken: ct));
    }

    public async Task<int> EkleAsync(
        IDbConnection conn, IDbTransaction tx, Kullanici kullanici, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlEkle, kullanici, tx, cancellationToken: ct));

    public async Task<int> AktiflikGuncelleAsync(
        IDbConnection conn, IDbTransaction tx, int kullaniciId, bool aktif, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlAktiflikGuncelle, new { kullaniciId, aktif }, tx, cancellationToken: ct));

    public async Task<int> RolGuncelleAsync(
        IDbConnection conn, IDbTransaction tx, int kullaniciId, byte rol, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlRolGuncelle, new { kullaniciId, rol }, tx, cancellationToken: ct));

    public async Task<int> SifreSifirlaAsync(
        IDbConnection conn, IDbTransaction tx, int kullaniciId, string sifreHash, CancellationToken ct = default)
        => await conn.ExecuteAsync(new CommandDefinition(
            SqlSifreSifirla, new { kullaniciId, sifreHash }, tx, cancellationToken: ct));
}
