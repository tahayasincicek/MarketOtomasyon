namespace MarketOtomasyon.Web;

public static class ContainerSaglikKomutu
{
    public static async Task<int> CalistirAsync(CancellationToken ct = default)
    {
        try
        {
            using var istemci = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            using var yanit = await istemci.GetAsync(
                "http://127.0.0.1:8080/saglik/canli",
                ct);

            return yanit.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
