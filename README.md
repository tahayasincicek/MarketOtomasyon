# Market Otomasyon

Küçük ölçekli bir market için kasa, stok, iade ve raporlama uygulaması.
ASP.NET Core MVC (.NET 8) + Dapper + SQL Server.

Veritabanı erişimi ORM ile değil, elle yazılmış SQL ile yapılır; amaç
sorguların ne yaptığının görünür kalmasıdır.

---

## İçindekiler

- [Kurulum](#kurulum)
- [Veritabanı kurulumu](#veritabanı-kurulumu)
- [Üretim yapılandırması](#üretim-yapılandırması)
- [Docker](#docker)
- [SQL betikleri](#sql-betikleri)
- [Mimari](#mimari)
- [Tasarım kararları](#tasarım-kararları)
- [Modüller](#modüller)
- [Demo verisi](#demo-verisi)
- [Testler](#testler)
- [Klasör yapısı](#klasör-yapısı)
- [Ayarlar](#ayarlar)

---

## Kurulum

### Gereksinimler

- .NET 8 SDK
- SQL Server 2019+ (Express yeterli) veya LocalDB

### Adımlar

```bash
git clone https://github.com/tahayasincicek/MarketOtomasyon.git
cd MarketOtomasyon
```

Yerel ayar dosyası şablondan oluşturulur:

```powershell
copy MarketOtomasyon\appsettings.Development.json.ornek MarketOtomasyon\appsettings.Development.json
```

Bağlantı dizesi bu dosyada tanımlıdır ve kuruluma göre düzenlenir:

```json
"ConnectionStrings": {
  "MarketDb": "Server=localhost;Database=MarketOtomasyon;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
}
```

SQL Server kimlik doğrulaması gerekiyorsa `Trusted_Connection=True`
yerine `User Id=...;Password=...` yazılır.

Ardından:

```bash
dotnet run --project MarketOtomasyon
```

Veritabanı yoksa oluşturulur ve şema betikleri otomatik uygulanır. Ayrı
bir kurulum adımı gerekmez. Uygulama `https://localhost:7037` adresinde
açılır.

Demo verisi (satış geçmişi, tedarikçiler) isteğe bağlıdır:

```bash
dotnet run --project MarketOtomasyon -- migrate --demo
```

### Geliştirme hesapları

| Kullanıcı | Şifre | Rol |
|---|---|---|
| `mudur` | `Mudur123!` | Müdür — tüm ekranlar |
| `kasiyer1` | `Kasiyer123!` | Kasiyer — kasa, iade, vardiya |

### Bağlantı dizesi neden `appsettings.json` içinde değil

`appsettings.json` her ortamda yüklenir ve depoda saklanır. Üretim
kimlik bilgileri oraya yazılırsa kaynak koda girer ve commit sonrası
geçmişten temizlenmesi zorlaşır. Bu nedenle:

| Dosya | İçerik | Depoda |
|---|---|---|
| `appsettings.json` | Ortak ayarlar, bağlantı dizesi **yok** | Evet |
| `appsettings.Development.json.ornek` | Şablon | Evet |
| `appsettings.Development.json` | Yerel bağlantı dizesi | Hayır |
| Ortam değişkeni | Üretim bağlantı dizesi | — |

Bağlantı dizesi hiçbir kaynakta bulunamazsa uygulama açılış anında
anlamlı bir hatayla durur; ilk isteğe kadar beklemez.

---

## Veritabanı kurulumu

Şema betikleri uygulama içine gömülüdür ve sırayla uygulanır. Uygulanan
her betik `SemaSurumu` tablosuna adı ve tarihiyle kaydedilir.

### Komutlar

| Komut | İşlev |
|---|---|
| `migrate` | Bekleyen betikleri uygular |
| `migrate --demo` | Demo verisi betiklerini de dahil eder |
| `migrate --liste` | Bekleyenleri listeler, hiçbir şey çalıştırmaz |
| `migrate --baseline` | Mevcut şemayı "uygulanmış" işaretler, çalıştırmaz |

Kullanımı:

```bash
dotnet run --project MarketOtomasyon -- migrate
```

Yayınlanmış çıktıda:

```bash
dotnet MarketOtomasyon.dll migrate
```

### Davranış

- **Tekrar çalıştırılabilir.** İkinci çalıştırmada "veritabanı güncel"
  bildirimi verilir, hiçbir betik yeniden işlenmez.
- **Yarıda kalırsa devam eder.** Bir betik hata verirse komut orada
  durur ve `1` çıkış kodu döner. Öncekiler kayıtlıdır; sorun giderilip
  komut yeniden çalıştırıldığında yalnızca kalanlar uygulanır.
- **Hatalı betik yarım kalmaz.** Her betik kendi transaction'ında
  çalışır; hata durumunda o betiğin yaptığı tüm değişiklikler geri alınır.

Yeni bir şema değişikliği için `Data/Sql/` altına sıradaki numarayla bir
dosya eklemek yeterlidir (`14_...sql`). Sıralama ve kayıt tutma
kendiliğinden işler.

Bu işlemi yürüten kütüphane **DbUp**'tır. EF Core gibi kod üretmez;
yalnızca `.sql` dosyalarını çalıştırır ve kaydeder. Betik içerikleri ham
SQL olarak kalır.

### Ortama göre çalışma biçimi

| Ortam | Davranış |
|---|---|
| Development | Uygulama açılışında otomatik uygulanır |
| Production | Otomatik **değildir**; `migrate` ayrı bir deploy adımıdır |

Üretimde otomatik çalıştırılmamasının nedeni, birden fazla uygulama
örneğinin aynı anda migration yürütme riski ve yavaş bir betiğin açılışı
kilitlemesidir.

### Mevcut bir veritabanının dahil edilmesi

Bu sürümden önce kurulmuş veritabanlarında tablolar bulunur ancak
`SemaSurumu` tablosu yoktur. `migrate` bu durumu algılar ve değişiklik
yapmadan durur; çünkü `01_ilk_sema.sql` korumasız `CREATE TABLE`
içerdiğinden dolu bir veritabanında baştan uygulanamaz.

Şemanın güncel olduğu biliniyorsa bir kez şu komut çalıştırılır:

```bash
dotnet run --project MarketOtomasyon -- migrate --baseline --demo
```

Komut hiçbir betik çalıştırmaz; mevcut şemayı "uygulanmış" olarak
işaretler. `--demo` yalnızca demo verisi zaten yüklüyse eklenir.

### Betiklerin elle çalıştırılması

Betikler `MarketOtomasyon/Data/Sql/` altındadır ve dosya adı sırası
çalıştırma sırasıdır:

```powershell
$sql = "MarketOtomasyon\Data\Sql"
Get-ChildItem "$sql\*.sql" | Where-Object { $_.Name -notmatch '^(3\d|92)_' } |
  Sort-Object Name | ForEach-Object {
    sqlcmd -S localhost -d MarketOtomasyon -b -i $_.FullName
    if ($LASTEXITCODE -ne 0) { Write-Error "Durdu: $($_.Name)"; break }
}
```

Bu yolda sürüm takibi oluşmaz; sonradan `migrate` kullanılacaksa
`--baseline` adımı gerekir. Betikler `CREATE DATABASE` ve `USE`
içermediğinden veritabanı önceden oluşturulmalı ve `-d` ile
hedeflenmelidir.

Şema betikleri tekrar çalıştırılabilir değildir; dolu bir veritabanında
ikinci kez uygulanmamalıdır.

---

## Üretim yapılandırması

Üretimde iki değer `appsettings` dosyalarından değil, ortamdan gelir.

### 1. Bağlantı dizesi

`ConnectionStrings__MarketDb` ortam değişkenine yazılır. Çift alt çizgi,
iç içe ayar anahtarının (`ConnectionStrings:MarketDb`) ortam değişkeni
karşılığıdır.

```powershell
setx ConnectionStrings__MarketDb "Server=sunucu;Database=MarketOtomasyon;User Id=market;Password=...;TrustServerCertificate=True"
```

`appsettings.Production.json` yalnızca log seviyesi ve izin verilen alan
adlarını taşır. Bu dosyaya bağlantı dizesi yazılırsa `AyarDosyalariTests`
başarısız olur; sırların kaynak koda sızması derleme aşamasında yakalanır.

### 2. Ortam adı

`ASPNETCORE_ENVIRONMENT=Production` olmalıdır. Bu ayar üç davranışı
birden belirler:

| Ayar | Development | Production |
|---|---|---|
| Oturum çerezi | HTTP üzerinden de gönderilir | Yalnızca HTTPS (`Secure`) |
| HSTS | Kapalı | Açık |
| Log seviyesi | `Debug` | `Information` |
| Migration | Açılışta otomatik | Ayrı komut |

Çerez politikasının ortama bağlanma nedeni: geliştirmede
`http://localhost` kullanıldığından çerez `Secure` işaretlenemez, aksi
halde oturum açılamaz. Üretimde işaretlenmezse HTTP'ye düşen tek bir
istekte oturum çerezi şifresiz iletilir.

`AllowedHosts` değeri kendi alan adıyla değiştirilmelidir; `*` bırakmak
Host başlığı sahteciliğine açık bırakır.

### Bilinen eksikler

Yukarıdaki iki madde deploy için gerekli minimumdur, yeterli değildir:

- Varsayılan `mudur` / `kasiyer1` hesaplarının şifreleri bu dosyada ve
  kurulum betiğinde yazılıdır; üretimden önce değiştirilmelidir.
- Giriş ekranında deneme sınırı yoktur. Başarısız denemeler loglanır
  ancak engellenmez.

---

## Docker

Kök dizindeki `Dockerfile` uygulamayı iki aşamada paketler: SDK
imajında derleyip yayınlar, ardından yalnızca ASP.NET çalışma zamanı
imajına kopyalar. Son imajda derleyici ve kaynak kod bulunmaz.

```bash
docker build -t marketotomasyon .
docker run -p 8080:8080 marketotomasyon
```

Veritabanı imajın içinde değildir. Konteyner içinde `localhost` kendi
ağ alanına işaret ettiğinden bağlantı dizesi dışarıdan verilir:

```bash
docker run -p 8080:8080 \
  -e "ConnectionStrings__MarketDb=Server=host.docker.internal;Database=MarketOtomasyon;User Id=sa;Password=...;TrustServerCertificate=True" \
  marketotomasyon
```

`host.docker.internal` konteynerden ana makineye işaret eder. Windows
kimlik doğrulaması konteynerde çalışmadığından kullanıcı adı ve şifre
kullanılmalıdır.

---

## SQL betikleri

Numara, çalıştırma sırasıdır. Betikler `migrate` komutuyla uygulandığı
için elle sıralamaya gerek yoktur; sıra bilgisi dosyaları okurken ve
yenisini eklerken kullanılır.

Betikler üç bloğa ayrılmıştır. Bloklar arasındaki boşluk, yeni dosya
eklendiğinde sonrakileri kaydırma ihtiyacını ortadan kaldırır.

### Şema (`01`–`13`)

| Dosya | İçerik |
|---|---|
| `01_ilk_sema.sql` | Çekirdek şema: ürün, barkod, fiyat, stok, vardiya, fiş, ödeme |
| `02_askiya_alma.sql` | `Fis.Askida` kolonu — kasada bekleyen sepetler |
| `03_kampanya.sql` | Kampanya başlığı, koşulları ve sonuçları |
| `04_iade.sql` | `Iade` / `IadeSatir`, `FisSatir.IadeEdilenMiktar` |
| `05_vardiya.sql` | İadenin vardiyaya bağlanması |
| `06_urun_resim.sql` | Ürün görselinin dosya yolu ve kaynağı |
| `07_sayim_zayi.sql` | Sayım ve zayi kayıtları |
| `08_maliyet.sql` | Parti maliyeti: `StokParti`, `StokPartiTuketim` |
| `09_yetkilendirme_log.sql` | `IslemLog` tablosu, hesap şifre hash'leri |
| `10_mudur_onayi.sql` | `IslemLog`'a onaylayan müdür ve onay sebebi |
| `11_skt_lot.sql` | `StokParti`ye son kullanma tarihi, lot, tedarikçi |
| `12_depo_transfer.sql` | `StokTransfer` / `StokTransferSatir` |
| `13_tedarikci_fatura.sql` | `Tedarikci`, `AlisFaturasi` / `AlisFaturasiSatir` |

### Örnek veri (`20`–`23`)

`20_ornek_veri.sql` kataloğu kurar; `21`–`23` onu zenginleştirir ve
mutlaka sonrasında çalışır. Önce çalıştırılırlarsa güncelleyecek satır
bulamaz ve sessizce sonuçsuz kalırlar.

| Dosya | İçerik |
|---|---|
| `20_ornek_veri.sql` | 2 depo, 2 kullanıcı, 6 kategori, 30 ürün, fiyat ve açılış stoğu |
| `21_gercek_barkodlar.sql` | Barkodları Open Food Facts karşılıklarıyla değiştirir |
| `22_urun_gorselleri.sql` | Ürünleri `wwwroot/urun-gorsel/*.webp` dosyalarına bağlar |
| `23_hizli_urun.sql` | Kasa ekranındaki hızlı ürün tuşları |

### Demo verisi (`30`–`31`)

| Dosya | İçerik |
|---|---|
| `30_demo_satis_gecmisi.sql` | Son 30 günün satış geçmişi |
| `31_demo_tedarikci_fatura.sql` | 5 tedarikçi, alış faturaları, SKT/lot partileri |

Bu iki betik `migrate` tarafından varsayılan olarak **dışarıda
bırakılır**; yalnızca `--demo` ile uygulanır. Ayrıntı için
[Demo verisi](#demo-verisi) bölümüne bakılabilir.

---

## Mimari

Üç katman, her birinin tek sorumluluğu vardır:

```
Controller  ──►  Service  ──►  Repository  ──►  SQL Server
   HTTP          iş kuralı      Dapper + SQL
```

### Repository — `Data/Repositories/`

Veritabanına erişen tek katmandır. SQL sorguları C# içinde
`private const string` olarak tutulur; ayrı `.sql` dosyalarına
dağılmaz, böylece sorgu ile çağıran kod yan yana okunur.

Repository iş kuralı içermez ve transaction yönetmez. Transaction'ı açan
servis, `IDbConnection` ve `IDbTransaction` nesnelerini repository'ye
geçirir.

### Service — `Services/`

İş kuralları bu katmandadır. İki tür sınıf bulunur.

**Saf hesaplayıcılar** veritabanına erişmez; girdi alır, çıktı verir. Bu
nedenle doğrudan birim testi yazılabilir:

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

Bu ayrımın nedeni, para hesabı yapan kodun veritabanı olmadan test
edilebilmesidir.

### Controller — `Controllers/`

HTTP isteğini karşılar, servisi çağırır, view döner. İş kuralı içermez.
Yetkilendirme bu katmandadır: `[Authorize(Roles = ...)]`.

---

## Tasarım kararları

### Stok miktarı kolon olarak tutulmaz

`Urun` tablosunda `StokMiktari` alanı yoktur. Bakiye, `StokHareket`
tablosundaki giriş ve çıkışların toplamıdır (`vw_StokBakiye`). Bu sayede
stok farklarının kaynağı her zaman hareket listesinde görünür ve
eşzamanlı iki satış birbirinin sayacını ezemez.

### Sevk sırası FEFO'dur

Raftan önce son kullanma tarihi en yakın parti çıkar; satılan malın
maliyeti o partinin maliyetidir. Son kullanma tarihi olmayan ürünler
sıranın sonunda kalır ve fiilen FIFO ile tüketilir.

Sıralama tek yerde tanımlıdır: `MaliyetRepository.SqlAcikPartiler`
içindeki `ORDER BY`. İlk satırın (`CASE WHEN SonKullanmaTarihi IS NULL`)
kaldırılması sessiz bir hataya yol açar; SQL Server `NULL` değerleri başa
koyduğundan tarihsiz ürünler önce tüketilir.

Gıda ürünlerinde mal kabulde tarih zorunludur
(`Urun.SonKullanmaZorunlu`). Boş bırakılan tarih partiyi sıranın sonuna
taşır.

### Süresi geçmiş parti satılamaz

Satış, parti tüketiminde bugünü geçerlilik günü olarak geçer; süresi
dolmuş partiler sıralamaya girmez. Karşılaştırma `>=` olduğundan son
kullanma günü bugün olan ürün satılabilir, ertesi gün satılamaz.

Filtre sorguya gömülü değil, parametreliktir
(`SqlAcikPartiler` içindeki `@gecerlilikGunu`). Zayi, transfer ve sayım
düzeltmesi `NULL` geçer; bu işlemlerin süresi geçmiş partiye erişmesi
gerekir. Filtre sabit olsaydı ilgili stok ne satılabilir ne düşülebilir
olurdu.

Satış bu nedenle reddedildiğinde kasiyere gerçek sebep bildirilir:
*"satılabilir stok yok, 20 birim son kullanma tarihi geçmiş stok var,
zayi olarak düşülmeli."*

Kasa ekranında ayrıca erken uyarı vardır: sepete giren üründe süresi
geçmiş stok bulunuyorsa satırda kırmızı **SKT** rozeti görünür. Rozet
satışı engellemez — sepetteki mal taze partiden çıkıyor olabilir — amacı
raf kontrolüdür. Kararı `SatisService` verir. Rozeti besleyen sorgu
sepetteki tüm ürünler için tek seferde çalışır
(`SuresiGecmisBakiyeleriAsync`); kasa sıcak yol olduğundan satır başına
sorgu yapılmaz.

### Son kullanma ekranı bir iş listesidir

`/SonKullanma` süresi geçmiş partileri kırmızı, yaklaşanları sarı
gösterir. Süresi geçmiş satırlardaki "zayi'ye al" işlemi partinin
kalanını tek adımda düşer ve satır listeden çıkar. Kasadaki satış reddi
son savunma hattıdır; bu liste sorunun kasaya ulaşmamasını sağlar.

Zayi burada parti bazlıdır (`SayimService.PartiZayiKaydetAsync`). Genel
zayi ürün ve depo alıp FEFO ile ilerlediğinden, ekranda işaretlenen lot
ile düşülen lot farklı olabilirdi.

### Transfer partileri de taşır

Depolar arası transfer yalnızca iki stok hareketi yazmaz; kaynak depodaki
partileri FEFO sırasıyla tüketir ve hedef depoda aynı maliyet, son
kullanma tarihi ve lot ile yeni partiler açar. Aksi halde hedef depoda
bakiye görünür ancak parti bulunmaz ve satış "parti bakiyesi yetersiz"
hatasıyla kesilir.

Tüketilen her parti için hedefte ayrı giriş hareketi yazılır;
`UX_StokParti_Hareket` benzersiz olduğundan bir stok hareketine yalnızca
bir parti bağlanabilir.

### Alış fiyatı KDV hariç, satış fiyatı KDV dahil saklanır

Alış KDV'si indirilebilir olduğundan maliyete girmez; müşteri etiketteki
tutarı öder. `FaturaHesaplayici` KDV'yi matrahın üstüne ekler,
`SepetHesaplayici` tutarın içinden ayrıştırır. Ters yönde çalıştıkları
için ayrı sınıflarda tutulurlar.

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
değeri `1875` olarak bağlanırdı. `Program.cs` invariant kültür zorlar;
ekrandaki gösterim ayrıca biçimlendirilir.

### Diğer

- **Fiş numarası sequence'ten alınır.** `MAX(FisNo)+1` eşzamanlı iki
  satışta aynı numarayı üretir; `FisNoSeq` üretmez.
- **Üçüncü parti dosyalar gömülüdür.** Bootstrap, jQuery, Phosphor Icons,
  ZXing ve Chart.js `wwwroot/lib/` altındadır; CDN kullanılmaz. Kasa
  bilgisayarının internet bağlantısı koptuğunda uygulama çalışmaya devam
  etmelidir.

---

## Modüller

| Modül | Ekran | İşlev |
|---|---|---|
| Kasa | `/Kasa` | Barkod okutma, sepet, hızlı ürün tuşları, askıya alma |
| Ödeme | `/Odeme` | Nakit/kart/karışık ödeme, para üstü, fiş yazdırma |
| İade | `/Iade` | Fişten seçili satır ve miktarın iadesi |
| Vardiya | `/Vardiya` | Vardiya açma/kapama, kasa sayımı, Z raporu |
| Ürün | `/Urun` | Ürün kartı, barkod yönetimi, fiyat geçmişi |
| Stok | `/Stok` | Stok hareketleri, mal kabul |
| Sayım ve Zayi | `/Sayim` | Envanter sayımı, fire kaydı |
| Kampanya | `/Kampanya` | İndirim kuralları |
| Kâr Marjı | `/Maliyet` | Parti maliyeti (FEFO), ürün bazında kâr |
| Raporlar | `/Rapor` | Ciro, en çok satan, ödeme dağılımı, saat yoğunluğu, kritik stok |
| Son Kullanma | `/SonKullanma` | Süresi geçmiş ve yaklaşan partiler, parti bazlı zayi |
| Depo Transferi | `/Transfer` | Depolar arası stok taşıma |
| Tedarikçiler | `/Tedarikci` | Tedarikçi kartları |
| Alış Faturaları | `/AlisFaturasi` | Fatura girişi ve otomatik mal kabul |
| Personel | `/Personel` | Kullanıcı oluşturma, rol değiştirme, şifre sıfırlama |
| İşlem Logları | `/IslemLog` | Hassas işlemlerin denetim kaydı |

Barkod okuma iki yolla çalışır: USB barkod okuyucu (klavye gibi davranır)
ve kamera (ZXing.js, `wwwroot/js/kamera.js`).

---

## Demo verisi

Her iki betik de tekrar çalıştırılabilir ve **üretimde çalıştırılmaz**;
sahte kayıt ürettiklerinden gerçek raporları bozarlar. `migrate`
varsayılan olarak bunları dışarıda bırakır.

### Satış geçmişi

`30_demo_satis_gecmisi.sql` son 30 güne yayılmış satış geçmişi üretir:

- Her gün için bir vardiya (bugünkü açık, önceki 29'u kapalı)
- Günde 8–24 fiş, hafta sonu daha yoğun
- Öğle ve akşam tepe yapan saat dağılımı
- Fiş başına 1–6 satır, temel gıdalar ağırlıklı
- Nakit/kart karışık ödemeler, nakitte para üstü
- Fişlerin bir kısmında iade
- Her satış için stok çıkışı ve FIFO parti tüketimi
- Vardiyaların beşte birinde kasa farkı

Sonuç: yaklaşık 440 fiş, 160.000 TL ciro, 370 TL ortalama sepet.

Ürettiği kayıtlar `DEMO` önekiyle işaretlenir. Yeniden üretmek için
betiğin başındaki `@Temizle` değişkeni `1` yapılır; bu yalnızca kendi
ürettiği kayıtları siler. Üretim tek transaction içinde yapılır.

### Tedarikçi ve fatura zinciri

`31_demo_tedarikci_fatura.sql` şunları oluşturur:

- 5 tedarikçi kartı (`TED001`–`TED005`)
- Alış faturaları (`DEMO-AF-xxx`)
- Her fatura satırı için stok giriş hareketi ve maliyet partisi
- Partilere son kullanma tarihi ve lot numarası

Bu betik olmadan üç ekran boş görünür: Tedarikçiler kart göstermez, Alış
Faturası ekranında seçilecek tedarikçi bulunmaz, Son Kullanma takibinde
tarihli parti olmaz.

Üretilen tarihlerin tamamı gelecektedir; bir kısmı 30 gün içinde dolar.
Bu nedenle Son Kullanma ekranı sarı (yaklaşan) satırlar gösterir, kırmızı
(süresi geçmiş) göstermez. Süresi geçmiş akışını denemek için bir
partinin tarihi geriye alınabilir:

```sql
UPDATE TOP (1) StokParti
SET SonKullanmaTarihi = DATEADD(DAY, -3, CAST(SYSUTCDATETIME() AS DATE))
WHERE KalanMiktar > 0 AND SonKullanmaTarihi IS NOT NULL;
```

Ardından ilgili ürün kasada satılamaz, Son Kullanma ekranında kırmızı
satır olarak listelenir ve zayi'ye alınabilir.

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

Kural sınıflarının veritabanı bilmemesi bilinçlidir; `IadeKurallari`,
`TransferKurallari`, `FaturaHesaplayici` gibi sınıflar girdi alıp karar
döndürdüğünden doğrudan test edilebilir.

Bazı kurallar SQL içinde yaşar ve birim testiyle doğrulanamaz — FEFO
sıralaması ve süresi geçmiş parti filtresi gibi. Bunlar geliştirme
sırasında gerçek veritabanında `ROLLBACK` ile biten betiklerle
sınanmıştır: betik test partileri ekler, sorguyu çalıştırır, sonucu
yazdırır ve iz bırakmadan geri alır.

---

## Klasör yapısı

```
MarketOtomasyon/
├── Controllers/          HTTP uçları
├── Services/             İş kuralları (saf hesaplayıcılar + orkestrasyon)
├── Data/
│   ├── Repositories/     Dapper + SQL
│   ├── Sql/              Şema ve veri betikleri (derlemeye gömülü)
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
Dockerfile                Konteyner imajı (iki aşamalı derleme)
```

---

## Ayarlar

`appsettings.json` içindeki uygulama ayarları:

| Bölüm | Anahtar | Anlamı |
|---|---|---|
| `Satis` | `DepoKodu` | Satış ve iadenin stoğu etkilediği depo (`MRK`) |
| `Satis` | `NegatifStogaIzinVer` | `false` ise stok yetersizse satış reddedilir |
| `Iade` | `SureGun` | Fiş tarihinden kaç gün sonrasına kadar iade kabul edilir (30) |
| `UrunResim` | `ApiTabani` | Open Food Facts uç noktası |
| `UrunResim` | `IstekAraligiMs` | İki API isteği arası bekleme; servis dakikada 15 istekle sınırlı |

Loglama `Serilog` bölümünden yapılandırılır. Günlük dosyalar `Loglar/`
altında tutulur, 30 gün saklanır ve dosya başına 10 MB ile sınırlıdır.
