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
  -v marketotomasyon-keys:/var/lib/marketotomasyon/keys \
  marketotomasyon
```

`host.docker.internal` konteynerden ana makineye işaret eder. Windows
kimlik doğrulaması konteynerde çalışmadığından kullanıcı adı ve şifre
kullanılmalıdır.

`ASPNETCORE_ENVIRONMENT=Production` imajda tanımlıdır.

### Oturum anahtarlarının kalıcı tutulması

ASP.NET Core, giriş çerezlerini Data Protection anahtarlarıyla şifreler.
Docker imajı anahtarları `/var/lib/marketotomasyon/keys` klasörüne yazar;
bu klasör kalıcı bir volume'a bağlanmazsa container yeniden oluşturulduğunda
mevcut kullanıcı oturumları açılamaz.

Volume ilk çalıştırmadan önce oluşturulur:

```bash
docker volume create marketotomasyon-keys
```

Yukarıdaki `docker run` örneği bu volume'u bağlar. Aynı makinedeki birden
fazla uygulama instance'ı aynı volume'u ve `VeriKoruma__UygulamaAdi`
değerini kullanmalıdır. Varsayılan uygulama adı `MarketOtomasyon`dur.

Birden fazla Docker sunucusu kullanılıyorsa yerel named volume yeterli
değildir. Anahtar klasörü bütün sunucuların eriştiği korumalı bir ağ
dosya sistemine bağlanmalı veya Redis/Azure Blob gibi ortak bir Data
Protection sağlayıcısı kullanılmalıdır. Anahtar dosyaları oturumları
çözebildiği için yedeklenmeli, yalnızca uygulama kullanıcısına açık olmalı
ve sır gibi korunmalıdır.

---

## Ters proxy ve HTTPS

Uygulama Nginx, Cloudflare veya bir yük dengeleyici arkasındaysa TLS
genellikle proxy üzerinde sonlanır. Proxy uygulamaya HTTP ile bağlandığı
için `X-Forwarded-Proto` işlenmezse uygulama isteği tekrar HTTPS'e
yönlendirir ve yönlendirme döngüsü oluşabilir.

Özellik varsayılan olarak kapalıdır. Aynı makinedeki Nginx için örnek:

```bash
TersProxy__Etkin=true
TersProxy__GuvenilenProxyler__0=127.0.0.1
TersProxy__GuvenilenProxyler__1=::1
```

Docker ağı gibi adresi değişebilen ortamlarda tek IP yerine gerçek ağ
aralığı verilir:

```bash
TersProxy__Etkin=true
TersProxy__GuvenilenAglar__0=172.18.0.0/16
```

Buradaki CIDR örnektir; Docker/hosting ağının gerçek aralığı
kullanılmalıdır. Yanlış adres veya boş güven listesi uygulamayı açılışta
durdurur. Böylece istemcinin kendi gönderdiği sahte
`X-Forwarded-Proto: https` başlığına yanlışlıkla güvenilmez.

Yalnızca uygulama portuna internetten doğrudan erişim güvenlik duvarıyla
kesin olarak engelliyse bütün proxy kaynakları açıkça kabul edilebilir:

```bash
TersProxy__Etkin=true
TersProxy__TumProxylereGuven=true
```

Nginx'in en az şu başlıkları iletmesi gerekir:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
```

Middleware `UseHsts` ve `UseHttpsRedirection` çağrılarından önce çalışır.
Varsayılan topoloji tek proxy kabul eder; proxy zinciri kullanılıyorsa
`ForwardLimit` kodda bilinçli biçimde artırılmalıdır.

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
