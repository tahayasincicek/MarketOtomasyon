using System.Text.Json;
using Microsoft.Extensions.Options;
using MarketOtomasyon.Data.Repositories;

namespace MarketOtomasyon.Services;

/// <summary>
/// Urun fotograflarini Open Food Facts'ten bir kez ceker, wwwroot altina
/// indirir ve yolunu Urun tablosuna yazar.
///
/// KASA AKISINDA BU SERVIS CAGRILMAZ. Open Food Facts dakikada 15 urun
/// sorgusu siniri koyuyor; her barkod okutmada API'ye gidilirse yogun bir
/// kasada IP birkac dakikada engellenir. Cagri yalnizca yonetim ekranindaki
/// "Resimleri Cek" dugmesinden yapilir, sonuc veritabaninda saklanir.
/// </summary>
public class UrunResimService
{
    /// <summary>Indirilecek en buyuk dosya. Beklenen boyut 5-30 KB.</summary>
    private const int EnBuyukDosyaBayt = 2 * 1024 * 1024;

    private static readonly string[] IzinliUzantilar = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IHttpClientFactory _istemciFabrikasi;
    private readonly UrunResimRepository _repository;
    private readonly IWebHostEnvironment _ortam;
    private readonly UrunResimAyarlari _ayarlar;
    private readonly ILogger<UrunResimService> _kayit;

    public UrunResimService(
        IHttpClientFactory istemciFabrikasi,
        UrunResimRepository repository,
        IWebHostEnvironment ortam,
        IOptions<UrunResimAyarlari> ayarlar,
        ILogger<UrunResimService> kayit)
    {
        _istemciFabrikasi = istemciFabrikasi;
        _repository = repository;
        _ortam = ortam;
        _ayarlar = ayarlar.Value;
        _kayit = kayit;
    }

    public record Sonuc(int Denenen, int Bulunan, int Bulunamayan, List<string> Hatalar)
    {
        public static Sonuc Bos => new(0, 0, 0, []);
    }

    /// <summary>Resmi olmayan tum urunler icin sirayla dener.</summary>
    public async Task<Sonuc> TumEksikleriCekAsync(CancellationToken ct = default)
    {
        var urunler = await _repository.ResmiOlmayanlarAsync(ct);
        if (urunler.Count == 0) return Sonuc.Bos;

        var klasor = Path.Combine(_ortam.WebRootPath, _ayarlar.KlasorAdi);
        Directory.CreateDirectory(klasor);

        var istemci = _istemciFabrikasi.CreateClient("acikUrunVeritabani");
        var hatalar = new List<string>();
        int bulunan = 0, bulunamayan = 0;

        for (var i = 0; i < urunler.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var urun = urunler[i];

            try
            {
                var yol = await BirUrunCekAsync(istemci, klasor, urun.Barkod, urun.Kod, ct);
                if (yol is null)
                {
                    bulunamayan++;
                }
                else
                {
                    await _repository.ResimYazAsync(
                        urun.UrunId, yol, $"Open Food Facts (CC-BY-SA) · {urun.Barkod}", ct);
                    bulunan++;
                }
            }
            catch (HizSiniriAsildi)
            {
                // Devam etmek sinirin daha da uzamasina yol acar; kalanlari birak.
                hatalar.Add("Open Food Facts hız sınırına takıldı; kalan ürünler atlandı. " +
                            "Bir dakika bekleyip tekrar deneyin.");
                break;
            }
            catch (Exception ex)
            {
                _kayit.LogWarning(ex, "Urun {Kod} ({Barkod}) resmi cekilemedi", urun.Kod, urun.Barkod);
                hatalar.Add($"{urun.Kod}: {ex.Message}");
            }

            // Hiz sinirina saygi. Son urunden sonra beklemenin anlami yok.
            if (i < urunler.Count - 1)
                await Task.Delay(_ayarlar.IstekAraligiMs, ct);
        }

        return new Sonuc(urunler.Count, bulunan, bulunamayan, hatalar);
    }

    /// <summary>Bulunursa wwwroot'a gorece yol, kayit yoksa null.</summary>
    private async Task<string?> BirUrunCekAsync(
        HttpClient istemci, string klasor, string barkod, string urunKodu, CancellationToken ct)
    {
        var adres = $"{_ayarlar.ApiTabani}{barkod}.json"
                  + "?fields=code,product_name,image_front_small_url,image_front_url";

        using var yanit = await istemci.GetAsync(adres, ct);

        // 429/503: hiz siniri. "Bulunamadi" saymak yaniltici olur - urun
        // aslinda orada, biz cok hizli sorduk.
        if ((int)yanit.StatusCode is 429 or 503) throw new HizSiniriAsildi();
        if (!yanit.IsSuccessStatusCode) return null;

        await using var akis = await yanit.Content.ReadAsStreamAsync(ct);
        using var belge = await JsonDocument.ParseAsync(akis, cancellationToken: ct);
        var kok = belge.RootElement;

        // status 1 = urun bulundu, 0 = kayit yok
        if (!kok.TryGetProperty("status", out var durum) || durum.GetInt32() != 1) return null;
        if (!kok.TryGetProperty("product", out var urun)) return null;

        // Kucuk gorsel (200 px) kasa ekrani icin yeterli; tam boy bosuna yer kaplar.
        var resimAdresi = MetinAl(urun, "image_front_small_url") ?? MetinAl(urun, "image_front_url");
        if (string.IsNullOrWhiteSpace(resimAdresi)) return null;
        if (!Uri.TryCreate(resimAdresi, UriKind.Absolute, out var resimUri)) return null;

        // Uzanti disaridan geliyor; dosya adina karismasin diye beyaz liste.
        var uzanti = Path.GetExtension(resimUri.AbsolutePath).ToLowerInvariant();
        if (!IzinliUzantilar.Contains(uzanti)) uzanti = ".jpg";

        var veri = await IndirAsync(istemci, resimUri, ct);
        if (veri is null) return null;

        // Dosya adi urun kodu: ayni urun tekrar cekilirse uzerine yazilir,
        // klasorde artik dosya birikmez. Kod disaridan gelmiyor, guvenli.
        var dosyaAdi = urunKodu + uzanti;
        await File.WriteAllBytesAsync(Path.Combine(klasor, dosyaAdi), veri, ct);

        return $"/{_ayarlar.KlasorAdi}/{dosyaAdi}";
    }

    private async Task<byte[]?> IndirAsync(HttpClient istemci, Uri adres, CancellationToken ct)
    {
        using var yanit = await istemci.GetAsync(adres, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!yanit.IsSuccessStatusCode) return null;

        // Beklenmedik boyutta bir dosya diske yazilmasin.
        if (yanit.Content.Headers.ContentLength > EnBuyukDosyaBayt) return null;

        var veri = await yanit.Content.ReadAsByteArrayAsync(ct);
        return veri.Length is > 0 and <= EnBuyukDosyaBayt ? veri : null;
    }

    private static string? MetinAl(JsonElement oge, string alan) =>
        oge.TryGetProperty(alan, out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;

    private sealed class HizSiniriAsildi : Exception;
}
