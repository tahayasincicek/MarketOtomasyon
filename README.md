# Market Otomasyon

Küçük ölçekli bir market için kasa, stok, iade ve raporlama uygulaması.
ASP.NET Core MVC (.NET 8) + Dapper + SQL Server.

Staj projesi olarak geliştirildi. Veritabanı erişimi ORM ile değil, elle
yazılmış SQL ile yapılır; amaç sorguların ne yaptığının görünür kalması.

---

## Hızlı kurulum

Gereksinimler:

- .NET 8 SDK
- SQL Server 2019+ (Express yeterli) veya LocalDB
- `sqlcmd` ya da SQL Server Management Studio

```bash
git clone https://github.com/tahayasincicek/MarketOtomasyon.git
cd MarketOtomasyon
```

Bağlantı dizesi `MarketOtomasyon/appsettings.json` içinde:

```json
"ConnectionStrings": {
  "MarketDb": "Server=localhost;Database=MarketOtomasyon;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
}
```

Farklı bir sunucu kullanıyorsan `Server=` kısmını değiştir. SQL Server
kimlik doğrulaması gerekiyorsa `Trusted_Connection=True` yerine
`User Id=...;Password=...` yaz.

Veritabanını kur (aşağıdaki bölümdeki sırayla), sonra:

```bash
dotnet run --project MarketOtomasyon
```

Uygulama `https://localhost:7037` adresinde açılır.

Geliştirme hesapları:

| Kullanıcı | Şifre | Rol |
|---|---|---|
| `mudur` | `Mudur123!` | Müdür — tüm ekranlar |
| `kasiyer1` | `Kasiyer123!` | Kasiyer — kasa, iade, vardiya |

---

## SQL dosyalarının çalıştırma sırası

**Dosya adlarındaki numaralar çalıştırma sırası değildir.** Numaralar
dosyanın projeye eklendiği günü gösteriyor. Gerçek sıra aşağıdaki gibi:
önce şema, sonra örnek veri, en son veriyi güncelleyen betikler.

Hepsi `MarketOtomasyon/Data/Sql/` altında.

### 1. Şema (bu sırayla)

| # | Dosya | Ne yapar |
|---|---|---|
| 1 | `01_ilk_sema.sql` | Veritabanını oluşturur; ürün, barkod, fiyat, stok, vardiya, fiş, ödeme tabloları |
| 2 | `02_askiya_alma.sql` | Fişe `Askida` kolonu — kasada bekleyen sepetler |
| 3 | `03_kampanya.sql` | Kampanya başlığı, koşulları ve sonuçları |
| 4 | `04_iade.sql` | `Iade` / `IadeSatir` tabloları, `FisSatir.IadeEdilenMiktar` |
| 5 | `05_vardiya.sql` | İadenin vardiyaya bağlanması |
| 6 | `06_urun_resim.sql` | Ürün görselinin dosya yolu ve kaynağı |
| 7 | `08_sayim_zayi.sql` | Sayım ve zayi kayıtları |
| 8 | `03_maliyet.sql` | FIFO maliyet: `StokParti`, `StokPartiTuketim` |
| 9 | `12_yetkilendirme_log.sql` | `IslemLog` tablosu, geliştirme hesaplarının şifre hash'leri |
| 10 | `13_mudur_onayi.sql` | `IslemLog`'a onaylayan müdür ve onay sebebi kolonları |

> İki tane `03_` var: `03_kampanya.sql` ve `03_maliyet.sql`. Maliyet
> dosyası aslında çok daha sonra (Gün 17) eklendi, numarası yanlış verildi.
> Yukarıdaki sırayı takip et; birbirlerine bağımlı değiller ama ikisi de
> `01_ilk_sema.sql`'den sonra çalışmalı.

### 2. Örnek veri

| # | Dosya | Ne yapar |
|---|---|---|
| 11 | `90_ornek_veri.sql` | 2 depo, 2 kullanıcı, 6 kategori, 30 ürün, barkodlar, açılış fiyatları ve açılış stokları |

### 3. Veriyi güncelleyen betikler (örnek veriden **sonra**)

Bu dörtlü `90_ornek_veri.sql`'in eklediği kayıtları düzeltir/zenginleştirir.
Önce çalıştırılırsa güncelleyecek satır bulamaz ve sessizce hiçbir şey yapmaz.

| # | Dosya | Ne yapar |
|---|---|---|
| 12 | `07_gercek_barkodlar.sql` | Uydurma barkodları Open Food Facts'te karşılığı olan gerçek barkodlarla değiştirir |
| 13 | `09_profesyonel_urun_gorselleri.sql` | Ürünleri `wwwroot/urun-gorsel/*.webp` dosyalarına bağlar |
| 14 | `10_hizli_urun.sql` | Kasa ekranındaki hızlı ürün tuşlarını tanımlar |
| 15 | `11_turkce_karakter_duzeltmeleri.sql` | Örnek verideki Türkçe karakterleri düzeltir |

### 4. Demo verisi (isteğe bağlı)

| # | Dosya | Ne yapar |
|---|---|---|
| 16 | `91_demo_veri.sql` | Son 30 günün satış geçmişi: vardiyalar, fişler, ödemeler, iadeler |

Raporlar, kâr marjı ve Z raporu ekranları geçmiş satış olmadan boş görünür.
Bu betik onları dolduran gerçekçi bir geçmiş üretir. Ayrıntı için aşağıdaki
"Demo verisi" bölümüne bak.

### Toplu kurulum

PowerShell (proje kökünden):

```powershell
$sql = "MarketOtomasyon\Data\Sql"
$sira = @(
  "01_ilk_sema", "02_askiya_alma", "03_kampanya", "04_iade", "05_vardiya",
  "06_urun_resim", "08_sayim_zayi", "03_maliyet", "12_yetkilendirme_log",
  "13_mudur_onayi",
  "90_ornek_veri",
  "07_gercek_barkodlar", "09_profesyonel_urun_gorselleri", "10_hizli_urun",
  "11_turkce_karakter_duzeltmeleri",
  "91_demo_veri"
)
foreach ($ad in $sira) { sqlcmd -S localhost -E -i "$sql\$ad.sql" }
```

Tüm betikler tekrar çalıştırılabilir (idempotent): var olan kayıtları
atlar, sadece eksikleri ekler. `01_ilk_sema.sql` istisnadır — veritabanı
zaten varsa `CREATE DATABASE` hata verir, bu beklenen davranıştır.

---

## Mimari

Üç katman. Her katmanın tek bir sorumluluğu var:

```
Controller  ──►  Service  ──►  Repository  ──►  SQL Server
   HTTP          iş kuralı      Dapper + SQL
```

### Repository — `Data/Repositories/`

Veritabanına dokunan tek katman. Tüm SQL sorguları C# içinde
`private const string` olarak durur; ayrı `.sql` dosyalarına dağılmaz,
böylece sorgu ile onu çağıran kod yan yana okunur.

Repository iş kuralı bilmez: verilen parametreyle sorguyu çalıştırır,
sonucu döner. Transaction yönetmez — transaction'ı açan servis, ona
`IDbConnection` ve `IDbTransaction` geçirir.

### Service — `Services/`

İş kuralları burada. İki tür sınıf var:

**Saf hesaplayıcılar** — veritabanına hiç dokunmaz, girdi alır çıktı verir.
Bu yüzden birim testleri kolay ve hızlı:

- `SepetHesaplayici` — satır toplamı, KDV, indirim dağıtımı
- `KampanyaHesaplayici` — hangi kampanya uygulanır, ne kadar indirim
- `IadeKurallari` — iade miktarı geçerli mi, iade tutarı ne
- `SayimKurallari` — sayım farkından stok düzeltmesi
- `OdemeHesaplayici` — para üstü
- `FifoMaliyetHesaplayici` — hangi partiden ne kadar tüketilir
- `BarkodCozumleyici` — terazi barkodundan ürün kodu ve gramaj

**Orkestrasyon servisleri** — transaction açar, saf hesaplayıcıları çağırır,
repository'lere yazar: `SatisService`, `IadeService`, `SayimService`,
`VardiyaService`, `MaliyetService`, `OdemeService`.

Ayrım şu yüzden önemli: para hesabı yapan kod veritabanı olmadan test
edilebilmeli. `MarketOtomasyon.Tests` içindeki testlerin çoğu bu saf
sınıfları hedefler.

### Controller — `Controllers/`

HTTP isteğini karşılar, servisi çağırır, view döner. İş kuralı içermez.
Yetkilendirme burada: `[Authorize(Roles = ...)]`.

---

## Önemli tasarım kararları

**Stok miktarı kolon olarak tutulmaz.** `Urun` tablosunda `StokMiktari`
diye bir alan yok. Bakiye her zaman `StokHareket` tablosundaki giriş ve
çıkışların toplamıdır (`vw_StokBakiye` view'i). Böyle olunca "stok neden
eksi göründü" sorusunun cevabı her zaman hareket listesinde durur ve
eşzamanlı iki satış birbirinin sayacını ezemez.

**Fiyat fiş satırında saklanır.** `FisSatir.BirimFiyat`, ürün kartından
okunmaz; satış anındaki fiyat oraya kopyalanır. Ürünün fiyatı sonra
değişirse geçmiş fişler ve iade tutarları bozulmaz.

**Tarihler UTC yazılır.** Tüm `DATETIME2` kolonları `SYSUTCDATETIME()`
ile dolar. Gün ve saat kırılımı gereken raporlarda sorgu yerel saate
çevirir:

```sql
CAST((f.Tarih AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') AS DATE)
```

Çevrilmezse UTC+3'te sabahın ilk üç saati bir önceki güne düşer ve saat
yoğunluğu grafiği üç saat kayar.

**Model binding invariant kültürde yapılır.** HTML `number` alanları
ondalık ayırıcı olarak her zaman nokta gönderir. Sunucu `tr-TR`
kültüründe çalışsaydı nokta binlik ayırıcı sayılır ve `18.75` değeri
`1875` olarak bağlanırdı. `Program.cs` bu yüzden invariant kültür
zorlar; ekrandaki gösterim ayrıca biçimlendirilir.

**Fiş numarası sequence'ten alınır.** `MAX(FisNo)+1` eşzamanlı iki
satışta aynı numarayı üretir; `FisNoSeq` üretmez.

**Üçüncü parti dosyalar projeye gömülüdür.** Bootstrap, jQuery, Phosphor
Icons, ZXing ve Chart.js `wwwroot/lib/` altında durur; CDN kullanılmaz.
Kasa bilgisayarının internet bağlantısı koptuğunda uygulamanın çalışmaya
devam etmesi gerekir.

---

## Modüller

| Modül | Ekran | Ne yapar |
|---|---|---|
| Kasa | `/Kasa` | Barkod okutma, sepet, hızlı ürün tuşları, askıya alma |
| Ödeme | `/Odeme` | Nakit/kart/karışık ödeme, para üstü, fiş yazdırma |
| İade | `/Iade` | Fişten seçili satır ve miktarın iadesi |
| Vardiya | `/Vardiya` | Vardiya açma/kapama, kasa sayımı, Z raporu |
| Ürün | `/Urun` | Ürün kartı, barkod yönetimi, fiyat geçmişi |
| Stok | `/Stok` | Stok hareketleri, mal kabul |
| Sayım ve Zayi | `/Sayim` | Envanter sayımı, fire kaydı |
| Kampanya | `/Kampanya` | İndirim kuralları |
| Kâr Marjı | `/Maliyet` | FIFO maliyet, ürün bazında kâr |
| Raporlar | `/Rapor` | Günlük ciro, en çok satan, ödeme dağılımı, saat yoğunluğu, kritik stok |
| Personel | `/Personel` | Kullanıcı oluşturma, rol değiştirme, pasifleştirme, şifre sıfırlama |
| İşlem Logları | `/IslemLog` | Hassas işlemlerin denetim kaydı |

Barkod okuma iki yolla çalışır: USB barkod okuyucu (klavye gibi davranır)
ve kamera (ZXing.js ile, `wwwroot/js/kamera.js`).

---

## Demo verisi

`91_demo_veri.sql` son 30 güne yayılmış gerçekçi bir satış geçmişi üretir:

- Her gün için bir vardiya (bugünkü açık, önceki 29'u kapalı ve sayılmış)
- Günde 8–24 fiş, hafta sonu daha yoğun
- Saat dağılımı gerçek market trafiğine benzer: öğle ve akşam tepe yapar
- Fiş başına 1–6 satır, temel gıdalar dört kat ağırlıklı seçilir
- Ödemeler nakit/kart karışık, nakitte para üstü hesaplanır
- Fişlerin küçük bir kısmı iade edilir
- Satılan her ürün için stok çıkışı ve FIFO parti tüketimi yazılır
- Vardiyaların beşte birinde küçük bir kasa farkı bırakılır

30 günde yaklaşık 440 fiş, 160.000 TL ciro, 370 TL ortalama sepet üretir.

Betik tekrar çalıştırılabilir: ürettiği her kaydı `DEMO` önekiyle
işaretler, zaten varsa hiçbir şey yapmaz. Yeniden üretmek için betiğin
başındaki `@Temizle` değişkenini `1` yap — bu, ürettiği tüm kayıtları
silip baştan oluşturur, gerçek verilere dokunmaz.

Üretim tek transaction içinde yapılır; bir adım hata verirse tamamı geri
alınır, yarım satış geçmişi kalmaz.

**Üretimde çalıştırılmaz.** Bu betik sahte satış kaydı üretir; gerçek bir
markette ciro raporlarını bozar.

---

## Testler

```bash
dotnet test
```

Testler `MarketOtomasyon.Tests/` altında ve veritabanı gerektirmez —
hepsi saf hesaplayıcı sınıfları hedefler. Kapsanan alanlar: sepet
toplamları ve KDV, kampanya seçimi, iade kuralları, sayım düzeltmesi,
para üstü, FIFO maliyet, terazi barkodu çözümleme.

---

## Klasör yapısı

```
MarketOtomasyon/
├── Controllers/          HTTP uçları
├── Services/             İş kuralları (saf hesaplayıcılar + orkestrasyon)
├── Data/
│   ├── Repositories/     Dapper + SQL
│   └── Sql/              Şema ve veri betikleri
├── Models/
│   ├── Entities/         Tablo karşılıkları
│   └── ViewModels/       Ekrana giden şekiller
├── Validators/           FluentValidation kuralları
├── ViewComponents/       Tekrar eden ekran parçaları
├── Security/             Rol sabitleri, claim yardımcıları
├── Views/                Razor sayfaları
└── wwwroot/
    ├── css/ js/          Uygulama varlıkları
    ├── lib/              Üçüncü parti (gömülü, CDN yok)
    └── urun-gorsel/      Ürün fotoğrafları

MarketOtomasyon.Tests/    Birim testleri
```

---

## Yapılandırma

`appsettings.json` içindeki ayarlar:

| Bölüm | Anahtar | Anlamı |
|---|---|---|
| `Satis` | `DepoKodu` | Satış ve iadenin stoğu etkilediği depo (`MRK`) |
| `Satis` | `NegatifStogaIzinVer` | `false` ise stok yetmiyorsa satış reddedilir |
| `Iade` | `SureGun` | Fiş tarihinden kaç gün sonrasına kadar iade kabul edilir (30) |
| `UrunResim` | `ApiTabani` | Open Food Facts uç noktası |
| `UrunResim` | `IstekAraligiMs` | İki API isteği arası bekleme — servis dakikada 15 istekle sınırlı |
