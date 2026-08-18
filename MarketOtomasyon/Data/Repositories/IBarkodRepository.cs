using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Data.Repositories;

/// <summary>
/// BarkodService'i veritabanindan ayirmak icin var: testlerde sahte bir
/// uygulama verilerek cozumleme mantigi SQL Server olmadan test edilebiliyor.
/// </summary>
public interface IBarkodRepository
{
    Task<BarkodCozumVm?> BarkodCozAsync(string barkod, CancellationToken ct = default);
    Task<IReadOnlyList<UrunBarkod>> UrunBarkodlariAsync(int urunId, CancellationToken ct = default);
    Task<bool> BarkodVarMiAsync(string barkod, CancellationToken ct = default);
    Task EkleAsync(UrunBarkod barkod, CancellationToken ct = default);
    Task<int> SilAsync(int id, int urunId, CancellationToken ct = default);
}
