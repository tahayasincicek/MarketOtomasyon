using Dapper;
using MarketOtomasyon.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MarketOtomasyon.Web;

public sealed class VeritabaniSaglikKontrolu(IDbConnectionFactory baglantiFabrikasi)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await baglantiFabrikasi
                .CreateOpenConnectionAsync(cancellationToken);
            var sonuc = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT 1",
                cancellationToken: cancellationToken));

            return sonuc == 1
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Veritabanı beklenen yanıtı vermedi.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Veritabanına ulaşılamıyor.", ex);
        }
    }
}
