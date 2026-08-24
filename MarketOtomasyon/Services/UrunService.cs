using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using System.Globalization;

namespace MarketOtomasyon.Services;

/// <summary>
/// Urun is kurallari. Urun ve fiyat kayitlari tek transaction icinde yazilir:
/// urun eklenip fiyati eklenemezse ortada fiyatsiz urun kalmamali.
/// </summary>
public class UrunService
{
    private readonly IDbConnectionFactory _factory;
    private readonly UrunRepository _urunRepository;
    private readonly FiyatRepository _fiyatRepository;
    private readonly IslemLogRepository _islemLogRepository;

    public UrunService(
        IDbConnectionFactory factory,
        UrunRepository urunRepository,
        FiyatRepository fiyatRepository,
        IslemLogRepository islemLogRepository)
    {
        _factory = factory;
        _urunRepository = urunRepository;
        _fiyatRepository = fiyatRepository;
        _islemLogRepository = islemLogRepository;
    }

    public async Task<int> EkleAsync(UrunFormVm form, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var urunId = await _urunRepository.EkleAsync(conn, tx, FormdanUrun(form), ct);
        await _fiyatRepository.FiyatEkleAsync(conn, tx, urunId, form.Fiyat, ct);

        tx.Commit();
        return urunId;
    }

    public async Task GuncelleAsync(
        UrunFormVm form,
        int kullaniciId,
        CancellationToken ct = default)
    {
        // Fiyat gercekten degistiyse yeni satir acilir; her kayitta gecmis sismesin.
        var mevcutFiyat = await _fiyatRepository.GuncelFiyatAsync(form.Id, ct);
        var fiyatDegisti = mevcutFiyat != form.Fiyat;

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _urunRepository.GuncelleAsync(conn, tx, FormdanUrun(form), ct);

        if (fiyatDegisti)
        {
            await _fiyatRepository.AcikFiyatiKapatAsync(conn, tx, form.Id, ct);
            await _fiyatRepository.FiyatEkleAsync(conn, tx, form.Id, form.Fiyat, ct);
            await _islemLogRepository.EkleAsync(conn, tx, new IslemLog
            {
                KullaniciId = kullaniciId,
                IslemTipi = "FiyatDegisikligi",
                HedefTipi = "Urun",
                HedefId = form.Id,
                EskiDeger = Fiyat(mevcutFiyat),
                YeniDeger = Fiyat(form.Fiyat),
                Aciklama = $"{form.Kod} - {form.Ad} ürününün satış fiyatı değiştirildi."
            }, ct);
        }

        tx.Commit();
    }

    private static string? Fiyat(decimal? fiyat)
        => fiyat?.ToString("0.00", CultureInfo.InvariantCulture);

    private static Urun FormdanUrun(UrunFormVm form) => new()
    {
        Id = form.Id,
        Kod = form.Kod.Trim(),
        Ad = form.Ad.Trim(),
        KategoriId = form.KategoriId,
        Birim = form.Birim,
        KdvOrani = form.KdvOrani,
        MinStokSeviyesi = form.MinStokSeviyesi,
        Tartili = form.Tartili,
        Aktif = form.Aktif
    };
}
