using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Tests;

/// <summary>Testlerde veritabani yerine gecen basit sozluk tabanli uygulama.</summary>
public class SahteBarkodRepository : IBarkodRepository
{
    private readonly Dictionary<string, BarkodCozumVm> _kayitlar = new(StringComparer.Ordinal);

    public SahteBarkodRepository Ekle(string barkod, BarkodCozumVm kayit)
    {
        _kayitlar[barkod] = kayit;
        return this;
    }

    public Task<BarkodCozumVm?> BarkodCozAsync(string barkod, CancellationToken ct = default)
        => Task.FromResult(_kayitlar.GetValueOrDefault(barkod));

    public Task<IReadOnlyList<UrunBarkod>> UrunBarkodlariAsync(int urunId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> BarkodVarMiAsync(string barkod, CancellationToken ct = default)
        => Task.FromResult(_kayitlar.ContainsKey(barkod));

    public Task EkleAsync(UrunBarkod barkod, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<int> SilAsync(int id, int urunId, CancellationToken ct = default)
        => throw new NotSupportedException();
}
