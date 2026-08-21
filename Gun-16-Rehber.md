# Gün 16 — Ürün Fotoğrafı (uygulama rehberi)

Kasada barkod okutulunca ve stok/ürün ekranlarında ürünün fotoğrafı görünecek.
Fotoğraflar kullanıcı tarafından yüklenmiyor; **Open Food Facts** açık ürün
veritabanından barkodla otomatik çekiliyor.

Sıra: 1 → 8.

---

## 0) Önce iki gerçeği bilmen lazım

### Barkodlarınız uydurma

`8690000000012` gerçek bir ürüne ait değil. `869` Türkiye'nin GS1 öneki ama
`0000000` diye bir firma yok — seed verisi olarak üretilmiş. Test ettim:

| Barkod | Open Food Facts |
|---|---|
| `8690000000012` (sizin Süt) | bulunamadı |
| `8690504017301` (Ülker Çubuk Kraker) | bulundu, resmi var |

Bu yüzden 2. adımda bir kısım ürünün barkodunu **gerçek Türk ürünleriyle**
değiştiriyoruz. Değiştirmezsek hiçbir ürüne resim gelmez.

### Dakikada 15 istek sınırı var

Open Food Facts kuralı: IP başına **dakikada 15 ürün sorgusu**, **10 arama**.
Bu rehberi hazırlarken bu sınıra kendim takıldım ve `HTTP 503` yedim.

Bunun tasarıma etkisi kritik: **kasada barkod okutulduğu anda API'ye gidilmez.**
Yoğun bir kasada bir dakikada 15 ürün rahat okutulur, IP'niz engellenir.

Doğru mimari:

```
BİR KEZ (yönetim ekranından)          HER SATIŞTA (kasada)
──────────────────────────            ────────────────────
Barkod → Open Food Facts              Ürün → DB'deki resim yolu
       → resmi indir                        → <img src="/urun-resim/…">
       → wwwroot'a kaydet
       → yolu Urun tablosuna yaz      (ağa hiç çıkılmaz)
```

Bu, projenin mevcut çizgisiyle de tutarlı: yazı tipini ve ZXing'i kasa
internetsiz kalsın diye yerelde tutmuştunuz. Ürün resmi de öyle olmalı.

---

## 1) YENİ DOSYA: `MarketOtomasyon/Data/Sql/06_urun_resim.sql`

```sql
/* =========================================================
   Urun fotografi
   ---------------------------------------------------------
   Resmin kendisi degil, wwwroot altindaki yolu saklanir.
   Boylece kasa ekrani her satista veritabanindan binary
   veri cekmez; dosyayi dogrudan tarayici onbellegi tasir.

   ResimKaynagi: resmin nereden geldigi. Attribution
   yukumlulugu icin gerekli (Open Food Facts resimleri
   CC-BY-SA lisansli).
   ========================================================= */

USE MarketOtomasyon;
GO

IF COL_LENGTH('Urun', 'ResimYolu') IS NULL
    ALTER TABLE Urun ADD ResimYolu NVARCHAR(260) NULL;
GO

IF COL_LENGTH('Urun', 'ResimKaynagi') IS NULL
    ALTER TABLE Urun ADD ResimKaynagi NVARCHAR(200) NULL;
GO

IF COL_LENGTH('Urun', 'ResimTarihi') IS NULL
    ALTER TABLE Urun ADD ResimTarihi DATETIME2 NULL;
GO
```

Çalıştır:

```bash
sqlcmd -S localhost -E -b -i "MarketOtomasyon\Data\Sql\06_urun_resim.sql"
```

---

## 2) YENİ DOSYA: `MarketOtomasyon/Data/Sql/07_gercek_barkodlar.sql`

Aşağıdaki barkodları Open Food Facts'te tek tek doğruladım — hepsinin kaydı
ve fotoğrafı var.

```sql
/* =========================================================
   Seed barkodlarini gercek Turk urunleriyle degistirir.
   Yalnizca TEKLI (Tip = 1) barkodlar degisir; koli
   barkodlari uydurma kalir, onlarin resmi de olmaz.

   Tartili urunler (Domates, Elma, Kiyma, peynirler,
   kuruyemis) barkodsuz satildigi icin listede yoktur;
   onlara yer tutucu gorunur.
   ========================================================= */

USE MarketOtomasyon;
GO

/* Kod | yeni barkod | Open Food Facts'teki karsiligi */
DECLARE @yeni TABLE (Kod NVARCHAR(30), Barkod NVARCHAR(30));

INSERT INTO @yeni (Kod, Barkod) VALUES
    ('URN001', '8690565100530'),   -- Sut 1 L          -> Pinar Sut 1 lt
    ('URN004', '8690698511760'),   -- Ekmek 250 g      -> UNO Cok Tahilli Ekmek
    ('URN006', '8690579140614'),   -- Makarna 500 g    -> Barilla Penne Rigate 500 g
    ('URN007', '8695077044198'),   -- Aycicek Yagi 1 L -> Sole Aycicek Yagi 1 L
    ('URN018', '5000112664492'),   -- Kola 1 L         -> Coca-Cola 1 L
    ('URN020', '8690767710537'),   -- Ayran 300 ml     -> Sutas Ayran 200 ml
    ('URN021', '8691381000486'),   -- Maden Suyu       -> Beypazari Dogal Maden Suyu
    ('URN022', '8690504135913'),   -- Cikolata 80 g    -> Ulker Cikolata
    ('URN024', '8690504017301');   -- Biskuvi 200 g    -> Ulker Cubuk Kraker

UPDATE b
SET    b.Barkod = y.Barkod
FROM   UrunBarkod b
JOIN   Urun u ON u.Id = b.UrunId
JOIN   @yeni y ON y.Kod = u.Kod
WHERE  b.Tip = 1;   -- yalnizca tekli barkod

SELECT u.Kod, u.Ad, b.Barkod
FROM   Urun u JOIN UrunBarkod b ON b.UrunId = u.Id
WHERE  b.Tip = 1 AND u.Kod IN (SELECT Kod FROM @yeni)
ORDER BY u.Kod;
GO
```

Çalıştır:

```bash
sqlcmd -S localhost -E -b -i "MarketOtomasyon\Data\Sql\07_gercek_barkodlar.sql"
```

> **Barkod test kartını yenilemen gerekecek.** `Gun-15-Barkod-Test-Karti.html`
> eski barkodları içeriyor; bu SQL'den sonra o karttaki 9 barkod artık
> veritabanıyla eşleşmez. Bana söyle, kartı yeni barkodlarla yeniden üreteyim.

---

## 3) `Models/Entities/Urun.cs`

Sınıfın içine ekle:

```csharp
    /// <summary>wwwroot altindaki gorece yol. Resim yoksa null.</summary>
    public string? ResimYolu { get; set; }

    /// <summary>Resmin kaynagi ve lisans bilgisi (CC-BY-SA atifi icin).</summary>
    public string? ResimKaynagi { get; set; }

    public DateTime? ResimTarihi { get; set; }
```

---

## 4) `appsettings.json`

`"Iade": { ... }` bloğunun altına ekle:

```json
  "UrunResim": {
    "ApiTabani": "https://world.openfoodfacts.org/api/v2/product/",
    "KullaniciAjani": "MarketOtomasyon/1.0 (staj-projesi)",
    "KlasorAdi": "urun-resim",
    "IstekAraligiMs": 4500,
    "ZamanAsimiSaniye": 15
  },
```

`IstekAraligiMs: 4500` → dakikada ~13 istek. Sınır 15; altında kalmak için pay bıraktık.
`KullaniciAjani` zorunlu — Open Food Facts kimliksiz istekleri bot sayıp engelliyor.

---

## 5) YENİ DOSYA: `MarketOtomasyon/Services/UrunResimAyarlari.cs`

```csharp
namespace MarketOtomasyon.Services;

public class UrunResimAyarlari
{
    public string ApiTabani { get; set; } = "https://world.openfoodfacts.org/api/v2/product/";

    /// <summary>Open Food Facts kimliksiz istekleri engelliyor; bu alan zorunlu.</summary>
    public string KullaniciAjani { get; set; } = "MarketOtomasyon/1.0";

    public string KlasorAdi { get; set; } = "urun-resim";

    /// <summary>Iki istek arasi bekleme. Servis dakikada 15 istek siniri koyuyor.</summary>
    public int IstekAraligiMs { get; set; } = 4500;

    public int ZamanAsimiSaniye { get; set; } = 15;
}
```

---

## 6) YENİ DOSYA: `MarketOtomasyon/Services/UrunResimService.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;

namespace MarketOtomasyon.Services;

/// <summary>
/// Urun fotograflarini Open Food Facts'ten bir kez ceker, wwwroot altina
/// indirir ve yolunu Urun tablosuna yazar.
///
/// Kasa akisinda BU SERVIS CAGRILMAZ. Open Food Facts dakikada 15 istek
/// siniri koyuyor; her barkod okutmada API'ye gidilirse IP engellenir.
/// Cagri yalnizca yonetim ekranindaki "Resimleri Cek" dugmesinden yapilir.
/// </summary>
public class UrunResimService
{
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

    public record Sonuc(int Denenen, int Bulunan, int Bulunamayan, List<string> Hatalar);

    /// <summary>Resmi olmayan tum urunler icin sirayla dener.</summary>
    public async Task<Sonuc> TumEksikleriCekAsync(CancellationToken ct = default)
    {
        var urunler = await _repository.ResmiOlmayanlarAsync(ct);
        var hatalar = new List<string>();
        int bulunan = 0, bulunamayan = 0;

        var klasor = Path.Combine(_ortam.WebRootPath, _ayarlar.KlasorAdi);
        Directory.CreateDirectory(klasor);

        var istemci = _istemciFabrikasi.CreateClient("acikUrunVeritabani");

        foreach (var urun in urunler)
        {
            ct.ThrowIfCancellationRequested();

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
                        urun.UrunId, yol,
                        "Open Food Facts (CC-BY-SA) · " + urun.Barkod, ct);
                    bulunan++;
                }
            }
            catch (Exception ex)
            {
                _kayit.LogWarning(ex, "Urun {Kod} resmi cekilemedi", urun.Kod);
                hatalar.Add($"{urun.Kod}: {ex.Message}");
            }

            // Hiz sinirina saygi: son urunden sonra beklemeye gerek yok.
            if (!ReferenceEquals(urun, urunler[^1]))
                await Task.Delay(_ayarlar.IstekAraligiMs, ct);
        }

        return new Sonuc(urunler.Count, bulunan, bulunamayan, hatalar);
    }

    /// <summary>Bulunursa wwwroot'a gorece yolu, bulunamazsa null doner.</summary>
    private async Task<string?> BirUrunCekAsync(
        HttpClient istemci, string klasor, string barkod, string urunKodu, CancellationToken ct)
    {
        var adres = $"{_ayarlar.ApiTabani}{barkod}.json"
                  + "?fields=code,product_name,image_front_small_url,image_front_url";

        using var yanit = await istemci.GetAsync(adres, ct);

        // 429/503: hiz sinirina takildik. Sessizce "bulunamadi" demek yaniltici olur.
        if ((int)yanit.StatusCode is 429 or 503)
            throw new InvalidOperationException(
                "Open Food Facts hız sınırı (HTTP " + (int)yanit.StatusCode + "). Biraz bekleyip tekrar deneyin.");

        if (!yanit.IsSuccessStatusCode) return null;

        using var akis = await yanit.Content.ReadAsStreamAsync(ct);
        using var belge = await JsonDocument.ParseAsync(akis, cancellationToken: ct);
        var kok = belge.RootElement;

        // status 1 = urun bulundu, 0 = kayit yok
        if (!kok.TryGetProperty("status", out var durum) || durum.GetInt32() != 1) return null;
        if (!kok.TryGetProperty("product", out var urun)) return null;

        // Kucuk gorsel kasa ekrani icin yeterli; tam boy bosuna yer kaplar.
        var resimAdresi = MetinAl(urun, "image_front_small_url") ?? MetinAl(urun, "image_front_url");
        if (string.IsNullOrWhiteSpace(resimAdresi)) return null;

        var uzanti = Path.GetExtension(new Uri(resimAdresi).AbsolutePath);
        if (string.IsNullOrWhiteSpace(uzanti)) uzanti = ".jpg";

        // Dosya adi urun koduyla: ayni urun tekrar cekilirse uzerine yazilir,
        // klasorde artik dosya birikmez.
        var dosyaAdi = urunKodu + uzanti;
        var tamYol = Path.Combine(klasor, dosyaAdi);

        var veri = await istemci.GetByteArrayAsync(resimAdresi, ct);
        await File.WriteAllBytesAsync(tamYol, veri, ct);

        return $"/{_ayarlar.KlasorAdi}/{dosyaAdi}";
    }

    private static string? MetinAl(JsonElement oge, string alan) =>
        oge.TryGetProperty(alan, out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
}
```

---

## 7) YENİ DOSYA: `MarketOtomasyon/Data/Repositories/UrunResimRepository.cs`

```csharp
using Dapper;

namespace MarketOtomasyon.Data.Repositories;

public class UrunResimRepository
{
    private readonly IDbConnectionFactory _factory;

    public UrunResimRepository(IDbConnectionFactory factory) => _factory = factory;

    public record EksikResimSatiri(int UrunId, string Kod, string Ad, string Barkod);

    // Yalnizca tekli barkodu olan aktif urunler. Koli barkodu ayri bir
    // ambalaji tarif eder, urunun kendi fotografini vermez.
    private const string SqlEksikler = @"
SELECT u.Id AS UrunId, u.Kod, u.Ad, b.Barkod
FROM Urun u
JOIN UrunBarkod b ON b.UrunId = u.Id AND b.Tip = 1
WHERE u.Aktif = 1
  AND u.ResimYolu IS NULL
  AND LEN(b.Barkod) = 13
ORDER BY u.Kod;";

    private const string SqlResimYaz = @"
UPDATE Urun
SET ResimYolu = @yol, ResimKaynagi = @kaynak, ResimTarihi = SYSUTCDATETIME()
WHERE Id = @urunId;";

    public async Task<List<EksikResimSatiri>> ResmiOlmayanlarAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<EksikResimSatiri>(
            new CommandDefinition(SqlEksikler, cancellationToken: ct));
        return liste.AsList();
    }

    public async Task ResimYazAsync(int urunId, string yol, string kaynak, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            SqlResimYaz, new { urunId, yol, kaynak }, cancellationToken: ct));
    }
}
```

---

## 8) `Program.cs`

Repository'ler bloğuna:

```csharp
builder.Services.AddScoped<UrunResimRepository>();
```

Servisler bloğuna:

```csharp
builder.Services.AddScoped<UrunResimService>();
```

Ayarlar bloğuna:

```csharp
builder.Services.Configure<UrunResimAyarlari>(builder.Configuration.GetSection("UrunResim"));
```

Ve `var app = builder.Build();` satırının **üstüne**, HttpClient tanımı:

```csharp
// Open Food Facts kimliksiz istekleri bot sayip engelliyor; User-Agent zorunlu.
builder.Services.AddHttpClient("acikUrunVeritabani", (sp, c) =>
{
    var ayar = sp.GetRequiredService<IOptions<UrunResimAyarlari>>().Value;
    c.DefaultRequestHeaders.UserAgent.ParseAdd(ayar.KullaniciAjani);
    c.Timeout = TimeSpan.FromSeconds(ayar.ZamanAsimiSaniye);
});
```

Dosyanın üstüne `using Microsoft.Extensions.Options;` ekle.

---

## Buraya kadar sunucu tarafı bitti

Sıradaki adımlar (ekranlarda gösterme) 9–13. Devamını istersen söyle,
onları da yazayım:

- **9)** `Urun` ekranına "Resimleri Çek" düğmesi ve sonuç bildirimi
- **10)** Ortak `_UrunResmi.cshtml` partial'ı + yer tutucu (resmi olmayan ürün için)
- **11)** Kasa: "Son okutulan" panelinde büyük fotoğraf
- **12)** Kasa sepeti + stok listesi + ürün listesinde küçük görsel
- **13)** Ürün detayında büyük görsel + kaynak/lisans atfı

---

## Bilmen gereken sınırlar

**Open Food Facts sadece gıda kapsıyor.** 30 ürününüzün dağılımı:

| Grup | Adet | Resim gelir mi |
|---|---|---|
| Paketli gıda (barkodu değiştirilenler) | 9 | ✅ evet |
| Paketli gıda (barkodu uydurma kalan) | 9 | ❌ yer tutucu |
| Tartılı (Domates, Elma, Kıyma, peynirler, kuruyemiş) | 6 | ❌ barkodsuz satılır, yer tutucu |
| Temizlik / kişisel bakım | 6 | ❌ OFF gıda dışını kapsamaz |

Yani ilk çalıştırmada **9 ürüne** resim gelecek, kalan 21'i yer tutucu gösterecek.
Bu aslında gerçekçi: hiçbir market otomasyonunda her ürünün fotoğrafı olmaz,
ekran bunu düzgün karşılamalı. 10. adımdaki yer tutucu tam da bunun için.

Daha fazla ürüne resim istersen iki yol var: gıda dışı için
**Open Products Facts** (`world.openproductsfacts.org`) ve kozmetik için
**Open Beauty Facts** (`world.openbeautyfacts.org`) — aynı API, aynı kod,
sadece taban adres değişiyor.

**Lisans:** Open Food Facts verisi ODbL, fotoğraflar CC-BY-SA. Ticari bir üründe
kullanacaksan ürün detayında kaynağı belirtmen gerekir — bu yüzden
`ResimKaynagi` kolonunu ekledik, 13. adımda ekrana basacağız.
