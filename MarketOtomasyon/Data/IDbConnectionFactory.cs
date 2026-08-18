using System.Data;

namespace MarketOtomasyon.Data;

public interface IDbConnectionFactory
{
    /// <summary>Acik durumda bir baglanti dondurur. Cagiran using ile kapatmalidir.</summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
