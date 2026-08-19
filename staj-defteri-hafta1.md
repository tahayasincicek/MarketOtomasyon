# Staj Defteri — Hafta 1

**Proje:** Market Otomasyonu
**Teknolojiler:** .NET 8 (ASP.NET Core MVC), Dapper, Microsoft SQL Server 2022, Bootstrap 5, FluentValidation, xUnit

---

## 1. Gün — Proje İskeleti ve Veri Erişim Altyapısı

Günün amacı, sonraki dört haftanın üzerine inşa edileceği temel proje yapısını kurmaktı.

### Yapılan işlemler

`dotnet new mvc` komutu ile .NET 8 hedefli bir ASP.NET Core MVC web uygulaması oluşturuldu. Projeye üç NuGet paketi eklendi:

- **Dapper** — ham SQL sorgularını çalıştırıp sonuçları C# nesnelerine eşlemek için. Entity Framework gibi bir ORM kullanılmadı; bunun yerine SQL'in doğrudan yazıldığı, daha ince kontrol sağlayan bir "mikro ORM" tercih edildi.
- **Microsoft.Data.SqlClient** — SQL Server'a bağlanmak için düşük seviyeli sürücü.
- **FluentValidation.AspNetCore** — form doğrulama kurallarını ayrı, okunabilir sınıflarda tanımlamak için.

Projede katmanlı bir mimari benimsendi ve buna uygun klasör yapısı oluşturuldu:

```
Models/Entities     → veritabanı tablolarına birebir karşılık gelen sınıflar
Models/ViewModels   → ekrana özel veri şekilleri
Data/Repositories   → yalnızca SQL çalıştıran sınıflar
Data/Sql            → şema (.sql) dosyaları
Services            → iş kuralları ve transaction yönetimi
Controllers         → yalnızca isteği alıp servisi çağıran sınıflar
Views               → Razor sayfaları
```

Bu ayrımın amacı, bir SQL sorgusunun nerede yazılacağının, bir hesaplamanın nerede yapılacağının her zaman belli olmasıdır: Controller içinde asla SQL veya hesap kodu yazılmayacak, Repository içinde asla iş kuralı (örneğin "fiyat sıfırdan büyük olmalı" gibi) bulunmayacak şekilde bir kural benimsendi.

### Veritabanı bağlantı katmanı

İki dosya yazıldı:

```csharp
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
```

```csharp
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MarketDb")
            ?? throw new InvalidOperationException("ConnectionStrings:MarketDb tanimli degil.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
```

Bu arayüz/uygulama ayrımının sebebi: Repository sınıfları doğrudan `SqlConnection` sınıfını değil, `IDbConnectionFactory` arayüzünü tanıyacak. İleride veritabanı değiştirilmek istenirse (örneğin testlerde SQLite kullanmak gibi) yalnızca bu tek sınıf değiştirilecek, onlarca Repository dosyasına dokunulmayacak.

Bağlantı dizesi `appsettings.json` içine eklendi:

```json
"ConnectionStrings": {
    "MarketDb": "Server=localhost;Database=MarketOtomasyon;Trusted_Connection=True;TrustServerCertificate=True"
}
```

`Program.cs` dosyasında bu fabrika, ASP.NET Core'un bağımlılık enjeksiyonu (Dependency Injection) konteynerine tek nesne (Singleton) olarak kaydedildi:

```csharp
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
```

### Test projesi ve sürüm kontrolü

Ana projeye referans veren ayrı bir xUnit test projesi (`MarketOtomasyon.Tests`) oluşturuldu; ileride yazılacak iş kurallarının (fiyat hesaplama, barkod doğrulama gibi) uygulamayı elle çalıştırmadan otomatik test edilebilmesi hedeflendi.

Proje için bir Git deposu başlatıldı, derleme çıktılarını (`bin/`, `obj/`) ve kişisel ayar dosyalarını hariç tutan bir `.gitignore` eklendi. Proje GitHub üzerinde herkese açık (public) bir depo olarak yayımlandı.

### Doğrulama

Uygulama `dotnet run` ile çalıştırıldı, ASP.NET Core'un varsayılan karşılama sayfasının tarayıcıda başarıyla açıldığı görüldü. Gün sonunda derleme 0 hata ve 0 uyarı ile tamamlandı.

---

## 2. Gün — Veritabanı Şeması ve İlk Veri Okuma

### Şema tasarımı

`Data/Sql/01_ilk_sema.sql` dosyasında market otomasyonunun çekirdek veritabanı şeması SQL DDL komutlarıyla (`CREATE TABLE`, `CREATE VIEW`, `CREATE SEQUENCE`) yazıldı. Toplam 11 tablo oluşturuldu:

| Tablo | Amacı |
|---|---|
| `Kategori` | Ürün kategorileri (kendine referanslı, ağaç yapısına uygun) |
| `Urun` | Ürün kartı: kod, ad, birim, KDV oranı, min. stok |
| `UrunBarkod` | Bir ürüne bağlı birden fazla barkod, koli çarpanı |
| `UrunFiyat` | Fiyat geçmişi (başlangıç/bitiş tarihli) |
| `Depo` | Fiziksel stok lokasyonları |
| `Kullanici` | Kasiyer/müdür hesapları |
| `Vardiya` | Kasa açılış/kapanış kayıtları |
| `Fis`, `FisSatir` | Satış fişi başlığı ve satırları |
| `Odeme` | Fişe bağlı ödemeler (nakit/kart/puan) |
| `StokHareket` | Tüm stok giriş/çıkışlarının merkezi kaydı |

Şemada iki mimari karar bilinçli olarak alındı:

**Karar 1 — Stok miktarı kolon olarak tutulmadı.** `Urun` tablosunda `StokMiktar` gibi bir alan yoktur. Bunun yerine her stok hareketi (satış, iade, mal kabul, sayım, zayiat, açılış) `StokHareket` tablosuna satır olarak eklenir ve anlık bakiye şu view ile hesaplanır:

```sql
CREATE VIEW vw_StokBakiye AS
SELECT h.UrunId, h.DepoId,
       SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END) AS Bakiye
FROM StokHareket h
GROUP BY h.UrunId, h.DepoId;
```

Bu yaklaşımın amacı denetlenebilirliktir: bir ürünün stok bakiyesinin neden o değerde olduğu, geçmişteki her hareketten geriye doğru izlenebilir; doğrudan bir sayı güncellenmediği için stok ile hareket geçmişi arasında tutarsızlık oluşamaz.

**Karar 2 — Fiyat geçmişi korunur.** Fiyat değiştiğinde `UrunFiyat` tablosundaki eski satır silinmez, `BitisTarihi` alanı doldurularak kapatılır ve yeni bir satır açılır. Güncel fiyat, `BitisTarihi IS NULL` koşuluyla bulunur:

```sql
CREATE VIEW vw_GuncelFiyat AS
SELECT f.UrunId, f.Fiyat
FROM UrunFiyat f
WHERE f.BitisTarihi IS NULL;
```

Ayrıca fiş numarası üretimi için `SELECT MAX(FisNo)+1` gibi yarış durumuna (race condition) açık bir yöntem yerine SQL Server'ın atomik `SEQUENCE` nesnesi tanımlandı (`FisNoSeq`); bu sayede iki kasiyerin eş zamanlı satış yapması durumunda aynı fiş numarasının üretilmesi engellenir.

### Entity sınıfları

Veritabanı tablolarına birebir karşılık gelen 6 C# sınıfı (`Urun`, `UrunBarkod`, `UrunFiyat`, `Kategori`, `Depo`, `StokHareket`) `Models/Entities` klasöründe yazıldı. Property adları kolon adlarıyla aynı tutuldu, çünkü Dapper varsayılan olarak sorgu sonucunu isim eşleşmesine göre nesneye aktarır; ek bir eşleme (mapping) kodu yazmaya gerek kalmadı. Para ve miktar alanlarında hassasiyet kaybı olmaması için her yerde `decimal` tipi kullanıldı, `float`/`double` tercih edilmedi.

### İlk Repository ve bağlantı testi

`UrunRepository` sınıfı yazıldı. Dapper kuralına uygun olarak SQL sorgusu metot içine gömülmeyip sınıf seviyesinde `const string` olarak tutuldu:

```csharp
private const string SqlAktifUrunSayisi = @"
SELECT COUNT(*) FROM Urun WHERE Aktif = 1;";

public async Task<int> AktifUrunSayisiAsync(CancellationToken ct = default)
{
    using var conn = await _factory.CreateOpenConnectionAsync(ct);
    return await conn.ExecuteScalarAsync<int>(
        new CommandDefinition(SqlAktifUrunSayisi, cancellationToken: ct));
}
```

Bu metodu çağıran geçici bir `HomeController.DbTest` aksiyonu eklendi ve tarayıcıdan `/Home/DbTest` adresine gidildiğinde "Baglanti tamam. Aktif urun sayisi: 3" çıktısının alındığı, yani .NET → Dapper → SQL Server hattının uçtan uca çalıştığı doğrulandı.

---

## 3. Gün — Ürün Yönetim Ekranı (Listeleme, Ekleme, Düzenleme)

### Sayfalanabilir ve filtrelenebilir listeleme sorgusu

`UrunRepository.ListeleAsync` metodu yazıldı. Bu metot, toplam kayıt sayısını ve o sayfaya ait satırları **tek veritabanı bağlantısında** almak için Dapper'ın `QueryMultipleAsync` özelliğini kullanır:

```csharp
private const string SqlListele = @"
SELECT COUNT(*) FROM Urun u
WHERE (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@kategoriId IS NULL OR u.KategoriId = @kategoriId)
  AND (@sadeceAktif = 0 OR u.Aktif = 1);

SELECT u.Id, u.Kod, u.Ad, k.Ad AS KategoriAd, u.Birim, u.KdvOrani, u.Tartili, u.Aktif,
       gf.Fiyat AS GuncelFiyat
FROM Urun u
JOIN Kategori k ON k.Id = u.KategoriId
LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE (@arama IS NULL OR u.Ad LIKE '%' + @arama + '%' OR u.Kod LIKE '%' + @arama + '%')
  AND (@kategoriId IS NULL OR u.KategoriId = @kategoriId)
  AND (@sadeceAktif = 0 OR u.Aktif = 1)
ORDER BY u.Ad
OFFSET @atla ROWS FETCH NEXT @adet ROWS ONLY;";
```

Sayfalama, `SELECT MAX(no)` gibi elle sayma yöntemleri yerine SQL Server'ın standart `OFFSET ... FETCH NEXT` sözdizimiyle veritabanı seviyesinde yapıldı. Tüm parametreler (`@arama`, `@kategoriId` vb.) Dapper'a nesne olarak verildi; hiçbir yerde string birleştirme (SQL injection riski) kullanılmadı.

### İş kuralları katmanı: `UrunService`

Ürün ekleme ve güncelleme işlemlerinde, ürün kaydı ile fiyat kaydının **birlikte başarılı olması veya birlikte geri alınması** gerektiği için transaction yönetimi bu sınıfta toplandı:

```csharp
public async Task<int> EkleAsync(UrunFormVm form, CancellationToken ct = default)
{
    using var conn = await _factory.CreateOpenConnectionAsync(ct);
    using var tx = conn.BeginTransaction();

    var urunId = await _urunRepository.EkleAsync(conn, tx, FormdanUrun(form), ct);
    await _fiyatRepository.FiyatEkleAsync(conn, tx, urunId, form.Fiyat, ct);

    tx.Commit();
    return urunId;
}
```

Güncelleme metodunda ise fiyatın gerçekten değişip değişmediği önceden kontrol edilir; değişmediyse yeni bir `UrunFiyat` satırı açılmaz, gereksiz geçmiş kaydı oluşmaz:

```csharp
var mevcutFiyat = await _fiyatRepository.GuncelFiyatAsync(form.Id, ct);
var fiyatDegisti = mevcutFiyat != form.Fiyat;
...
if (fiyatDegisti)
{
    await _fiyatRepository.AcikFiyatiKapatAsync(conn, tx, form.Id, ct);
    await _fiyatRepository.FiyatEkleAsync(conn, tx, form.Id, form.Fiyat, ct);
}
```

### Doğrulama kuralları (FluentValidation)

`UrunFormVmValidator` sınıfında kod benzersizliği, geçerli KDV oranı (%0, %1, %10, %20), birimin ADET/KG olması, fiyatın sıfırdan büyük olması gibi kurallar tanımlandı. Kod benzersizlik kontrolü veritabanına gittiği için asenkron olarak yazılmak zorundaydı:

```csharp
RuleFor(x => x.Kod)
    .NotEmpty().WithMessage("Urun kodu zorunludur.")
    .MustAsync(async (form, kod, ct) =>
        !await urunRepository.KodVarMiAsync(kod.Trim(), form.Id == 0 ? null : form.Id, ct))
    .WithMessage(x => $"'{x.Kod}' kodu baska bir urunde kullaniliyor.");
```

ASP.NET Core'un yerleşik model doğrulaması asenkron kuralları desteklemediği için doğrulama, `UrunController` içinden elle çağrıldı (`await _validator.ValidateAsync(form, ct)`).

### Tespit edilen ve giderilen hata

Testler sırasında, sunucu Türkçe (tr-TR) kültür ayarında çalıştığı için HTML `number` giriş alanının gönderdiği "18.75" değerinin nokta işaretinin binlik ayırıcı sanılıp "1875" olarak veritabanına kaydedildiği görüldü. Bu, gerçek kullanımda fiyat hatasına yol açabilecek ciddi bir hataydı. Çözüm olarak `Program.cs` içine istek yerelleştirmesi eklenerek form verisinin her zaman değişmez (invariant) kültürde ayrıştırılması sağlandı:

```csharp
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = invariant,
    SupportedUICultures = invariant
});
```

Düzeltme sonrası aynı senaryo tekrar test edildi ve fiyatın veritabanına doğru (18.7500) yazıldığı doğrulandı.

### Arayüz

Bootstrap 5 ile `Views/Urun/Index.cshtml` (arama/filtre formu, sayfalanabilir tablo) ve `Views/Urun/Form.cshtml` (ekleme/düzenleme formu, alan bazlı hata mesajları) sayfaları hazırlandı.

---

## 4. Gün — Barkod Yönetimi ve Fiyat Geçmişi Ekranı

### Tek sorguda barkod çözümleme

`BarkodRepository` sınıfı yazıldı. Bu sınıfın en önemli metodu, kasa ekranında her barkod okutulduğunda çalışacak olan `BarkodCozAsync`'tir. Amaç, üç ayrı tabloda (barkod, ürün, fiyat) duran bilgiyi **tek bir veritabanı turunda** almaktır:

```csharp
private const string SqlBarkodCoz = @"
SELECT u.Id AS UrunId, u.Kod, u.Ad, u.Birim, u.KdvOrani, u.Tartili,
       b.Barkod, b.Carpan, b.Tip AS BarkodTip, gf.Fiyat
FROM UrunBarkod b
JOIN Urun u ON u.Id = b.UrunId AND u.Aktif = 1
LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE b.Barkod = @barkod;";
```

Sorgunun iki detayı bilinçli tasarlandı: `JOIN Urun ... AND u.Aktif = 1` koşulu sayesinde satıştan kaldırılmış (pasif) bir ürünün barkodu okutulursa sonuç boş döner, yani ürün satılamaz. Fiyat tablosuna bağlantı ise kasıtlı olarak `LEFT JOIN` yapıldı; çünkü fiyatı hiç girilmemiş bir ürün varsa `INNER JOIN` kullanılsaydı sorgu tamamen boş dönerdi ve sistem "barkod bulunamadı" diye yanlış bir hata verirdi. `LEFT JOIN` ile ürün bilgisi gelir, yalnızca `Fiyat` alanı `null` olur ve uygulama "fiyat tanımlanmamış" diye doğru hatayı verebilir.

### Barkod ekleme/silme ve doğrulama

Ürün detay ekranından (`/Urun/Detay/{id}`) bir ürüne yeni barkod eklenip mevcut barkodlar silinebilecek şekilde `BarkodController` aksiyonları ve karşılık gelen View yazıldı. `BarkodFormVmValidator` ile şu kurallar getirildi: aynı barkodun başka bir ürüne kayıtlı olmaması, barkodun yalnızca harf/rakam içermesi, koli tipi barkodlarda çarpanın 1'den büyük olması zorunluluğu (çarpanı 1 olan bir koli barkodunun tekli barkoddan farkı kalmayacağı için).

Barkod silme işleminde ekstra bir güvenlik önlemi alındı: silme sorgusu yalnızca `Id` değil, aynı zamanda `UrunId` şartını da içerir —

```sql
DELETE FROM UrunBarkod WHERE Id = @id AND UrunId = @urunId;
```

Bu sayede form üzerinden manipüle edilmiş bir istekle başka bir ürünün barkodunun yanlışlıkla silinmesi engellenmiş olur.

### Fiyat geçmişi ekranı

`FiyatRepository.GecmisAsync` metodu ile bir ürünün tüm fiyat kayıtları, en yeni en üstte olacak şekilde listelendi. Detay sayfasında güncel (henüz kapatılmamış) fiyat satırı yeşil renkle vurgulandı. Yapılan canlı testte bir ürünün fiyatı 15.00 TL'den 16.50 TL'ye değiştirildi; veritabanında eski satırın silinmediği, `BitisTarihi` alanının doldurulup "kapalı" duruma geçtiği, yeni satırın "güncel" olarak işaretlendiği doğrulandı.

---

## 5. Gün — Barkod Çözümleme Mantığı, Stok Girişi ve Birim Testler

### Seed verisindeki hata ve düzeltilmesi

EAN-13 doğrulaması eklenmeden önce, önceki günlerde örnek veri olarak elle yazılan 38 barkodun hiçbirinin kontrol hanesinin matematiksel olarak doğru olmadığı fark edildi. Bir betik yardımıyla her barkodun ilk 12 hanesinden doğru kontrol hanesi yeniden hesaplanarak tüm örnek veri barkodları düzeltildi.

### EAN-13 kontrol hanesi doğrulaması

Veritabanına hiç bağımlı olmayan, statik ve saf bir sınıf olan `BarkodCozumleyici` yazıldı. Bu tasarımın amacı, mantığın doğrudan birim testlerle (veritabanı gerekmeden, milisaniyeler içinde) doğrulanabilmesidir:

```csharp
public static bool Ean13Gecerli(string? barkod)
{
    if (barkod is null || barkod.Length != 13) return false;
    if (!barkod.All(char.IsAsciiDigit)) return false;

    var toplam = 0;
    for (var i = 0; i < 12; i++)
    {
        var hane = barkod[i] - '0';
        toplam += (i % 2 == 0) ? hane : hane * 3;
    }

    var beklenen = (10 - (toplam % 10)) % 10;
    return beklenen == barkod[12] - '0';
}
```

Algoritma, EAN-13 standardına göre soldan tek sıradaki haneleri 1 ile, çift sıradaki haneleri 3 ile çarpıp toplamı 10'a tamamlayan değeri son hane ile karşılaştırır.

### Koli barkodu çarpanı

`BarkodService.CozAsync` içinde, okutulan barkodun tipine göre sepete eklenecek miktar belirlendi:

```csharp
var miktar = urun.BarkodTip == 2 ? urun.Carpan : 1m;
```

Tekli barkodda miktar her zaman 1, koli barkodunda ise `UrunBarkod` tablosundaki `Carpan` alanı (örneğin 12'li süt kolisi için 12) kullanılır.

### Terazi barkodu ayrıştırma

Tartılan ürünler (domates, kıyma, peynir vb.) için kullanılan terazi barkodlarının yapısı şu şekilde tasarlandı: 2 haneli sabit önek + 5 haneli ürün kodu + 5 haneli gramaj + 1 kontrol hanesi. Gramaj her tartımda değiştiği için barkodun tamamı veritabanında saklanamaz; yalnızca sabit kısım (ilk 7 hane) `UrunBarkod` tablosuna `Tip = 3` ile kaydedilir, gramaj okutma anında ayrıştırılır:

```csharp
public static (string Anahtar, decimal MiktarKg) TeraziAyristir(string barkod)
{
    var anahtar = barkod[..7];
    var gramaj = int.Parse(barkod.Substring(7, 5));
    return (anahtar, gramaj / 1000m);
}
```

Örneğin `2800001012501` barkodu okutulduğunda anahtar `2800001` (Domates) ve miktar `1.250` kg olarak ayrıştırılır.

### Stok girişi (mal kabul)

`StokRepository` yazılarak `StokHareket` tablosuna hareket ekleme ve depo bazlı bakiye hesaplama metotları oluşturuldu. `StokService.MalKabulAsync` metodu, mal kabul işlemini `KaynakTip = 3` (mal kabul) ile işaretleyip stok giriş hareketini transaction içinde kaydeder ve işlem sonrası güncel bakiyeyi geri döndürür:

```csharp
public async Task<decimal> MalKabulAsync(int urunId, int depoId, decimal miktar, string? aciklama, CancellationToken ct = default)
{
    using var conn = await _factory.CreateOpenConnectionAsync(ct);
    using var tx = conn.BeginTransaction();

    await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
    {
        UrunId = urunId, DepoId = depoId, Yon = 1, Miktar = miktar,
        KaynakTip = 3, Aciklama = aciklama
    }, ct);

    tx.Commit();
    return await _stokRepository.BakiyeAsync(urunId, depoId, ct);
}
```

`/Stok/Giris` ekranı hazırlandı; kasiyerin ürünü listeden aramasına gerek kalmadan doğrudan barkod okutarak mal kabul yapabilmesi için bu ekranda da `BarkodService` kullanıldı. Ekranda son 15 stok hareketi de listelenmektedir.

### Birim testler

xUnit ile iki test dosyası yazıldı:

- **`BarkodCozumleyiciTests`** — 20 test: geçerli/geçersiz EAN-13 barkodları, biçimi bozuk barkodlar (12 haneli, 14 haneli, harf içeren), terazi öneki tanıma, terazi ayrıştırma senaryoları.
- **`BarkodServiceTests`** — 9 test: tekli barkodun 1 adet döndürmesi, koli barkodunun çarpan kadar miktar döndürmesi, terazi barkodunun gramajı doğru kilograma çevirmesi, geçersiz kontrol hanesinin reddedilmesi, tanımsız barkoda ve fiyatı girilmemiş ürüne anlamlı hata mesajı verilmesi.

`BarkodService` sınıfının veritabanına doğrudan bağımlı olmaması için `IBarkodRepository` arayüzü çıkarıldı; testlerde gerçek SQL Server yerine basit bir sözlük (`Dictionary`) üzerinde çalışan `SahteBarkodRepository` adlı bir sahte (mock/stub) nesne kullanıldı. Bu sayede testler veritabanı bağlantısı olmadan, toplamda yaklaşık 28 milisaniyede çalışabilmektedir. Toplam **29 test** yazılmış ve tamamı başarıyla geçmiştir.

### Son doğrulama

`GET /Barkod/Coz?barkod=...` uç noktası gerçek veritabanına karşı test edildi: tekli barkod 1 adet, koli barkodu 12 adet, terazi barkodu (1.25 kg domates) doğru tutarla (31.13 TL) doğru şekilde döndü; geçersiz kontrol haneli ve tanımsız barkodlar anlamlı hata mesajlarıyla reddedildi. Barkod okutularak yapılan mal kabul testinde bir ürünün depo bazlı stok bakiyesinin (48 → 72 adet) doğru şekilde arttığı, hareketin `StokHareket` tablosuna kaynak tipi ve açıklamasıyla birlikte kaydedildiği doğrulandı.
