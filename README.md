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

Yerel ayar dosyanı şablondan oluştur:

```powershell
copy MarketOtomasyon\appsettings.Development.json.ornek MarketOtomasyon\appsettings.Development.json
```

İçindeki bağlantı dizesini kendi kurulumuna göre düzenle:

```json
"ConnectionStrings": {
  "MarketDb": "Server=localhost;Database=MarketOtomasyon;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
}
```

Farklı bir sunucu kullanıyorsan `Server=` kısmını değiştir. SQL Server
kimlik doğrulaması gerekiyorsa `Trusted_Connection=True` yerine
`User Id=...;Password=...` yaz.

Kopya `.gitignore`'dadır — herkesin sunucu adı ve kimlik doğrulama biçimi
farklı olabilir, bu ayar kişiye özeldir.

**Neden `appsettings.json` değil:** o dosya her ortamda yüklenir ve
repoda durur. Üretim kullanıcı adı ve şifresi oraya yazılsaydı kaynak
koda girer, bir kez commit'lendikten sonra geçmişten temizlenmesi
zorlaşırdı. Üretimde bağlantı dizesi ortam değişkeninden okunur —
"Üretime çıkarken" bölümüne bak.

Bağlantı dizesi hiçbir yerde bulunamazsa uygulama **açılışta** anlamlı
bir hatayla durur; ilk isteği bekleyip giriş ekranında patlamaz.

Sonra çalıştır:

```bash
dotnet run --project MarketOtomasyon
```

Veritabanı yoksa oluşturulur ve kurulum betikleri kendiliğinden uygulanır;
ayrı bir kurulum adımı yok. Demo verisi istiyorsan bir kez şunu çalıştır:

```bash
dotnet run --project MarketOtomasyon -- migrate --demo
```

Uygulama `https://localhost:7037` adresinde açılır.

> Bu sürümden **önce** kurduğun bir veritabanın varsa bir kerelik bir
> adım gerekiyor: "Elle kurulmuş bir veritabanın varsa" bölümüne bak.

Geliştirme hesapları:

| Kullanıcı | Şifre | Rol |
|---|---|---|
| `mudur` | `Mudur123!` | Müdür — tüm ekranlar |
| `kasiyer1` | `Kasiyer123!` | Kasiyer — kasa, iade, vardiya |

### Üretime çıkarken

Üretimde iki şey `appsettings` dosyalarından gelmez, ortamdan gelir.

**1. Bağlantı dizesi.** `ConnectionStrings__MarketDb` ortam değişkenine
yazılır. Çift alt çizgi, iç içe ayar anahtarının (`ConnectionStrings:MarketDb`)
ortam değişkeni karşılığıdır:

```powershell
setx ConnectionStrings__MarketDb "Server=sunucu;Database=MarketOtomasyon;User Id=market;Password=...;TrustServerCertificate=True"
```

`appsettings.Production.json` içinde bu anahtar bilerek yok; dosya yalnızca
log seviyesi ve izin verilen alan adlarını taşır. Oraya bağlantı dizesi
yazılırsa bir birim testi (`AyarDosyalariTests`) düşer — sırların kaynak
koda sızmasını derleme aşamasında yakalamak için.

**2. Ortam adı.** `ASPNETCORE_ENVIRONMENT=Production` olmalı. Bu ayar üç
şeyi birden değiştirir:

| Ayar | Development | Production |
|---|---|---|
| Oturum çerezi | HTTP'de de gönderilir | **Yalnızca HTTPS** (`Secure`) |
| HSTS | kapalı | açık |
| Log seviyesi | `Debug` | `Information` |

Çerez politikası ayrımı önemli: geliştirmede `http://localhost` ile
çalışıldığı için çerez `Secure` işaretlenemez, yoksa giriş yapılamaz.
Üretimde ise işaretlenmezse, HTTP'ye düşen tek bir istekte oturum çerezi
şifresiz gider ve ağı dinleyen biri müdür oturumunu devralabilir.

`AllowedHosts` değerini de kendi alan adınla değiştir; `*` bırakmak Host
başlığı sahteciliğine açık bırakır.

> Bu iki madde deploy için gerekli minimumdur, yeterli değil. Varsayılan
> `mudur` / `kasiyer1` hesaplarının şifreleri README'de ve kurulum
> betiğinde yazılıdır — üretime çıkmadan mutlaka değiştirilmeli. Giriş
> ekranında deneme sınırı da yok; şifre deneyen biri engellenmiyor,
> yalnızca loglanıyor.

### Docker ile çalıştırma

Kök dizindeki `Dockerfile` uygulamayı iki aşamada paketler: SDK imajında
derleyip yayınlar, sonra yalnızca ASP.NET çalışma zamanı imajına kopyalar.
Böylece son imajda derleyici ve kaynak kod bulunmaz.

```bash
docker build -t marketotomasyon .
docker run -p 8080:8080 marketotomasyon
```

**Veritabanı imajın içinde değildir.** Konteynerdeki uygulama `localhost`
dediğinde kendi içini kastedeceği için, bağlantı dizesini dışarıdan
vermen gerekir:

```bash
docker run -p 8080:8080 \
  -e "ConnectionStrings__MarketDb=Server=host.docker.internal;Database=MarketOtomasyon;User Id=sa;Password=...;TrustServerCertificate=True" \
  marketotomasyon
```

`host.docker.internal` konteynerden ana makineye işaret eder. Windows
kimlik doğrulaması konteynerde çalışmadığı için `Trusted_Connection`
yerine kullanıcı adı/şifre kullanılmalı.

---

## SQL dosyalarının çalıştırma sırası

**Numara = çalıştırma sırası.** Betikleri elle çalıştırman gerekmiyor —
`migrate` komutu bunu yapıyor (aşağıda) — ama sırayı bilmek dosyaları
okurken ve yenisini eklerken işe yarar.

Hepsi `MarketOtomasyon/Data/Sql/` altında. Numaralar üç bloğa ayrılmış:

| Blok | Aralık | Ne yapar |
|---|---|---|
| Şema | `01`–`13` | Tabloları, index'leri ve view'ları kurar |
| Örnek veri | `20`–`23` | Ürün kataloğu ve onu zenginleştiren betikler |
| Demo verisi | `30`–`31` | İsteğe bağlı; ekranları dolduran sahte geçmiş |

Bloklar arasındaki boşluk bilinçli: yeni bir şema betiği eklendiğinde
`14` olur, sonraki dosyaları kaydırmak gerekmez.

### 1. Şema

| Dosya | Ne yapar |
|---|---|
| `01_ilk_sema.sql` | Çekirdek şema: ürün, barkod, fiyat, stok, vardiya, fiş, ödeme tabloları |
| `02_askiya_alma.sql` | Fişe `Askida` kolonu — kasada bekleyen sepetler |
| `03_kampanya.sql` | Kampanya başlığı, koşulları ve sonuçları |
| `04_iade.sql` | `Iade` / `IadeSatir` tabloları, `FisSatir.IadeEdilenMiktar` |
| `05_vardiya.sql` | İadenin vardiyaya bağlanması |
| `06_urun_resim.sql` | Ürün görselinin dosya yolu ve kaynağı |
| `07_sayim_zayi.sql` | Sayım ve zayi kayıtları |
| `08_maliyet.sql` | Parti maliyeti: `StokParti`, `StokPartiTuketim` |
| `09_yetkilendirme_log.sql` | `IslemLog` tablosu, geliştirme hesaplarının şifre hash'leri |
| `10_mudur_onayi.sql` | `IslemLog`'a onaylayan müdür ve onay sebebi kolonları |
| `11_skt_lot.sql` | `StokParti`ye son kullanma tarihi, lot ve tedarikçi; `Urun`a SKT zorunluluk bayrağı |
| `12_depo_transfer.sql` | `StokTransfer` / `StokTransferSatir`, transfer numarası sequence'i |
| `13_tedarikci_fatura.sql` | `Tedarikci`, `AlisFaturasi` / `AlisFaturasiSatir`; `StokParti`ye tedarikçi ve fatura satırı bağlantısı |

### 2. Örnek veri

`20_ornek_veri.sql` kataloğu kurar; `21`–`23` onu zenginleştirir ve
**mutlaka ondan sonra** çalışmalıdır — önce çalıştırılırlarsa
güncelleyecek satır bulamaz ve sessizce hiçbir şey yapmazlar.

| Dosya | Ne yapar |
|---|---|
| `20_ornek_veri.sql` | 2 depo, 2 kullanıcı, 6 kategori, 30 ürün, barkodlar, açılış fiyatları ve açılış stokları |
| `21_gercek_barkodlar.sql` | Uydurma barkodları Open Food Facts'te karşılığı olan gerçek barkodlarla değiştirir |
| `22_urun_gorselleri.sql` | Ürünleri `wwwroot/urun-gorsel/*.webp` dosyalarına bağlar |
| `23_hizli_urun.sql` | Kasa ekranındaki hızlı ürün tuşlarını tanımlar |

### 3. Demo verisi (isteğe bağlı)

| Dosya | Ne yapar |
|---|---|
| `30_demo_satis_gecmisi.sql` | Son 30 günün satış geçmişi: vardiyalar, fişler, ödemeler, iadeler |
| `31_demo_tedarikci_fatura.sql` | 5 tedarikçi ve alış faturaları; her fatura satırı için stok girişi ve maliyet/SKT/lot partisi |

Bu ikisi olmadan uygulama çalışır ama birçok ekran boş görünür: raporlar
ve kâr marjı geçmiş satış ister, alış faturası ekranı tedarikçi ister,
Son Kullanma takibi ise tarihli parti ister. Ayrıntı için aşağıdaki
"Demo verisi" bölümüne bak.

### Kurulum komutu

Betikleri elle çalıştırmana gerek yok. Uygulama onları kendi içinde
taşır ve sırayla uygular:

```bash
dotnet run --project MarketOtomasyon -- migrate
```

Demo verisini de istiyorsan:

```bash
dotnet run --project MarketOtomasyon -- migrate --demo
```

Neyin beklediğini görmek için (hiçbir şey çalıştırmaz):

```bash
dotnet run --project MarketOtomasyon -- migrate --liste
```

**Geliştirmede bu komutu çalıştırmayı unutsan da olur:** `dotnet run`
ile uygulama açılırken bekleyen betikler kendiliğinden uygulanır. Depoyu
klonlayıp doğrudan çalıştırabilmen için böyle.

Üretimde ise **otomatik değildir** — orada `migrate` ayrı bir deploy
adımıdır. Uygulama ayağa kalkarken şema değiştirmek, birden fazla
örneğin aynı anda migration çalıştırmasına ve yavaş bir betiğin açılışı
kilitlemesine yol açar.

### Hangi betiğin uygulandığı nasıl bilinir

Uygulanan her betik `SemaSurumu` tablosuna adı ve tarihiyle yazılır.
Bunun üç sonucu var:

- **Komut tekrar çalıştırılabilir.** İkinci kez çalıştırınca "veritabanı
  güncel" der ve hiçbir şey yapmaz.
- **Yarıda kalırsa kaldığı yerden devam eder.** Bir betik patlarsa komut
  orada durur ve hata verir; ondan öncekiler kaydedilmiş olur. Sorunu
  düzeltip komutu yeniden çalıştırdığında yalnızca kalanlar uygulanır.
- **Patlayan betik yarım kalmaz.** Her betik kendi transaction'ında
  çalışır; hata alırsa o betiğin yaptığı her şey geri alınır.

Yeni bir şema değişikliği eklemek için `Data/Sql/` altına sıradaki
numarayla bir dosya koyman yeterli — `14_...sql` gibi. Kayıt tutma ve
sıralama kendiliğinden çalışır.

> Betik içerikleri değişmedi: hâlâ ham SQL, hâlâ okunabilir. Değişen tek
> şey nasıl çalıştırıldıkları. Bunu yapan kütüphane **DbUp**; EF Core
> gibi kod üretmez, yalnızca `.sql` dosyalarını çalıştırıp kaydeder.

### Elle kurulmuş bir veritabanın varsa

Bu sürümden önce kurduğun bir veritabanında tablolar var ama `SemaSurumu`
tablosu yok. `migrate` bu durumu fark eder ve dokunmadan durur, çünkü
betikleri baştan uygulamak "tablo zaten var" hatası verirdi.

Şemanın güncel olduğundan eminsen bir kez şunu çalıştır:

```bash
dotnet run --project MarketOtomasyon -- migrate --baseline --demo
```

Bu komut **hiçbir betik çalıştırmaz**; mevcut şemayı "uygulanmış" olarak
işaretler. Sonrasında normal `migrate` akışı devreye girer. (`--demo`
yalnızca demo verisi zaten yüklüyse eklenir.)

### Betikleri elle çalıştırmak

Gerekirse hâlâ mümkün — dosyalar `MarketOtomasyon/Data/Sql/` altında
duruyor ve dosya adı sırası çalıştırma sırasıdır:

```powershell
$sql = "MarketOtomasyon\Data\Sql"
Get-ChildItem "$sql\*.sql" | Where-Object { $_.Name -notmatch '^(3\d|92)_' } |
  Sort-Object Name | ForEach-Object {
    sqlcmd -S localhost -d MarketOtomasyon -b -i $_.FullName
    if ($LASTEXITCODE -ne 0) { Write-Error "Durdu: $($_.Name)"; break }
}
```

Bu yolda sürüm takibi olmaz; `migrate` sonradan çalıştırılırsa yukarıdaki
`--baseline` adımı gerekir. Betikler artık `CREATE DATABASE` ve `USE`
içermediği için veritabanını önceden oluşturup `-d` ile hedeflemelisin.

Şema betikleri tekrar çalıştırılabilir değildir (`01_ilk_sema.sql`
korumasız `CREATE TABLE` içerir); dolu bir veritabanında ikinci kez
çalıştırma.

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
- `FifoMaliyetHesaplayici` — verilen sıradaki partilerden ne kadar tüketilir
- `PartiKurallari` — son kullanma tarihi ve lot doğrulaması
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

**Sevk sırası FEFO'dur.** Raftan önce son kullanma tarihi en yakın
parti çıkar; satılan malın maliyeti de o partinin maliyetidir. Son
kullanma tarihi olmayan ürünler (kırtasiye, züccaciye) sıranın sonunda
kalır ve fiilen FIFO ile tüketilir.

Sıralama tek bir yerde, `MaliyetRepository.SqlAcikPartiler` içindeki
`ORDER BY`da tanımlıdır. İlk satırı (`CASE WHEN SonKullanmaTarihi IS NULL`)
silmek sessiz bir hataya yol açar: SQL Server `NULL`'ları başa koyar ve
tarihsiz ürünler, yarın bozulacak sütten önce tüketilir.

Gıda ürünlerinde mal kabulde tarih zorunludur (`Urun.SonKullanmaZorunlu`).
Boş bırakılan tarih partiyi sıranın sonuna atar; yani unutmak, ürünü "en
son satılacak" partiye çevirir.

**Süresi geçmiş parti satılamaz.** Satış, partileri tüketirken bugünü
geçerlilik günü olarak verir; süresi dolmuş partiler sıralamaya hiç
girmez. Sınır `>=` olduğu için **son kullanma günü bugün olan ürün hâlâ
satılabilir**, ertesi gün satılamaz.

Bu filtre sorguya gömülü değil, parametrelidir
(`MaliyetRepository.SqlAcikPartiler` içindeki `@gecerlilikGunu`). Zayi,
transfer ve sayım düzeltmesi `NULL` geçer, çünkü onların süresi geçmiş
partiye erişebilmesi gerekir. Filtre sabit olsaydı o stok ne satılabilir
ne düşülebilir olurdu; temizlemek istediğiniz şeyi temizleyemezdiniz.

Satış bu yüzden reddedildiğinde kasiyer "stok yok" değil, gerçek sebebi
görür: *"satılabilir stok yok, 20 birim son kullanma tarihi geçmiş stok
var, zayi olarak düşülmeli."* Aksi halde kasiyer ekranda 20 adet görüp
satamaz ve nedenini anlayamazdı.

Kasada uyarı daha erken çıkar: sepete giren bir üründe süresi geçmiş
stok varsa satırda kırmızı **SKT** rozeti belirir. Bu rozet satışı
**engellemez** — sepetteki mal taze partiden çıkıyor olabilir; amaç
kasiyerin rafa bakması. Kararı veren yer hâlâ `SatisService`. Rozeti
besleyen sorgu sepetteki tüm ürünler için tek seferde çalışır
(`SuresiGecmisBakiyeleriAsync`); kasa sıcak yol olduğu için satır başına
sorgu atılmaz.

**Son kullanma ekranı bir iş listesidir**, rapor değil. `/SonKullanma`
süresi geçmiş partileri kırmızı, yaklaşanları sarı gösterir; süresi
geçmiş her satırın yanındaki "zayi'ye al" o partinin kalanını tek adımda
düşer ve satır listeden kaybolur. Kasadaki satış reddi son savunma
hattıdır; bu liste sorunun kasaya hiç ulaşmaması içindir.

Zayi burada **parti bazlıdır** (`SayimService.PartiZayiKaydetAsync`).
Genel zayi ürün + depo alıp FEFO ile ilerler; kullanıcının ekranda
işaretlediği lot ile sistemin düşürdüğü lot ayrı olabilirdi. Burada
raftan çekilen parti neyse kayıt da onu düşer.

**Transfer partileri de taşır.** Depolar arası transfer yalnızca iki stok
hareketi yazmaz: kaynak depodaki partileri FEFO sırasıyla tüketir ve hedef
depoda aynı maliyet, son kullanma tarihi ve lot ile yeni partiler açar.
Aksi halde hedef depoda bakiye görünür ama parti bulunmaz ve satış
"parti bakiyesi yetersiz" hatasıyla kırılır.

Tüketilen her parti için hedefte **ayrı** giriş hareketi yazılır, çünkü
`UX_StokParti_Hareket` benzersizdir: bir stok hareketine yalnızca bir parti
bağlanabilir.

**Alış fiyatı KDV hariç, satış fiyatı KDV dahil saklanır.** Alış KDV'si
indirilebilir olduğu için maliyete girmez; müşteri ise etiketteki tutarı
öder. `FaturaHesaplayici` KDV'yi matrahın üstüne ekler, `SepetHesaplayici`
tutarın içinden ayrıştırır — birbirinin tersi yönde çalışırlar, bu yüzden
ayrı dosyalarda tutulurlar.

**Alış faturası mal kabulü sarmalar, değiştirmez.** Fatura kaydedilince
her satır aynı transaction içinde stok hareketi ve FEFO partisi oluşturur
(`StokService.MalKabulYazAsync`). Böylece stok ile belge hiçbir durumda
birbirinden ayrılamaz: bir satırda hata olursa tüm fatura geri alınır.

Tedarikçi ve alış faturası modülü bilinçli olarak dardır: cari hesap,
ödeme takibi, sipariş ve irsaliye **kapsam dışıdır**. Bunlar eksik değil,
kapsam dışı — yarım bir cari hesap, bakiyesi yanlış çıkan bir ekrandan
beterdir.

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
| Kâr Marjı | `/Maliyet` | Parti maliyeti (FEFO), ürün bazında kâr |
| Raporlar | `/Rapor` | Günlük ciro, en çok satan, ödeme dağılımı, saat yoğunluğu, kritik stok |
| Son Kullanma | `/SonKullanma` | Süresi geçmiş ve yaklaşan partiler; parti bazlı zayi |
| Depo Transferi | `/Transfer` | Depolar arası stok taşıma (partileriyle birlikte) |
| Tedarikçiler | `/Tedarikci` | Tedarikçi kartları |
| Alış Faturaları | `/AlisFaturasi` | Fatura girişi ve otomatik mal kabul |
| Personel | `/Personel` | Kullanıcı oluşturma, rol değiştirme, pasifleştirme, şifre sıfırlama |
| İşlem Logları | `/IslemLog` | Hassas işlemlerin denetim kaydı |

Barkod okuma iki yolla çalışır: USB barkod okuyucu (klavye gibi davranır)
ve kamera (ZXing.js ile, `wwwroot/js/kamera.js`).

---

## Demo verisi

`30_demo_satis_gecmisi.sql` son 30 güne yayılmış gerçekçi bir satış geçmişi üretir:

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

### Modül demo verileri

`31_demo_tedarikci_fatura.sql` satış geçmişini değil, tedarikçi ve fatura
zincirini doldurur:

- 5 tedarikçi kartı (`TED001`–`TED005`)
- Her tedarikçiden alış faturaları (`DEMO-AF-xxx`)
- Her fatura satırı için stok giriş hareketi ve maliyet partisi
- Partilere son kullanma tarihi ve lot numarası

Bu betik olmadan üç ekran boş görünür: **Tedarikçiler** hiç kart
göstermez, **Alış Faturası** ekranında seçilecek tedarikçi bulunmaz ve
partilerde tarih olmadığı için **Son Kullanma** takibi boş kalır.

Tarihler bugüne göre üretilir ve tamamı **gelecektedir**; bir kısmı 30
gün içinde dolar. Yani Son Kullanma ekranı bu veriyle sarı (yaklaşan)
satırlar gösterir, kırmızı (süresi geçmiş) göstermez.

Süresi geçmiş akışını ve "zayi'ye al" butonunu denemek için bir partinin
tarihini elle geriye alman gerekir:

```sql
UPDATE TOP (1) StokParti
SET SonKullanmaTarihi = DATEADD(DAY, -3, CAST(SYSUTCDATETIME() AS DATE))
WHERE KalanMiktar > 0 AND SonKullanmaTarihi IS NOT NULL;
```

Bundan sonra o ürün kasada satılamaz — satış "son kullanma tarihi geçmiş
stok var, zayi olarak düşülmeli" uyarısı verir; Son Kullanma ekranında
kırmızı satır olarak çıkar ve tek tıkla zayi'ye alınabilir.

`30_demo_satis_gecmisi.sql` gibi tekrar çalıştırılabilir ve **üretimde
çalıştırılmaz.**

---

## Testler

```bash
dotnet test
```

238 test, `MarketOtomasyon.Tests/` altında. Hiçbiri veritabanı
gerektirmez — hepsi saf kural ve hesaplayıcı sınıflarını hedefler.

Kapsanan alanlar: sepet toplamları ve KDV, kampanya seçimi, iade
kuralları, sayım düzeltmesi, para üstü, FIFO maliyet, terazi barkodu
çözümleme, fatura hesaplama, tedarikçi ve transfer kuralları, müdür
onayı, yetkilendirme, son kullanma sınıflandırması.

Kural sınıflarının veritabanı bilmemesi bilinçli: `IadeKurallari`,
`TransferKurallari`, `FaturaHesaplayici` gibi sınıflar sadece girdi alıp
karar döndürür, bu yüzden doğrudan test edilebilirler.

Bazı kurallar SQL'de yaşar ve birim testiyle doğrulanamaz — FEFO
sıralaması ve süresi geçmiş parti filtresi gibi. Bunlar geliştirme
sırasında gerçek veritabanında `ROLLBACK` ile biten betiklerle sınandı:
betik test partileri ekler, sorguyu çalıştırır, sonucu yazdırır ve hiçbir
iz bırakmadan geri alır.

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
Dockerfile                Konteyner imajı (iki aşamalı derleme)
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
