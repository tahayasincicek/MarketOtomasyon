# Veritabanı

[← README](../README.md)

---

## Kurulum komutları

Şema betikleri uygulama içine gömülüdür ve sırayla uygulanır. Uygulanan
her betik `SemaSurumu` tablosuna adı ve tarihiyle kaydedilir.

| Komut | İşlev |
|---|---|
| `migrate` | Bekleyen betikleri uygular |
| `migrate --demo` | Demo verisi betiklerini de dahil eder |
| `migrate --liste` | Bekleyenleri listeler, hiçbir şey çalıştırmaz |
| `migrate --baseline` | Mevcut şemayı "uygulanmış" işaretler, çalıştırmaz |

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
- **Yarıda kalırsa devam eder.** Bir betik hata verirse komut orada durur
  ve `1` çıkış kodu döner. Öncekiler kayıtlıdır; sorun giderilip komut
  yeniden çalıştırıldığında yalnızca kalanlar uygulanır.
- **Hatalı betik yarım kalmaz.** Her betik kendi transaction'ında çalışır;
  hata durumunda o betiğin değişiklikleri geri alınır.

Yeni bir şema değişikliği için `Data/Sql/` altına sıradaki numarayla bir
dosya eklemek yeterlidir (`14_...sql`).

Bu işlemi yürüten kütüphane **DbUp**'tır. EF Core gibi kod üretmez;
yalnızca `.sql` dosyalarını çalıştırır ve kaydeder.

### Ortama göre davranış

| Ortam | Migration |
|---|---|
| Development | Uygulama açılışında otomatik |
| Production | Otomatik **değil**; ayrı deploy adımı |

Üretimde otomatik çalıştırılmamasının nedeni, birden fazla uygulama
örneğinin aynı anda migration yürütme riski ve yavaş bir betiğin açılışı
kilitlemesidir.

---

## Mevcut bir veritabanının dahil edilmesi

Bu sürümden önce kurulmuş veritabanlarında tablolar bulunur ancak
`SemaSurumu` tablosu yoktur. `migrate` bu durumu algılar ve değişiklik
yapmadan durur; `01_ilk_sema.sql` korumasız `CREATE TABLE` içerdiğinden
dolu bir veritabanında baştan uygulanamaz.

Şemanın güncel olduğu biliniyorsa bir kez:

```bash
dotnet run --project MarketOtomasyon -- migrate --baseline --demo
```

Komut hiçbir betik çalıştırmaz; mevcut şemayı "uygulanmış" olarak
işaretler. `--demo` yalnızca demo verisi zaten yüklüyse eklenir.

---

## Betik listesi

Numara, çalıştırma sırasıdır. Betikler üç bloğa ayrılmıştır; bloklar
arasındaki boşluk, yeni dosya eklendiğinde sonrakileri kaydırma
ihtiyacını ortadan kaldırır.

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
mutlaka sonrasında çalışır.

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

`migrate` bu ikisini varsayılan olarak dışarıda bırakır; yalnızca
`--demo` ile uygulanır.

---

## Demo verisi ayrıntısı

Her iki betik de tekrar çalıştırılabilir ve **üretimde çalıştırılmaz**;
sahte kayıt ürettiklerinden gerçek raporları bozarlar.

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
ürettiği kayıtları siler.

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

Ardından ilgili ürün kasada satılamaz ve Son Kullanma ekranında kırmızı
satır olarak listelenir.

---

## Betiklerin elle çalıştırılması

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
`--baseline` adımı gerekir.

Betikler `CREATE DATABASE` ve `USE` içermez — veritabanı adı bağlantı
dizesinden gelir. Bu nedenle veritabanı önceden oluşturulmalı ve `-d` ile
hedeflenmelidir.

Her betik kendi içinde `SET QUOTED_IDENTIFIER ON` yapar. Bu gereklidir:
`sqlcmd` bu ayarı varsayılan olarak kapalı başlatır ve şemadaki
filtrelenmiş index'ler yüzünden `INSERT` çalışmaz. SSMS ayarı açık
başlattığından sorun orada görünmez.

Şema betikleri tekrar çalıştırılabilir değildir; dolu bir veritabanında
ikinci kez uygulanmamalıdır.
