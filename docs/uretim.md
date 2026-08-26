# Üretime çıkarma

[← README](../README.md)

---

## Zorunlu iki ayar

Üretimde iki değer `appsettings` dosyalarından değil, ortamdan gelir.

### 1. Bağlantı dizesi

`ConnectionStrings__MarketDb` ortam değişkenine yazılır. Çift alt çizgi,
iç içe ayar anahtarının (`ConnectionStrings:MarketDb`) ortam değişkeni
karşılığıdır.

```powershell
setx ConnectionStrings__MarketDb "Server=sunucu;Database=MarketOtomasyon;User Id=market;Password=...;TrustServerCertificate=True"
```

Bağlantı dizesi hiçbir kaynakta bulunamazsa uygulama açılış anında
anlamlı bir hatayla durur; ilk isteğe kadar beklemez.

### 2. Ortam adı

`ASPNETCORE_ENVIRONMENT=Production` olmalıdır. Bu ayar dört davranışı
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
istekte oturum çerezi şifresiz iletilir ve ağı dinleyen biri oturumu
devralabilir.

---

## Ayar dosyaları

| Dosya | İçerik | Depoda |
|---|---|---|
| `appsettings.json` | Ortak ayarlar, bağlantı dizesi **yok** | Evet |
| `appsettings.Development.json.ornek` | Şablon | Evet |
| `appsettings.Development.json` | Yerel bağlantı dizesi | Hayır |
| `appsettings.Production.json` | Log seviyesi, `AllowedHosts` | Evet |

`appsettings.json` her ortamda yüklenir ve depoda saklanır. Üretim
kimlik bilgileri oraya yazılırsa kaynak koda girer ve commit sonrası
geçmişten temizlenmesi zorlaşır.

`appsettings.Production.json` dosyasına bağlantı dizesi yazılırsa
`AyarDosyalariTests` başarısız olur; sırların kaynak koda sızması
derleme aşamasında yakalanır.

`AllowedHosts` değeri kendi alan adıyla değiştirilmelidir; `*` bırakmak
Host başlığı sahteciliğine açık bırakır.

---

## Deploy sırası

```bash
# 1. Şemayı güncelle
dotnet MarketOtomasyon.dll migrate

# 2. Çıkış kodu 0 ise uygulamayı başlat
dotnet MarketOtomasyon.dll
```

Migration üretimde otomatik değildir. Uygulama ayağa kalkarken şema
değiştirmek, birden fazla örneğin aynı anda migration çalıştırmasına ve
yavaş bir betiğin açılışı kilitlemesine yol açar.

`migrate` bir betikte hata verirse `1` döner ve orada durur; öncekiler
kayıtlıdır. Ayrıntı için [veritabanı belgesi](veritabani.md).

---

## Docker

Kök dizindeki `Dockerfile` uygulamayı iki aşamada paketler: SDK imajında
derleyip yayınlar, ardından yalnızca ASP.NET çalışma zamanı imajına
kopyalar. Son imajda derleyici ve kaynak kod bulunmaz.

```bash
docker build -t marketotomasyon .
docker run -p 8080:8080 marketotomasyon
```

Veritabanı imajın içinde değildir. Konteyner içinde `localhost` kendi ağ
alanına işaret ettiğinden bağlantı dizesi dışarıdan verilir:

```bash
docker run -p 8080:8080 \
  -e "ConnectionStrings__MarketDb=Server=host.docker.internal;Database=MarketOtomasyon;User Id=sa;Password=...;TrustServerCertificate=True" \
  marketotomasyon
```

`host.docker.internal` konteynerden ana makineye işaret eder. Windows
kimlik doğrulaması konteynerde çalışmadığından kullanıcı adı ve şifre
kullanılmalıdır.

`ASPNETCORE_ENVIRONMENT=Production` imajda tanımlıdır.

---

## Bilinen eksikler

Yukarıdaki ayarlar deploy için gerekli minimumdur, yeterli değildir.
Gerçek bir markette kullanılmadan önce şunlar giderilmelidir:

| Eksik | Risk |
|---|---|
| Varsayılan hesap şifreleri | `mudur` / `kasiyer1` şifreleri belgelerde ve kurulum betiğinde yazılı |
| Giriş deneme sınırı yok | Başarısız denemeler loglanır ancak engellenmez |
| Yedekleme stratejisi tanımsız | Veri kaybı senaryosu için plan yok |
| Sağlık kontrolü ucu yok | Yük dengeleyici uygulamanın ayakta olduğunu anlayamaz |
