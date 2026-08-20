# Staj Defteri — 9. Gün

**Proje:** Market Otomasyonu
**Konu:** Ödeme alma, para üstü hesabı ve karışık ödeme

---

## Günün amacı

Önceki günlerde sepet oluşturma, indirim uygulama ve tutar hesaplama tamamlanmıştı. Bu günün hedefi, satışın para tarafını yazmaktı: müşteriden nakit veya kartla ödeme alınması, nakit ödemede para üstünün hesaplanması, bir fişin birden fazla ödeme yöntemiyle kapatılabilmesi ve ödeme yarıda kalırsa sisteme zarar vermeden geri dönülebilmesi.

---

## 1. Ödeme tasarımı: neden birden fazla ödeme kaydı

Gerçek bir markette müşteri her zaman tek yöntemle ödeme yapmaz. 100 TL'lik alışverişin 40 TL'sini nakit, 60 TL'sini kartla ödemek istemesi sık karşılaşılan bir durumdur. Bu nedenle ödeme, fiş üzerinde tek bir alan olarak değil, `Odeme` tablosunda **fişe bağlı birden fazla satır** olarak tasarlandı (bu yapı ikinci günde şema kurulurken planlanmıştı).

Buna bağlı olarak şu kural benimsendi: fiş, kalan borcu sıfırlanana kadar **Beklemede** (Durum 1) kalır, ancak sıfırlandığında **Tamamlandı** (Durum 2) durumuna geçer. Böylece ödeme yarıda kesilse, kasiyer vazgeçse veya elektrik kesilse bile yarı ödenmiş bir satış oluşmaz.

---

## 2. OdemeHesaplayici sınıfı

Ödeme hesapları ve kuralları, veritabanına bağlı olmayan saf bir sınıfta toplandı (`Services/OdemeHesaplayici.cs`). Önceki günlerde olduğu gibi bu ayrım, kuralların birim testlerle doğrudan doğrulanabilmesi için yapıldı.

### Para üstü

Nakit ödemede kasiyer iki ayrı sayı girer: fişe **mahsup edilecek** tutar ve müşteriden **fiilen alınan** tutar. Para üstü elle yazılmaz, bu ikisinin farkından hesaplanır:

```csharp
/// <summary>Nakitte para ustu: alinan - mahsup edilen. Negatif olamaz.</summary>
public static decimal ParaUstuHesapla(decimal tutar, decimal alinanTutar)
{
    var ustu = alinanTutar - tutar;
    return ustu < 0 ? 0 : decimal.Round(ustu, 2, MidpointRounding.AwayFromZero);
}
```

Örnek: müşteri 40 TL'lik kısım için 50 TL uzattıysa para üstü 10 TL'dir. Bu iki alanın ayrı tutulması önemlidir; aksi hâlde kasada fiilen ne kadar nakit bulunması gerektiği hesaplanamaz. (Bu bilgi, üçüncü haftada yazılacak vardiya kapanışında kullanılacaktır.)

### Ödeme doğrulama kuralları

```csharp
public static (bool Gecerli, string? Hata) Dogrula(
    byte tip, decimal tutar, decimal? alinanTutar, decimal kalan)
{
    if (tip is not (TipNakit or TipKart or TipPuan))
        return (false, "Gecersiz odeme tipi.");

    if (tutar <= 0)
        return (false, "Odeme tutari sifirdan buyuk olmalidir.");

    if (kalan <= 0)
        return (false, "Fisin odenmemis bakiyesi yok.");

    if (tutar > kalan)
        return (false, $"Odeme tutari kalan borcu ({kalan:0.00}) asamaz.");

    if (tip == TipNakit)
    {
        if (alinanTutar is null)
            return (false, "Nakit odemede alinan tutar girilmelidir.");

        if (alinanTutar < tutar)
            return (false, "Alinan tutar, mahsup edilecek tutardan az olamaz.");
    }

    return (true, null);
}
```

Kuralların gerekçeleri: Ödeme tutarının kalan borcu aşamaması, fişe borcundan fazla tahsilat işlenmesini engeller (müşterinin fazla verdiği para "ödeme" değil "para üstü"dür). Nakitte alınan tutarın mahsuptan az olamaması ise kasada eksik para kalmasını önler.

---

## 3. OdemeRepository ve OdemeService

`OdemeRepository` yalnızca SQL çalıştırır: ödeme ekleme, fişin ödemelerini listeleme, ödenen toplamı hesaplama, tek ödeme silme ve tüm ödemeleri silme.

`OdemeService` iş kurallarını ve transaction yönetimini üstlenir. Ödeme ekleme akışı şöyledir: açık fiş bulunur, o ana kadar ödenen toplam okunur, kalan hesaplanır, doğrulama yapılır, ödeme kaydı yazılır ve kalan sıfırlandıysa fiş kapatılır.

Burada altıncı günde öğrenilen bir ders tekrar işe yaradı. Ödeme eklendikten sonra yeni toplam okunurken, transaction'ın kendi bağlantısı kullanılmalıdır; ayrı bir bağlantı açılırsa henüz commit edilmemiş satır görünmez ve hesap yanlış çıkar:

```csharp
// Odeme eklendikten sonraki toplam ayni transaction icinden okunur;
// ayri baglanti acilirsa henuz commit edilmemis satiri goremez.
var yeniOdenen = await _odemeRepository.OdenenToplamAsync(conn, tx, fis.Id, ct);
var yeniKalan = OdemeHesaplayici.KalanHesapla(fis.GenelToplam, yeniOdenen);

if (yeniKalan <= 0)
    await _fisRepository.DurumGuncelleAsync(conn, tx, fis.Id, DurumTamamlandi, ct);
```

Ayrıca iki geri dönüş yolu yazıldı:

- **Tek ödemeyi iptal etme:** Yanlış tutar girilmişse o ödeme satırı silinir ve fiş yeniden Beklemede durumuna alınır.
- **Ödemeden tamamen vazgeçme:** Alınan tüm ödemeler silinir, fiş Beklemede'ye döner. Sepet satırlarına dokunulmaz; kasiyer ürün ekleyip çıkarmaya devam edebilir.

---

## 4. Ödeme penceresi arayüzü

Kasa ekranına Bootstrap modal penceresi olarak bir ödeme ekranı eklendi (`Views/Kasa/Index.cshtml` içinde tanımlı, `wwwroot/js/odeme.js` ile yönetiliyor). Pencere F2 tuşuyla veya "Ödeme Al" düğmesiyle açılıyor.

Pencerenin sol tarafında fiş toplamı, ödenen tutar ve büyük puntolu kalan borç; sağ tarafında ödeme tipi seçimi ve tutar alanları bulunuyor. Altta ise o fişe kadar alınan ödemelerin listesi var.

Arayüzde kasiyerin işini hızlandıran birkaç ayrıntı yazıldı:

- Tutar kutusu, kalan borç ile önceden doldurulmuş geliyor. Kasiyer tek yöntemle ödeme alıyorsa hiçbir şey yazmadan Enter'a basması yeterli.
- Nakit seçilince "müşteriden alınan" alanı, kart seçilince "POS onay kodu" alanı görünüyor; ilgisiz alan gizleniyor.
- Alınan tutar boş bırakılırsa müşterinin tam parayı verdiği kabul ediliyor.
- Fiş kapandığında yeni ödeme alınamıyor, pencere yalnızca "Yeni Fiş" düğmesiyle kapanıyor.
- Pencere kapandığında odak otomatik olarak barkod alanına dönüyor.

---

## 5. Karşılaşılan hata: gizlenmeyen indirim satırı

Ekran test edilirken, kasa ekranının sağ panelinde hiç indirim uygulanmamışken bile **"-0,00"** yazan bir satırın göründüğü fark edildi.

**Sebebi:** Satırı gizlemek için HTML'in `hidden` özniteliği kullanılmıştı. Ancak aynı elemanda Bootstrap'ın `d-flex` sınıfı da vardı ve bu sınıf `display: flex !important` tanımladığı için `hidden` özniteliğini geçersiz kılıyordu.

**Çözümü:** Gizleme işlemi Bootstrap'ın kendi `d-none` sınıfıyla yapıldı:

```javascript
// Bootstrap d-flex, hidden ozniteligini ezer; gizleme d-none ile yapilir.
document.getElementById("indirim-satiri").classList.toggle("d-none", sepet.toplamIndirim <= 0);
```

---

## 6. Test ve doğrulama

Ödeme hesapları için 18 yeni birim test yazıldı; projedeki toplam test sayısı 79'a çıktı ve tamamı geçti. Test edilen durumlar: para üstü hesabı, alınan tutarın eksik olması, kalan hesabı, nakitte alınan tutarın zorunluluğu, kartta aranmaması, ödemenin kalan borcu aşamaması, sıfır ve negatif tutar, kapanmış fişe ödeme denemesi, geçersiz ödeme tipi ve kabul senaryosunun adım adım doğrulanması.

Sistem ayrıca gerçek veritabanı üzerinde uçtan uca denendi. 100 TL'lik bir fiş oluşturuldu ve şu adımlar izlendi:

| Adım | Sonuç |
|---|---|
| 40 TL nakit (müşteri 50 TL verdi) | Ödenen 40, kalan 60, para üstü 10, fiş açık |
| Kalan 60 TL kart (onay kodu ile) | Ödenen 100, kalan 0, fiş **Tamamlandı** |
| Kalan 70 TL iken 100 TL ödeme denemesi | Reddedildi |
| 70 TL için 50 TL nakit verilmesi | Reddedildi |
| Kapanmış fişe yeni ödeme | Reddedildi |
| 30 TL'lik ödemenin iptali | Ödenen 0, kalan 100, fiş yeniden Beklemede |

Veritabanı kontrolünde fişin Durum 2 (Tamamlandı) olduğu ve `Odeme` tablosunda biri nakit (alınan ve para üstü bilgisiyle), diğeri kart (onay koduyla) olmak üzere iki ayrı satır bulunduğu doğrulandı.

---

## Günün değerlendirmesi

Ödeme alma tamamlandı; kasa artık bir satışı baştan sona kapatabilmektedir. Bu günün en dikkat gerektiren kısmı, ödemenin "yarım kalabilir" bir işlem olmasıydı: kasiyerin yanlış tutar girmesi, müşterinin vazgeçmesi veya kartın çekmemesi durumunda sistemin tutarsız bir duruma düşmemesi gerekiyordu. Fişin ancak borç tamamen kapandığında kapanması ve her aşamada sepete geri dönülebilmesi bu yüzden önemliydi.

Bu aşamada satılan ürünler henüz stoktan düşmemektedir; satışın stok hareketine dönüşmesi bir sonraki adımın konusudur.
