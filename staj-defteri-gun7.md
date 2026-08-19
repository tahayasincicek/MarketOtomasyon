# Staj Defteri — 7. Gün

**Proje:** Market Otomasyonu
**Konu:** Kasa ekranı arayüzü, AJAX ile sepet yönetimi ve klavye kısayolları

---

## Günün amacı

Bir önceki gün sepetin arka plan altyapısı (fiş açma, satır ekleme, KDV hesaplama) tamamlanmış, ancak kullanıcı arayüzü yazılmamıştı; işlemler yalnızca JSON uç noktaları üzerinden yapılabiliyordu. Bu günün hedefi, kasiyerin gerçekten kullanacağı kasa ekranını geliştirmekti.

Ekranın temel gereksinimi şuydu: kasiyer barkodu okuttuğunda ürün anında sepete düşmeli ve **sayfa yenilenmemelidir**. Bir markette kasiyer saniyede birkaç ürün okutur; her okutmada sayfanın yeniden yüklenmesi hem yavaş olur hem de barkod alanındaki imleç kaybolacağı için kasiyerin sürekli fareye uzanması gerekirdi.

---

## 1. Ekran tasarımı

`Views/Kasa/Index.cshtml` dosyasında Bootstrap 5'in ızgara (grid) sistemi kullanılarak iki sütunlu bir yerleşim oluşturuldu:

**Sol sütun (geniş):** Üstte büyük puntolu barkod giriş alanı, altında sepet tablosu. Tabloda sıra numarası, ürün adı ve kodu, miktar (düzenlenebilir kutu), birim fiyat, satır tutarı ve silme düğmesi sütunları bulunmaktadır.

**Sağ sütun (dar):** Son okutulan ürün bilgisi, fiş numarası, ara toplam, KDV oran kırılımı, satır sayısı ve büyük puntolu genel toplam kutusu ile "Ödeme Al" ve "Fişi İptal Et" düğmeleri.

Genel toplam bilerek büyük (2,4 rem) ve renkli bir kutuda gösterildi; kasiyerin ekrana bakmadan, göz ucuyla görebilmesi gereken tek bilgi budur. Ayrıca sayfanın sağ üst köşesine klavye kısayollarını hatırlatan küçük bir şerit eklendi.

Ana menüye (`_Layout.cshtml`) "Kasa" bağlantısı eklendi ve ilk sıraya yerleştirildi.

---

## 2. İstemci tarafı JavaScript — kasa.js

`wwwroot/js/kasa.js` dosyasında ekranın tüm etkileşimi yazıldı. Herhangi bir JavaScript kütüphanesi (jQuery vb.) kullanılmadı; tarayıcının kendi `fetch` API'si tercih edildi.

### Sunucuya istek gönderme

Sunucudaki aksiyonlar form verisi beklediği için istekler `application/x-www-form-urlencoded` biçiminde gönderilmektedir:

```javascript
async function gonder(yol, veri) {
    const yanit = await fetch(yol, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams(veri)
    });

    const govde = await yanit.json();

    // Hata durumunda sunucu { sepet, hata } sarmalayicisi doner.
    return yanit.ok
        ? { sepet: govde, hata: null }
        : { sepet: govde.sepet, hata: govde.hata };
}
```

Sunucu her işlemde sepetin güncel hâlini JSON olarak döndürmektedir. İstemci bu veriyi alıp tabloyu ve toplam panelini yeniden çizmektedir; sayfa hiçbir aşamada yeniden yüklenmemektedir.

### Sayı biçimlendirme

Tutarlar ve miktarlar Türkçe biçimde gösterilmek üzere tarayıcının `Intl.NumberFormat` arayüzü ile biçimlendirildi:

```javascript
const paraBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
```

Böylece sunucudan gelen `32.5` değeri ekranda `32,50` olarak görünmektedir. Sunucu tarafında ise 3. günde yaşanan hatanın tekrarlanmaması için verinin değişmez (invariant) kültürde işlenmesine devam edilmektedir; yani biçimlendirme yalnızca görüntüleme aşamasında yapılmaktadır.

### Miktar güncelleme

Sepetteki her satırın miktar kutusuna yeni değer yazılıp Enter'a basıldığında satır güncellenmektedir. Kullanıcının ondalık ayırıcı olarak virgül kullanabileceği düşünülerek dönüşüm yapıldı:

```javascript
// Kullanici virgulle de yazabilir: 1,5 -> 1.5
const miktar = parseFloat(girdi.value.replace(",", "."));
if (isNaN(miktar)) { uyariGoster("Geçersiz miktar."); return; }
```

Miktar olarak 0 girildiğinde satır silinmektedir; bu davranış sunucu tarafında altıncı günde yazılmıştı.

---

## 3. Odak yönetimi

Kasa ekranının en önemli kullanılabilirlik özelliği, imlecin (odağın) her zaman barkod alanında olmasıdır. Kasiyer bir ürünü sepetten sildikten veya miktarını değiştirdikten sonra fareyle tekrar barkod alanına tıklamak zorunda kalmamalıdır.

Bunun için tüm işlemler ortak bir yardımcı fonksiyondan geçirildi. Bu fonksiyon isteği çalıştırır, sonucu ekrana basar ve **her durumda** (hata olsa bile) odağı barkod alanına geri verir:

```javascript
async function islet(istek, sonuOkutulanGuncelle) {
    try {
        const { sepet, hata } = await istek();

        if (hata) uyariGoster(hata); else uyariGizle();
        if (sepet) {
            ciz(sepet);
            if (sonuOkutulanGuncelle && !hata) sonOkutulan(sepet.satirlar);
        }
    } catch (e) {
        uyariGoster("Sunucuya ulaşılamadı.");
    } finally {
        odakla();
    }
}
```

`finally` bloğunun kullanılması bilinçlidir: sunucu hata verse veya bağlantı kopsa dahi odak barkod alanına dönmekte, kasiyer çalışmaya devam edebilmektedir.

---

## 4. Klavye kısayolları

Yol haritasında belirtilen kısayollar sayfanın tamamında çalışacak şekilde tanımlandı:

| Tuş | İşlev |
|---|---|
| Enter | Barkodu sepete ekle |
| F2 | Ödeme al |
| F4 | Seçili satırı sil |
| Esc | Fişi iptal et |

F4 tuşunun çalışabilmesi için "seçili satır" kavramı eklendi: kasiyer bir satıra tıkladığında satır vurgulanmakta ve F4 ile silinebilmektedir. Silme sonrası seçim otomatik olarak düşürülmektedir.

Fişi iptal etme işlemi bir onay penceresi göstermektedir; yanlışlıkla Esc tuşuna basılması durumunda müşterinin tüm sepetinin silinmesini önlemek için bu koruma eklendi.

Ödeme düğmesi şu an devre dışıdır ve sepet boşken pasif kalmaktadır; ödeme ekranı bir sonraki günün konusudur.

---

## 5. Karşılaşılan hata: hızlı okutmada barkodların birleşmesi

Ekran tarayıcıda test edilirken önemli bir hata tespit edildi. Arka arkaya hızlı biçimde dört barkod okutulduğunda sistem şu hatayı verdi:

```
'86900000001286900000020328000010125018690000000036' barkodlu urun bulunamadi.
```

Görüldüğü gibi dört ayrı barkod tek bir metin hâlinde birleşmişti.

**Sebebi:** İlk yazılan kodda barkod alanı, sunucudan cevap geldikten **sonra** temizleniyordu. Ancak barkod okuyucular tüm haneleri milisaniyeler içinde yazıp Enter tuşunu gönderir. Sunucu cevabı gelene kadar geçen sürede kasiyer ikinci ürünü okutursa, ikinci barkod birincisi hâlâ kutudayken üzerine yazılmakta ve iki barkod birleşmektedir. Bu, gerçek kullanımda kesinlikle karşılaşılacak bir hataydı.

**Çözümü:** Barkod alanı, istek gönderilmeden **önce** temizlenecek şekilde değiştirildi:

```javascript
// Alan sunucu cevabi BEKLENMEDEN temizlenir: barkod okuyucu cok hizli
// yazar, sonraki okutma cevap gelmeden baslarsa iki barkod birlesirdi.
barkodGirdi.value = "";
```

Buna ek olarak, üst üste gelen isteklerin sepet tablosunu yanlış sırada çizmesini önlemek için istekleri sıraya alan basit bir kuyruk yapısı eklendi:

```javascript
/// Istekleri sirayla calistiran basit kuyruk.
let kuyruk = Promise.resolve();

function siraya(is) {
    kuyruk = kuyruk.then(is, is);
    return kuyruk;
}
```

Ayrıca odak fonksiyonundaki metin seçme (`select()`) çağrısı kaldırıldı. Sebebi şudur: istek sürerken kasiyer yeni bir barkod yazmaya başlamış olabilir; cevap geldiğinde metnin seçili hâle gelmesi, yazılacak bir sonraki karakterin mevcut metnin üzerine yazılmasına ve veri kaybına yol açardı.

---

## 6. Test ve doğrulama

Ekran, tarayıcı üzerinde gerçek etkileşimle test edildi. Barkod alanına barkod yazılıp Enter'a basıldığında ürünün sepete düştüğü ve sayfanın yenilenmediği doğrulandı (kabul kriteri).

Ardından farklı senaryolar denendi:

| Senaryo | Sonuç |
|---|---|
| Tekli barkod okutma | 1 adet olarak eklendi |
| Aynı ürünü tekrar okutma | Yeni satır açılmadı, miktar 2'ye çıktı |
| Koli barkodu okutma | 12 adet olarak eklendi |
| Terazi barkodu okutma | 1,250 kg olarak eklendi, satırda "kg" rozeti göründü |
| Miktarı 12'den 3'e düşürme | Satır tutarı 504,00'ten 126,00'a güncellendi |
| Satır seçip F4 ile silme | Satır silindi, toplam güncellendi |
| Kontrol hanesi hatalı barkod | "Barkod kontrol hanesi tutmuyor, tekrar okutun." uyarısı çıktı, sepet bozulmadı |

Üç farklı KDV oranı (%1, %10, %20) içeren bir sepette KDV kırılımının sağ panelde oran oran doğru listelendiği ve genel toplamın 689,13 TL olarak hesaplandığı görüldü; bu değer altıncı günde arka planda hesaplanan değerle birebir aynıydı.

Her işlemden sonra imlecin barkod alanına geri döndüğü de kontrol edildi.

Mevcut 46 birim testin tamamı çalıştırıldı ve hepsinin geçmeye devam ettiği doğrulandı.

---

## Günün değerlendirmesi

Kasa ekranı tamamlandı ve kabul kriteri karşılandı: barkod yazılıp Enter'a basıldığında ürün sepete düşmekte, sayfa yenilenmemektedir. Bir kasiyer artık fareye hiç dokunmadan ürün ekleyebilmekte, miktar değiştirebilmekte ve satır silebilmektedir.

Ödeme alma işlemi henüz yazılmadığı için "Ödeme Al" düğmesi uyarı vermektedir; ödeme ekranı ve satışın tamamlanması (stok düşümü dahil) sekizinci günün konusudur.
