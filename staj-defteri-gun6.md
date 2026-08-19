# Staj Defteri — 6. Gün

**Proje:** Market Otomasyonu
**Konu:** Sepet altyapısı, fiş yönetimi ve KDV hesaplama

---

## Günün amacı

Projenin ikinci haftasına, yani satış işlemlerine geçildi. Bu günün hedefi, kasa ekranının arkasında çalışacak sepet altyapısını kurmaktı: bir müşterinin aldığı ürünlerin tutulduğu yapının oluşturulması, ürün ekleme/çıkarma işlemlerinin yazılması ve satış tutarı ile KDV'nin doğru hesaplanmasının sağlanması. Kasa ekranının görsel arayüzü bir sonraki güne bırakılarak, bu gün yalnızca arka plandaki iş mantığı ve veri katmanı geliştirildi.

---

## 1. Sepetin nerede tutulacağı kararı

Geliştirmeye başlamadan önce, açık sepetin (müşteri henüz ödeme yapmadan önce toplanan ürünlerin) nerede saklanacağına karar verildi. İki seçenek değerlendirildi:

**Birinci seçenek:** Sepeti web oturumunda (session) yani sunucunun geçici belleğinde tutmak. Bu yöntem daha basittir ancak kasa bilgisayarı kapanır, tarayıcı çöker veya uygulama yeniden başlarsa sepetteki tüm ürünler kaybolur.

**İkinci seçenek (tercih edilen):** Sepeti doğrudan veritabanında, `Fis` tablosunda **"Beklemede"** durumundaki (`Durum = 1`) bir fiş olarak tutmak.

İkinci seçenek tercih edildi. Bu yaklaşımın avantajları şunlardır: kasa çökse dahi sepet kaybolmaz, müşteri sırada beklerken sepet askıya alınıp başka bir müşteriye geçilebilir, gerekirse başka bir kasadan devralınabilir. Ayrıca Gün 2'de tasarlanan veritabanı şeması zaten bu senaryoya göre planlanmıştı (fişin `Durum` alanında 1 = beklemede, 2 = tamamlandı, 9 = iptal değerleri tanımlıydı).

Bu tasarımda önemli bir kural benimsendi: **beklemedeki fiş stoğu etkilemez.** Sepete ürün eklenmesi bir satış anlamına gelmez; stok ancak ödeme alınıp fiş "Tamamlandı" durumuna geçtiğinde düşecektir.

---

## 2. Entity sınıflarının yazılması

Veritabanındaki `Fis`, `FisSatir` ve `Vardiya` tablolarına karşılık gelen C# sınıfları `Models/Entities` klasöründe oluşturuldu. Bu sınıflar Gün 2'deki kurala uygun olarak, property adları kolon adlarıyla birebir aynı olacak şekilde yazıldı; böylece Dapper ek bir eşleme koduna gerek kalmadan sorgu sonuçlarını bu nesnelere aktarabilmektedir.

`FisSatir` sınıfındaki `BirimFiyat` alanı özellikle önemlidir. Bu alan, satışın yapıldığı andaki ürün fiyatını **satır içinde saklar**; ürün kartından okumaz. Sebebi şudur: ürünün fiyatı yarın değişirse, geçmişte kesilmiş fişlerin tutarları da değişmiş gibi görünür ve muhasebe kayıtları bozulurdu. Fiyatın satırda dondurulması bu sorunu önlemektedir.

---

## 3. FisRepository sınıfı

Fiş ve fiş satırlarıyla ilgili tüm SQL sorguları `Data/Repositories/FisRepository.cs` dosyasında toplandı. Sınıf, katman kuralına uygun olarak yalnızca SQL çalıştırmakta, hiçbir iş kuralı içermemektedir.

### Fiş numarasının güvenli üretilmesi

Sınıfın en dikkat gerektiren kısmı fiş numarası üretimi oldu. Fiş numarası üretmenin yaygın ama hatalı yöntemi `SELECT MAX(FisNo) + 1` sorgusudur. Bu yöntemin sorunu şudur: iki kasiyer aynı anda satış tamamlarsa, her ikisi de veritabanına baktığında henüz kaydedilmemiş aynı en büyük numarayı görür ve aynı fiş numarasını üretmeye çalışır. Bu duruma yarış koşulu (race condition) denir.

Bunun yerine, Gün 2'de veritabanında tanımlanan `FisNoSeq` adlı SQL Server sequence nesnesi kullanıldı. Sequence, SQL Server motorunun kendi içinde atomik (bölünemez) olarak yönettiği bir sayaçtır; aynı anda yüzlerce istek gelse bile her birine farklı bir değer verilmesi motor tarafından garanti edilir:

```sql
DECLARE @no INT = NEXT VALUE FOR FisNoSeq;
DECLARE @fisNo NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMdd') + '-' + FORMAT(@no, '00000');

INSERT INTO Fis (FisNo, VardiyaId, KullaniciId, Durum)
OUTPUT INSERTED.Id, INSERTED.FisNo
VALUES (@fisNo, @vardiyaId, @kullaniciId, 1);
```

Üretilen fiş numarası tarih öneki ile birlikte `20260819-00004` biçimindedir. `OUTPUT INSERTED` ifadesi sayesinde eklenen kaydın Id ve fiş numarası aynı sorguda geri alınmakta, ikinci bir `SELECT` sorgusuna gerek kalmamaktadır.

### Diğer metotlar

Sınıfa ayrıca açık fişi getirme, fişin satırlarını listeleme, satır ekleme, satır miktarını güncelleme, satır silme, fiş başlığındaki toplamları güncelleme ve fişi iptal etme metotları yazıldı. Satır silme ve güncelleme sorgularında yalnızca satır Id'si değil, `FisId` şartı da kullanıldı; böylece dışarıdan gönderilen hatalı bir istekle başka bir fişin satırının değiştirilmesi engellendi.

---

## 4. SepetHesaplayici sınıfı — hesaplamalar

Sepet tutarlarını hesaplayan mantık, `Services/SepetHesaplayici.cs` dosyasında **statik ve saf** (veritabanına, oturuma, hiçbir dış kaynağa bağlı olmayan) bir sınıf olarak yazıldı. Bu tasarımın amacı, hesaplamaların doğrudan birim testlerle, veritabanı kurulumu gerektirmeden doğrulanabilmesidir.

### KDV'nin toplamdan ayrıştırılması

En kritik hesaplama KDV'dir. Türkiye'deki perakende uygulamasında **raf fiyatları KDV dahildir**; yani müşterinin gördüğü 120 TL'lik etiket, KDV'yi zaten içermektedir. Bu nedenle KDV tutarın üzerine eklenmez, içinden ayrıştırılır:

```csharp
public static decimal KdvAyristir(decimal kdvDahilTutar, decimal kdvOrani)
{
    if (kdvOrani <= 0) return 0;

    var haric = kdvDahilTutar / (1 + kdvOrani / 100m);
    return decimal.Round(kdvDahilTutar - haric, 2, MidpointRounding.AwayFromZero);
}
```

Örnek: %20 KDV'li 120 TL'lik bir ürünün içindeki KDV 24 TL değil, 20 TL'dir (100 TL matrah + 20 TL KDV = 120 TL). Bu ayrımın yanlış yapılması hem müşteriye yanlış fiş kesilmesine hem de vergi beyanının hatalı olmasına yol açardı.

### KDV oran bazlı kırılım

Bir markette aynı sepette farklı KDV oranlarına sahip ürünler bulunur: temel gıda %1, içecek %10, temizlik ürünleri %20. Yasal olarak fişte her KDV oranı için ayrı matrah ve KDV satırı basılması gerekmektedir. Bu nedenle satırları orana göre gruplayan bir metot yazıldı:

```csharp
public static List<KdvKirilimVm> KdvKirilimiHesapla(IEnumerable<SepetSatirVm> satirlar) =>
    satirlar
        .GroupBy(s => s.KdvOrani)
        .Select(g =>
        {
            var toplam = g.Sum(s => SatirToplamHesapla(s.Miktar, s.BirimFiyat, s.IndirimTutari));
            var kdv = KdvAyristir(toplam, g.Key);

            return new KdvKirilimVm
            {
                Oran = g.Key,
                Toplam = toplam,
                KdvTutari = kdv,
                Matrah = toplam - kdv
            };
        })
        .OrderBy(k => k.Oran)
        .ToList();
```

Bu gruplama matematiksel olarak zorunludur: karışık oranlı bir sepette, tek bir genel toplamdan KDV çıkarmak mümkün değildir; her oranın kendi içinde hesaplanması gerekir.

---

## 5. SepetService sınıfı — iş mantığı

`Services/SepetService.cs` dosyasında sepet işlemleri yazıldı: barkodla ürün ekleme, satır miktarını değiştirme, satır silme ve sepeti iptal etme. Katman kuralına uygun olarak veritabanı işlemi (transaction) yönetimi bu sınıfta yapılmaktadır.

Her işlem sonrasında fiş başlığındaki toplam alanları (`AraToplam`, `ToplamKdv`, `GenelToplam`) satırlardan yeniden hesaplanıp veritabanına yazılmaktadır. Toplamların fişte de saklanmasının sebebi, ileride yazılacak raporların her seferinde tüm fiş satırlarını toplamak zorunda kalmamasıdır.

### Aynı ürünün tekrar okutulması

Kasada aynı ürün ikinci kez okutulduğunda yeni bir satır açmak yerine mevcut satırın miktarının artırılması sağlandı. Ancak burada bir istisna tanımlandı:

```csharp
// Tartili urunlerde her okutma ayri bir tartimdir; miktarlari birlestirmek yaniltici olur.
var mevcutSatirId = cozum.Birim == "KG"
    ? null
    : await _fisRepository.AyniUrunSatiriBulAsync(conn, tx, fisId, cozum.UrunId, ct);
```

Tartılan ürünlerde (domates, kıyma vb.) her barkod okutması ayrı bir tartım işlemidir. 1.2 kg ve 0.8 kg olarak iki ayrı poşette tartılan domatesi fişte "2 kg" şeklinde tek satırda göstermek, müşteri açısından fişin denetlenebilirliğini bozardı. Bu nedenle tartılı ürünlerde her okutma ayrı satır olarak eklenmektedir.

---

## 6. Karşılaşılan hatalar ve çözümleri

### Hata 1: Veritabanı kilitlenmesi (deadlock)

Kod ilk kez çalıştırıldığında, sepete ürün ekleme isteği yanıt vermedi ve bir süre sonra şu hatayla sonlandı:

```
Microsoft.Data.SqlClient.SqlException: Yürütme Zaman Aşımı Süresi Doldu.
```

Sorunun kaynağı incelendiğinde şu durum tespit edildi: Açık bir transaction içinde yeni bir fiş satırı eklendikten sonra, toplamları hesaplamak için satırlar tekrar okunuyordu. Ancak bu okuma işlemi **ayrı bir veritabanı bağlantısı** açıyordu. Transaction henüz tamamlanmadığı için eklenen satırlar kilitli durumdaydı ve yeni bağlantı bu kilidin çözülmesini beklemeye başlıyordu. Transaction ise okuma bitmeden tamamlanamadığı için sistem kendi kendini bekler duruma düşüyordu.

Çözüm olarak, transaction içinden okuma yapan ayrı bir metot aşırı yüklemesi (overload) yazıldı. Bu sürüm yeni bağlantı açmak yerine, transaction'ın kendi bağlantısını ve transaction nesnesini parametre olarak almaktadır:

```csharp
/// <summary>
/// Acik bir transaction icinden okumak icin. Ayri baglanti acan surum
/// kullanilirsa, transaction'in kilitledigi satirlari beklemeye takilir.
/// </summary>
public async Task<List<SepetSatirVm>> SatirlariGetirAsync(
    IDbConnection conn, IDbTransaction tx, int fisId, CancellationToken ct = default)
{
    var liste = await conn.QueryAsync<SepetSatirVm>(
        new CommandDefinition(SqlSatirlar, new { fisId }, tx, cancellationToken: ct));
    return liste.AsList();
}
```

Bu düzeltmeden sonra işlem sorunsuz çalıştı.

### Hata 2: Çağrı sırasına bağımlı fonksiyon

Yazılan birim testlerden biri başarısız oldu: KDV kırılımı hesaplayan metot, beklenen 300 TL yerine 0 TL döndürüyordu. İnceleme sonucunda metodun, satır toplamlarının kendisinden **önce** hesaplanmış olmasına bağımlı olduğu görüldü. Yani metot tek başına çağrıldığında yanlış sonuç veriyordu.

Bu, testlerin yakaladığı gerçek bir tasarım kusuruydu. Metot, satır tutarlarını kendi içinde hesaplayacak şekilde düzeltildi; artık hangi sırayla çağrıldığından bağımsız olarak doğru sonuç vermektedir.

---

## 7. Test ve doğrulama

Sepet hesaplamaları için `MarketOtomasyon.Tests` projesinde 17 yeni birim test yazıldı: satır tutarı hesaplama ve kuruş yuvarlama, indirimin tutardan büyük olması durumu, KDV ayrıştırma (%0, %1, %10, %20 oranları), farklı KDV oranlarının ayrı ayrı gruplanması, grupların matrah + KDV toplamının grup toplamına eşitliği ve boş sepet durumu. Projedeki toplam test sayısı 46'ya çıktı ve tamamı başarıyla geçti.

Hesaplamaların yanı sıra sistem, gerçek veritabanına karşı da uçtan uca test edildi. Sepete farklı KDV oranlarında beş ürün eklendi (tekli barkod, koli barkodu ve terazi barkodu dahil) ve sonuç şu şekilde alındı:

| KDV Oranı | Matrah | KDV Tutarı | Toplam |
|---|---|---|---|
| %1 | 95,18 | 0,95 | 96,13 |
| %10 | 458,18 | 45,82 | 504,00 |
| %20 | 74,17 | 14,83 | 89,00 |
| **Genel** | **627,53** | **61,60** | **689,13** |

Aynı ürünün ikinci kez okutulmasıyla miktarın 1'den 2'ye çıktığı, koli barkodu okutulduğunda 12 adet eklendiği, terazi barkodunda 1,250 kg miktarın doğru işlendiği doğrulandı. Ayrıca miktar güncelleme, satır silme, miktarın sıfır girilmesi durumunda satırın silinmesi ve geçersiz barkodda anlamlı hata mesajı dönmesi test edildi. Fiş başlığındaki toplamların da veritabanına doğru yazıldığı SQL sorgusuyla kontrol edildi.

---

## Günün değerlendirmesi

Sepet altyapısı tamamlandı ve kabul kriteri karşılandı: servis üzerinden sepete ürün eklenip toplam ve KDV doğru hesaplanmaktadır. Kasa ekranının görsel arayüzü henüz yazılmadığı için işlemler şimdilik JSON uç noktaları üzerinden (`/Kasa/Ekle`, `/Kasa/Sepet`, `/Kasa/MiktarGuncelle`, `/Kasa/SatirSil`, `/Kasa/Iptal`) çalışmaktadır; ekran bir sonraki gün geliştirilecektir.

Ayrıca oturum açma özelliği henüz bulunmadığı için kasiyer kullanıcısı geçici olarak sabit kabul edilmekte ve açık vardiya yoksa otomatik açılmaktadır. Vardiya yönetimi üçüncü haftanın konusu olduğundan bu geçici çözüm o hafta kaldırılacaktır.
