namespace MarketOtomasyon.Web;

public sealed class VeriKorumaAyarlari
{
    public const string Bolum = "VeriKoruma";

    public string UygulamaAdi { get; set; } = "MarketOtomasyon";
    public string? AnahtarKlasoru { get; set; }

    public IReadOnlyList<string> DogrulamaHatalari(bool containerdaCalisiyor)
    {
        var hatalar = new List<string>();

        if (string.IsNullOrWhiteSpace(UygulamaAdi))
            hatalar.Add("VeriKoruma:UygulamaAdi boş bırakılamaz.");

        if (containerdaCalisiyor && string.IsNullOrWhiteSpace(AnahtarKlasoru))
        {
            hatalar.Add(
                "Container ortamında VeriKoruma:AnahtarKlasoru tanımlanmalıdır. " +
                "Bu klasörü kalıcı ve bütün instance'ların erişebildiği bir depoya bağlayın.");
        }

        if (!string.IsNullOrWhiteSpace(AnahtarKlasoru))
        {
            try
            {
                _ = Path.GetFullPath(AnahtarKlasoru);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                hatalar.Add("VeriKoruma:AnahtarKlasoru geçerli bir dosya yolu değildir.");
            }
        }

        return hatalar;
    }
}
