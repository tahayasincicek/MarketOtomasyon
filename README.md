# Market Otomasyon

Küçük ölçekli bir market için kasa, stok, iade ve raporlama uygulaması.

**ASP.NET Core MVC (.NET 8) · Dapper · SQL Server**

### Canlı demo

**https://marketotomasyon-z0yo.onrender.com**

Giriş bilgileri açılış ekranında yazılıdır; tıklayınca alanlar dolar.

> Ücretsiz sunucuda çalışıyor: 15 dakika işlem görmezse uykuya geçiyor,
> o durumda ilk açılış bir dakikayı bulabilir. Sonraki sayfalar hızlıdır.

---

## Kurulum

**Gerekenler:** .NET 8 SDK, SQL Server 2019+ (Express yeterli)

```bash
git clone https://github.com/tahayasincicek/MarketOtomasyon.git
cd MarketOtomasyon
copy MarketOtomasyon\appsettings.Development.json.ornek MarketOtomasyon\appsettings.Development.json
dotnet run --project MarketOtomasyon
```

Veritabanı yoksa oluşturulur ve tablolar otomatik kurulur.
Uygulama `https://localhost:7037` adresinde açılır.

Örnek satış geçmişi ve tedarikçi verisi için (isteğe bağlı):

```bash
dotnet run --project MarketOtomasyon -- migrate --demo
```

SQL Server farklı bir sunucudaysa `appsettings.Development.json`
içindeki bağlantı dizesi düzenlenir.

### Giriş

| Kullanıcı | Şifre | Rol |
|---|---|---|
| `mudur` | `Mudur123!` | Tüm ekranlar |
| `kasiyer1` | `Kasiyer123!` | Kasa, iade, vardiya |

---

## Ekranlar

| Ekran | Yol | İşlev |
|---|---|---|
| Kasa | `/Kasa` | Barkod okutma, sepet, askıya alma |
| Ödeme | `/Odeme` | Nakit/kart, para üstü, fiş |
| İade | `/Iade` | Fişten satır iadesi |
| Vardiya | `/Vardiya` | Vardiya açma/kapama, Z raporu |
| Ürün | `/Urun` | Ürün kartı, barkod, fiyat geçmişi |
| Stok | `/Stok` | Stok hareketleri, mal kabul |
| Sayım ve Zayi | `/Sayim` | Envanter sayımı, fire |
| Kampanya | `/Kampanya` | İndirim kuralları |
| Kâr Marjı | `/Maliyet` | Parti maliyeti, ürün bazında kâr |
| Raporlar | `/Rapor` | Ciro, en çok satan, kritik stok |
| Son Kullanma | `/SonKullanma` | Süresi geçmiş ve yaklaşan partiler |
| Depo Transferi | `/Transfer` | Depolar arası stok taşıma |
| Tedarikçiler | `/Tedarikci` | Tedarikçi kartları |
| Alış Faturaları | `/AlisFaturasi` | Fatura girişi, otomatik mal kabul |
| Personel | `/Personel` | Kullanıcı ve rol yönetimi |
| İşlem Logları | `/IslemLog` | Denetim kaydı |
| Hesabım | `/Profil` | Kendi bilgileri, şifre değiştirme |

Barkod hem USB okuyucuyla hem kamerayla okunur. Dar ekranlarda tablolar
ikincil sütunlarını gizler; o bilgi satırın altında görünür.

---

## Testler

```bash
dotnet test
```

329 test. Veritabanı gerektirmez.

---

## Ayrıntılı belgeler

| Belge | İçerik |
|---|---|
| [Mimari](docs/mimari.md) | Katmanlar, tasarım kararları, iş kuralları |
| [Veritabanı](docs/veritabani.md) | Şema betikleri, migration, demo verisi |
| [Üretim](docs/uretim.md) | Deploy, Docker, güvenlik ayarları |

---

## Proje hakkında

Veritabanı erişimi ORM ile değil elle yazılmış SQL ile yapılır; amaç
sorguların ne yaptığının görünür kalmasıdır. Stok miktarı kolon olarak
tutulmaz, hareketlerden hesaplanır. Parti maliyeti ve sevkiyat sırası
FEFO'dur.

Bu kararların gerekçeleri [docs/mimari.md](docs/mimari.md) içindedir.
