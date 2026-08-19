# Staj Defteri — 8. Gün

**Proje:** Market Otomasyonu
**Konu:** Satır tutarı hesapları, manuel indirim ve yetki denetimi

---

## Günün amacı

Kasa ekranı bir önceki gün çalışır hâle gelmişti; ürün eklenip miktar değiştirilebiliyordu. Bu günün hedefi, gerçek bir kasada karşılaşılan hesap durumlarını eklemekti: müşteriye özel indirim verilmesi, bu indirimin kimin yetkisiyle verilebileceğinin denetlenmesi ve indirimin KDV hesabını bozmadan uygulanması.

---

## 1. Fiyat modelinin netleştirilmesi

Güne, hesap modelinin doğrulanmasıyla başlandı. İki farklı yaklaşım mümkündü:

**KDV hariç model:** Veritabanındaki fiyat KDV içermez, KDV satır tutarının üzerine eklenir.
**KDV dahil model:** Veritabanındaki fiyat KDV'yi zaten içerir, KDV bu tutarın içinden ayrıştırılır.

Türkiye'de perakende satışta raf etiketindeki fiyat KDV dahildir; müşteri kasada etikette gördüğü tutarı öder. Bu nedenle **KDV dahil model** benimsendi. Gün içinde her iki model de kodlanıp denendi, sonuç olarak KDV dahil modelde karar kılındı.

Modelin çalışma biçimi:

```
10 adet x 100,00 TL (KDV dahil)  → brüt 1000,00
%10 indirim                       → -100,00
Tahsil edilecek tutar             →  900,00
  içindeki KDV (%20)              →  150,00   (900 - 900/1,20)
  matrah                          →  750,00
```

Bu hesap `Services/SepetHesaplayici.cs` içinde iki fonksiyona ayrıldı:

```csharp
/// <summary>Musteriden alinacak satir tutari (KDV dahil): miktar x birim fiyat - indirim.</summary>
public static decimal SatirToplamHesapla(decimal miktar, decimal birimFiyat, decimal indirim = 0)
{
    var brut = decimal.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
    var toplam = brut - indirim;
    return toplam < 0 ? 0 : toplam;
}

/// <summary>
/// KDV dahil tutarin icindeki KDV: tutar - (tutar / (1 + oran/100)).
/// 120 TL ve %20 icin 20 TL doner.
/// </summary>
public static decimal KdvAyristir(decimal kdvDahilTutar, decimal kdvOrani)
{
    if (kdvOrani <= 0) return 0;

    var haric = kdvDahilTutar / (1 + kdvOrani / 100m);
    return decimal.Round(kdvDahilTutar - haric, 2, MidpointRounding.AwayFromZero);
}
```

Ayrıca indirimin tutardan büyük olması durumunda negatif satır tutarı oluşmaması için sonuç sıfırda sınırlandırıldı.

---

## 2. İndirim yetkisi denetimi

Gerçek bir markette her kasiyer istediği oranda indirim veremez. Bu kural `Services/IndirimYetkisi.cs` içinde ayrı bir sınıf olarak yazıldı:

| Rol | Satır indirimi | Fiş indirimi |
|---|---|---|
| Kasiyer | en fazla %10 | en fazla %5 |
| Müdür | %50'ye kadar | %50'ye kadar |
| Herkes | %50 üzeri reddedilir | %50 üzeri reddedilir |

Fiş genelindeki limitin satır limitinden dar tutulması bilinçlidir: fiş indirimi tüm sepeti etkilediği için kötüye kullanıma daha açıktır. Mutlak %50 sınırı ise, manuel indirim yoluyla ürünün bedelsiz verilmesini engellemek için konuldu.

```csharp
private static (bool Yeterli, string? Hata) Kontrol(byte rol, decimal yuzde, decimal kasiyerLimiti, string kapsam)
{
    if (yuzde <= 0)
        return (false, "Indirim orani sifirdan buyuk olmalidir.");

    if (yuzde > MutlakLimitYuzde)
        return (false, $"Indirim %{MutlakLimitYuzde:0.##} oranini asamaz.");

    if (rol == RolMudur)
        return (true, null);

    if (yuzde > kasiyerLimiti)
        return (false, $"{kapsam} indiriminde %{kasiyerLimiti:0.##} ustu mudur onayi gerektirir.");

    return (true, null);
}
```

Bu sınıf da veritabanına bağlı olmayan saf bir sınıf olarak yazıldı; böylece kurallar doğrudan birim testle doğrulanabilmektedir.

Kullanıcı rolünü okumak için `KullaniciRepository` sınıfı eklendi. Kasa ekranından gelen isteğe "onaylayan kullanıcı" bilgisi eklenirse yetki o kullanıcının rolüne göre denetlenmekte, eklenmezse işlemi yapan kasiyerin rolü kullanılmaktadır.

---

## 3. Fiş bazlı indirimin satırlara dağıtılması

Fiş geneline indirim uygulanırken önemli bir tasarım sorunu ortaya çıktı: indirim, fiş başlığında tek bir tutar olarak tutulursa KDV hesabı bozulur.

Sebebi şudur: bir sepette farklı KDV oranlarına sahip ürünler bulunur (temel gıda %1, içecek %10, temizlik %20). Fişte yasal olarak her KDV oranı için ayrı matrah ve KDV satırı basılması gerekir. İndirim tek bir toplam olarak durursa, bu indirimin hangi orandan ne kadar KDV düşüreceği belirsiz kalır.

Bu nedenle fiş indirimi, satırlara brüt tutarları oranında dağıtılmaktadır:

```csharp
foreach (var satir in satirlar)
    dagitim[satir.SatirId] = decimal.Round(indirimTutari * brutler[satir.SatirId] / toplamBrut, 2,
        MidpointRounding.AwayFromZero);

var artik = indirimTutari - dagitim.Values.Sum();
if (artik != 0)
{
    var enBuyuk = satirlar.OrderByDescending(s => brutler[s.SatirId]).First().SatirId;
    dagitim[enBuyuk] += artik;
}
```

Dağıtımda her satır ayrı ayrı kuruşa yuvarlandığı için toplamları verilen indirimden bir-iki kuruş sapabilir. Bu artık, en büyük satıra eklenerek toplamın birebir tutması sağlandı. Örneğin üç eşit satıra 10 TL dağıtıldığında 3,33 + 3,33 + 3,33 = 9,99 eder; kalan 0,01 en büyük satıra eklenir.

Gerçek veri üzerinde yapılan testte %5 fiş indirimi şöyle dağıldı:

```
Süt          32,50 → 1,62
Çamaşır Suyu 89,00 → 4,45
Domates      31,13 → 1,56
                     7,63  (kayıtlı toplam indirimle birebir aynı)
```

---

## 4. Toplam KDV'nin gruplardan hesaplanması

Sepet toplamları hesaplanırken ikinci bir yuvarlama sorunu fark edildi. Toplam KDV iki farklı yolla bulunabilir: her satırın KDV'sini ayrı ayrı hesaplayıp toplamak, ya da KDV oranı gruplarının toplamından hesaplamak. Her satır kuruşa yuvarlandığı için bu iki yöntem birbirinden bir kuruş sapabilir; bu durumda fişin altındaki "Toplam KDV" ile fişte basılan oran dökümü tutmaz.

Bu nedenle toplam KDV, oran gruplarından toplanacak şekilde yazıldı:

```csharp
// Toplam KDV ve matrah kirilimdan alinir; satir satir ayristirip toplamak
// yuvarlama yuzunden fisin oran dokumuyle bir kurus sapabilirdi.
var genelToplam = satirlar.Sum(s => s.SatirToplam);
var toplamKdv = kirilim.Sum(k => k.KdvTutari);
```

---

## 5. Kasa ekranına indirim arayüzü

Kasa ekranına iki yeni klavye kısayolu eklendi: **F5** seçili satıra indirim, **F6** fiş geneline indirim.

İndirim oranı, tarayıcının hazır `prompt()` penceresi yerine sayfa içine yerleştirilen küçük bir panelden girilmektedir. Bunun sebebi, tarayıcı diyaloglarının odağı barkod alanından koparması ve kasiyerin akışını bozmasıdır. Panelde ayrıca "Onaylayan" seçimi bulunmakta; kasiyer limitini aşan bir indirim gerektiğinde müdür onayı buradan seçilebilmektedir.

Yetki reddedildiğinde panel kapanmayıp açık kalacak şekilde yazıldı; böylece kasiyer hata mesajını gördükten sonra müdür onayı seçip aynı işlemi tekrarlayabilmektedir.

Ekranda indirim uygulanmış satırlar kırmızı bir rozetle işaretlenmekte, sağ paneldeki özet bölümüne de toplam indirim satırı eklenmektedir.

---

## 6. Test ve doğrulama

`SepetHesaplayiciTests` ve yeni yazılan `IndirimYetkisiTests` dosyalarıyla test sayısı 61'e çıkarıldı ve tamamı geçti. Test edilen durumlar arasında şunlar bulunmaktadır: satır tutarının kuruşa yuvarlanması, indirimin tutardan büyük olması, KDV ayrıştırma, farklı oranların ayrı gruplanması, dağıtımın brüt oranına uygunluğu, yuvarlama artığının toplamı bozmaması, indirimin toplam brütü aşamaması ve rol bazlı yetki kuralları.

Ayrıca sistem gerçek veritabanına karşı uçtan uca denendi:

| Senaryo | Sonuç |
|---|---|
| Kasiyer %8 satır indirimi | Uygulandı |
| Kasiyer %30 satır indirimi | Reddedildi, uyarı mesajı döndü |
| Müdür onayıyla %30 | Uygulandı |
| Müdür %60 | Reddedildi (mutlak sınır) |
| Fiş geneline %5 indirim | Satırlara doğru dağıtıldı, KDV kırılımı tutarlı kaldı |
| Tartılı ürün (1,250 kg domates) | Barkoddaki gramajdan otomatik hesaplandı |
| Sepeti tamamen iptal | Sepet boşaldı, fiş veritabanında Durum 9 (iptal) oldu |

Her senaryoda ara toplam ile toplam KDV'nin toplamının genel toplama eşit kaldığı ayrıca kontrol edildi.

---

## Günün değerlendirmesi

Satır hesapları, manuel indirim, yetki denetimi ve sepet iptali tamamlandı. Bu günün en öğretici kısmı, basit görünen "indirim" özelliğinin aslında KDV muhasebesiyle iç içe olması oldu: indirimin nereye yazıldığı, hangi satıra ne kadar düştüğü ve kuruş yuvarlamalarının nasıl ele alındığı doğrudan fişin yasal geçerliliğini etkilemektedir.

Sıradaki adım ödeme alma ve satışın tamamlanmasıdır; ödeme onaylandığında fiş "Tamamlandı" durumuna geçecek ve satılan ürünler stoktan düşecektir.
