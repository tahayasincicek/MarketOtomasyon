# Mimari ve tasarım kararları

[← README](../README.md)

---

## Katmanlar

```
Controller  ──►  Service  ──►  Repository  ──►  SQL Server
   HTTP          iş kuralı      Dapper + SQL
```

### Controller — `Controllers/`

HTTP isteğini karşılar, servisi çağırır, view döner. İş kuralı içermez.
Yetkilendirme bu katmandadır: `[Authorize(Roles = ...)]`.

### Service — `Services/`

İş kuralları burada, iki tür sınıfta toplanır.

**Saf hesaplayıcılar** veritabanına erişmez; girdi alır, çıktı verir. Bu
nedenle doğrudan birim testi yazılabilir.

| Sınıf | Sorumluluk |
|---|---|
| `SepetHesaplayici` | Satır toplamı, KDV, indirim dağıtımı |
| `KampanyaHesaplayici` | Kampanya seçimi ve indirim tutarı |
| `IadeKurallari` | İade miktarı ve tutarı doğrulaması |
| `SayimKurallari` | Sayım farkından stok düzeltmesi |
| `OdemeHesaplayici` | Para üstü |
| `FifoMaliyetHesaplayici` | Parti tüketim dağılımı |
| `PartiKurallari` | Son kullanma tarihi ve lot doğrulaması |
| `BarkodCozumleyici` | Terazi barkodundan ürün kodu ve gramaj |
| `FaturaHesaplayici` | Alış faturası satır ve KDV hesabı |

**Orkestrasyon servisleri** transaction açar, saf hesaplayıcıları çağırır
ve repository'lere yazar: `SatisService`, `IadeService`, `SayimService`,
`VardiyaService`, `MaliyetService`, `OdemeService`, `TransferService`,
`AlisFaturasiService`.

Ayrımın nedeni, para hesabı yapan kodun veritabanı olmadan test
edilebilmesidir.

### Repository — `Data/Repositories/`

Veritabanına erişen tek katmandır. SQL sorguları C# içinde
`private const string` olarak tutulur; ayrı `.sql` dosyalarına dağılmaz,
böylece sorgu ile çağıran kod yan yana okunur.

Repository iş kuralı içermez ve transaction yönetmez. Transaction'ı açan
servis, `IDbConnection` ve `IDbTransaction` nesnelerini repository'ye
geçirir.

---

## Stok ve maliyet

### Stok miktarı kolon olarak tutulmaz

`Urun` tablosunda `StokMiktari` alanı yoktur. Bakiye, `StokHareket`
tablosundaki giriş ve çıkışların toplamıdır (`vw_StokBakiye`).

Bu sayede stok farklarının kaynağı her zaman hareket listesinde görünür
ve eşzamanlı iki satış birbirinin sayacını ezemez.

### Sevk sırası FEFO'dur

Raftan önce son kullanma tarihi en yakın parti çıkar; satılan malın
maliyeti o partinin maliyetidir. Son kullanma tarihi olmayan ürünler
sıranın sonunda kalır ve fiilen FIFO ile tüketilir.

Sıralama tek yerde tanımlıdır: `MaliyetRepository.SqlAcikPartiler`
içindeki `ORDER BY`.

> İlk satırın (`CASE WHEN SonKullanmaTarihi IS NULL`) kaldırılması sessiz
> bir hataya yol açar: SQL Server `NULL` değerleri başa koyduğundan
> tarihsiz ürünler, yarın bozulacak süt yerine önce tüketilir.

Gıda ürünlerinde mal kabulde tarih zorunludur
(`Urun.SonKullanmaZorunlu`). Boş bırakılan tarih partiyi sıranın sonuna
taşır.

### Süresi geçmiş parti satılamaz

Satış, parti tüketiminde bugünü geçerlilik günü olarak geçer; süresi
dolmuş partiler sıralamaya girmez. Karşılaştırma `>=` olduğundan son
kullanma günü **bugün** olan ürün satılabilir, ertesi gün satılamaz.

Filtre sorguya gömülü değil, parametreliktir (`@gecerlilikGunu`):

| İşlem | Değer | Neden |
|---|---|---|
| Satış | Bugün | Süresi geçmiş mal satılmamalı |
| Zayi | `NULL` | Süresi geçmiş malın düşülmesi gerekir |
| Transfer | `NULL` | Depo değişimi satış değildir |
| Sayım düzeltmesi | `NULL` | Fiziksel sayım tarihe bakmaz |

Filtre sabit olsaydı ilgili stok ne satılabilir ne düşülebilir olurdu.

Satış reddedildiğinde kasiyere gerçek sebep bildirilir: *"satılabilir
stok yok, 20 birim son kullanma tarihi geçmiş stok var, zayi olarak
düşülmeli."*

Kasa ekranında ayrıca erken uyarı vardır: sepete giren üründe süresi
geçmiş stok bulunuyorsa satırda kırmızı **SKT** rozeti görünür. Rozet
satışı engellemez — sepetteki mal taze partiden çıkıyor olabilir — amacı
raf kontrolüdür. Rozeti besleyen sorgu sepetteki tüm ürünler için tek
seferde çalışır; kasa sıcak yol olduğundan satır başına sorgu yapılmaz.

### Son kullanma ekranı bir iş listesidir

`/SonKullanma` süresi geçmiş partileri kırmızı, yaklaşanları sarı
gösterir. Süresi geçmiş satırlardaki "zayi'ye al" işlemi partinin
kalanını tek adımda düşer ve satır listeden çıkar.

Kasadaki satış reddi son savunma hattıdır; bu liste sorunun kasaya
ulaşmamasını sağlar.

Zayi burada parti bazlıdır (`SayimService.PartiZayiKaydetAsync`). Genel
zayi ürün ve depo alıp FEFO ile ilerlediğinden, ekranda işaretlenen lot
ile düşülen lot farklı olabilirdi.

### Transfer partileri de taşır

Depolar arası transfer yalnızca iki stok hareketi yazmaz; kaynak depodaki
partileri FEFO sırasıyla tüketir ve hedef depoda aynı maliyet, son
kullanma tarihi ve lot ile yeni partiler açar.

Aksi halde hedef depoda bakiye görünür ancak parti bulunmaz ve satış
"parti bakiyesi yetersiz" hatasıyla kesilir.

Tüketilen her parti için hedefte ayrı giriş hareketi yazılır;
`UX_StokParti_Hareket` benzersiz olduğundan bir stok hareketine yalnızca
bir parti bağlanabilir.

---

## Fiyat ve fatura

### Alış fiyatı KDV hariç, satış fiyatı KDV dahil saklanır

Alış KDV'si indirilebilir olduğundan maliyete girmez; müşteri etiketteki
tutarı öder.

`FaturaHesaplayici` KDV'yi matrahın üstüne ekler, `SepetHesaplayici`
tutarın içinden ayrıştırır. Ters yönde çalıştıkları için ayrı sınıflarda
tutulurlar.

### Alış faturası mal kabulü sarmalar

Fatura kaydedildiğinde her satır aynı transaction içinde stok hareketi ve
FEFO partisi oluşturur (`StokService.MalKabulYazAsync`). Stok ile belge
hiçbir durumda ayrışmaz; bir satırda hata oluşursa tüm fatura geri alınır.

Tedarikçi ve alış faturası modülü bilinçli olarak dardır. Cari hesap,
ödeme takibi, sipariş ve irsaliye kapsam dışıdır.

### Fiyat fiş satırında saklanır

`FisSatir.BirimFiyat` ürün kartından okunmaz; satış anındaki fiyat oraya
kopyalanır. Ürün fiyatı sonradan değiştiğinde geçmiş fişler ve iade
tutarları etkilenmez.

---

## Teknik ayrıntılar

### Tarihler UTC yazılır

Tüm `DATETIME2` kolonları `SYSUTCDATETIME()` ile dolar. Gün ve saat
kırılımı gereken raporlarda sorgu yerel saate çevirir:

```sql
CAST((f.Tarih AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') AS DATE)
```

Çevrilmediğinde UTC+3'te günün ilk üç saati bir önceki güne düşer ve saat
yoğunluğu grafiği kayar.

### Model binding invariant kültürde yapılır

HTML `number` alanları ondalık ayırıcı olarak nokta gönderir. Sunucu
`tr-TR` kültüründe çalışsaydı nokta binlik ayırıcı sayılır ve `18.75`
değeri `1875` olarak bağlanırdı.

`Program.cs` invariant kültür zorlar; ekrandaki gösterim ayrıca
biçimlendirilir.

### Fiş numarası sequence'ten alınır

`MAX(FisNo)+1` eşzamanlı iki satışta aynı numarayı üretir; `FisNoSeq`
üretmez.

### Üçüncü parti dosyalar gömülüdür

Bootstrap, jQuery, Phosphor Icons, ZXing ve Chart.js `wwwroot/lib/`
altındadır; CDN kullanılmaz. Kasa bilgisayarının internet bağlantısı
koptuğunda uygulama çalışmaya devam etmelidir.

---

## Klasör yapısı

```
MarketOtomasyon/
├── Controllers/          HTTP uçları
├── Services/             İş kuralları
├── Data/
│   ├── Repositories/     Dapper + SQL
│   ├── Sql/              Şema betikleri (derlemeye gömülü)
│   └── VeritabaniKurucu  Migration çalıştırıcı
├── Models/
│   ├── Entities/         Tablo karşılıkları
│   └── ViewModels/       Ekrana giden şekiller
├── Validators/           FluentValidation kuralları
├── ViewComponents/       Tekrar eden ekran parçaları
├── Security/             Rol sabitleri, claim yardımcıları
├── Web/                  Ara katmanlar, konsol komutları
├── Views/                Razor sayfaları
└── wwwroot/
    ├── css/ js/          Uygulama varlıkları
    ├── lib/              Üçüncü parti (gömülü, CDN yok)
    └── urun-gorsel/      Ürün fotoğrafları

MarketOtomasyon.Tests/    Birim testleri
Dockerfile                Konteyner imajı
```

---

## Testler

```bash
dotnet test
```

255 test, `MarketOtomasyon.Tests/` altındadır. Hiçbiri veritabanı
gerektirmez; tamamı saf kural ve hesaplayıcı sınıflarını hedefler.

Kapsanan alanlar: sepet toplamları ve KDV, kampanya seçimi, iade
kuralları, sayım düzeltmesi, para üstü, FIFO maliyet, terazi barkodu
çözümleme, fatura hesaplama, tedarikçi ve transfer kuralları, müdür
onayı, yetkilendirme, son kullanma sınıflandırması, migration betik
seçimi, ayar dosyası doğrulaması.

Bazı kurallar SQL içinde yaşar ve birim testiyle doğrulanamaz — FEFO
sıralaması ve süresi geçmiş parti filtresi gibi. Bunlar geliştirme
sırasında gerçek veritabanında `ROLLBACK` ile biten betiklerle
sınanmıştır.

---

## Ayarlar

`appsettings.json` içindeki uygulama ayarları:

| Bölüm | Anahtar | Anlamı |
|---|---|---|
| `Satis` | `DepoKodu` | Satış ve iadenin stoğu etkilediği depo (`MRK`) |
| `Satis` | `NegatifStogaIzinVer` | `false` ise stok yetersizse satış reddedilir |
| `Iade` | `SureGun` | Kaç gün sonrasına kadar iade kabul edilir (30) |
| `UrunResim` | `ApiTabani` | Open Food Facts uç noktası |
| `UrunResim` | `IstekAraligiMs` | İstekler arası bekleme; servis dakikada 15 istekle sınırlı |

Loglama `Serilog` bölümünden yapılandırılır. Günlük dosyalar `Loglar/`
altında tutulur, 30 gün saklanır, dosya başına 10 MB ile sınırlıdır.
