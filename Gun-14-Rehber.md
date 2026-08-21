# Gün 14 — Vardiya ve Z Raporu (uygulama rehberi)

Mevcut durum: `Vardiya` tablosu (`Data/Sql/01_ilk_sema.sql:88`) ve `VardiyaRepository`
zaten var. `KasaController.VardiyaIdAsync` açık vardiya yoksa 0 TL ile otomatik açıyor.
Bu gün o geçici davranışı gerçek bir vardiya ekranına çeviriyoruz.

Sıra önemli: 1 → 14.

---

## 1) YENİ DOSYA: `MarketOtomasyon/Data/Sql/05_vardiya.sql`

Nakit iadeyi doğru vardiyaya yazabilmek için `Iade` tablosuna `VardiyaId` ekliyoruz.
(Fiş üstünden gitmek yanlış olur: 3 gün önceki fişin iadesi bugünkü kasadan çıkar.)

```sql
/* =========================================================
   Vardiya kapanisi ve Z raporu
   ---------------------------------------------------------
   Iade, olustugu ANDAKI vardiyaya baglanir; iade edilen fisin
   vardiyasina degil. Kasadan para o an cikar.
   Eski satirlar icin NULL kalir.
   ========================================================= */

USE MarketOtomasyon;
GO

IF COL_LENGTH('Iade', 'VardiyaId') IS NULL
BEGIN
    ALTER TABLE Iade ADD VardiyaId INT NULL;
END
GO

IF OBJECT_ID('FK_Iade_Vardiya', 'F') IS NULL
BEGIN
    ALTER TABLE Iade
        ADD CONSTRAINT FK_Iade_Vardiya FOREIGN KEY (VardiyaId) REFERENCES Vardiya(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Iade_Vardiya')
    CREATE INDEX IX_Iade_Vardiya ON Iade (VardiyaId);
GO

-- Z raporunda fisler vardiyaya gore taraniyor.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fis_Vardiya')
    CREATE INDEX IX_Fis_Vardiya ON Fis (VardiyaId, Durum);
GO
```

Bu dosyayı SSMS'te (veya `sqlcmd`) çalıştır.

---

## 2) `MarketOtomasyon/Models/Entities/Iade.cs`

`public int FisId { get; set; }` satırının **hemen altına** ekle:

```csharp
    /// <summary>Iadenin yapildigi andaki acik vardiya. Fisin vardiyasi degil.</summary>
    public int? VardiyaId { get; set; }
```

---

## 3) `MarketOtomasyon/Data/Repositories/IadeRepository.cs`

`SqlIadeEkle` sabitindeki INSERT'ü değiştir (iki satır):

```csharp
INSERT INTO Iade (IadeNo, FisId, VardiyaId, KullaniciId, ToplamTutar, OdemeTipi, Aciklama)
OUTPUT INSERTED.Id, INSERTED.IadeNo
VALUES (@iadeNo, @FisId, @VardiyaId, @KullaniciId, @ToplamTutar, @OdemeTipi, @Aciklama);
```

---

## 4) `MarketOtomasyon/Data/Repositories/VardiyaRepository.cs`

Dosyanın üstündeki `using`lere ekle:

```csharp
using MarketOtomasyon.Models.ViewModels;
```

`SqlAc` sabitinin **altına** şu SQL sabitlerini ekle:

```csharp
    private const string SqlIdIleGetir = @"
SELECT Id, KullaniciId, AcilisTarihi, AcilisTutari, KapanisTarihi,
       SayilanTutar, BeklenenTutar, Fark, Durum
FROM Vardiya
WHERE Id = @vardiyaId;";

    // Durum = 1 sarti: ayni vardiya iki kez kapatilamaz.
    private const string SqlKapat = @"
UPDATE Vardiya
SET KapanisTarihi = SYSUTCDATETIME(),
    SayilanTutar  = @sayilanTutar,
    BeklenenTutar = @beklenenTutar,
    Fark          = @fark,
    Durum         = 2
WHERE Id = @vardiyaId AND Durum = 1;";

    // Z raporu tek sorguda. Odeme.Tutar = fise mahsup edilen tutar,
    // alinan nakit degil; para ustu geri verildigi icin kasada kalan budur.
    private const string SqlZRapor = @"
SELECT
    v.Id                                AS VardiyaId,
    v.KullaniciId,
    v.AcilisTarihi,
    v.AcilisTutari,
    v.KapanisTarihi,
    v.SayilanTutar,
    v.Durum,
    ISNULL(s.FisSayisi, 0)              AS FisSayisi,
    ISNULL(s.Ciro, 0)                   AS Ciro,
    ISNULL(s.ToplamIndirim, 0)          AS ToplamIndirim,
    ISNULL(s.ToplamKdv, 0)              AS ToplamKdv,
    ISNULL(o.Nakit, 0)                  AS NakitSatis,
    ISNULL(o.Kart, 0)                   AS KartSatis,
    ISNULL(o.Puan, 0)                   AS PuanSatis,
    ISNULL(i.IadeSayisi, 0)             AS IadeSayisi,
    ISNULL(i.IadeToplam, 0)             AS IadeToplam,
    ISNULL(i.NakitIade, 0)              AS NakitIade
FROM Vardiya v
OUTER APPLY (
    SELECT COUNT(*)              AS FisSayisi,
           SUM(f.GenelToplam)    AS Ciro,
           SUM(f.ToplamIndirim)  AS ToplamIndirim,
           SUM(f.ToplamKdv)      AS ToplamKdv
    FROM Fis f
    WHERE f.VardiyaId = v.Id AND f.Durum = 2
) s
OUTER APPLY (
    SELECT SUM(CASE WHEN o2.Tip = 1 THEN o2.Tutar END) AS Nakit,
           SUM(CASE WHEN o2.Tip = 2 THEN o2.Tutar END) AS Kart,
           SUM(CASE WHEN o2.Tip = 3 THEN o2.Tutar END) AS Puan
    FROM Odeme o2
    JOIN Fis f2 ON f2.Id = o2.FisId
    WHERE f2.VardiyaId = v.Id AND f2.Durum = 2
) o
OUTER APPLY (
    SELECT COUNT(*)                                               AS IadeSayisi,
           SUM(i2.ToplamTutar)                                    AS IadeToplam,
           SUM(CASE WHEN i2.OdemeTipi = 1 THEN i2.ToplamTutar END) AS NakitIade
    FROM Iade i2
    WHERE i2.VardiyaId = v.Id
) i
WHERE v.Id = @vardiyaId;";

    private const string SqlSonKapananlar = @"
SELECT TOP (@adet) Id, KullaniciId, AcilisTarihi, AcilisTutari, KapanisTarihi,
       SayilanTutar, BeklenenTutar, Fark, Durum
FROM Vardiya
WHERE Durum = 2
ORDER BY KapanisTarihi DESC;";
```

Sonra sınıfın sonuna (son `}` işaretinin üstüne) metotları ekle:

```csharp
    public async Task<Vardiya?> GetirAsync(int vardiyaId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Vardiya>(
            new CommandDefinition(SqlIdIleGetir, new { vardiyaId }, cancellationToken: ct));
    }

    /// <summary>Etkilenen satir sayisi. 0 ise vardiya zaten kapaliydi.</summary>
    public async Task<int> KapatAsync(
        int vardiyaId, decimal sayilanTutar, decimal beklenenTutar, decimal fark,
        CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            SqlKapat, new { vardiyaId, sayilanTutar, beklenenTutar, fark }, cancellationToken: ct));
    }

    public async Task<ZRaporVm?> ZRaporAsync(int vardiyaId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ZRaporVm>(
            new CommandDefinition(SqlZRapor, new { vardiyaId }, cancellationToken: ct));
    }

    public async Task<List<Vardiya>> SonKapananlarAsync(int adet = 20, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var liste = await conn.QueryAsync<Vardiya>(
            new CommandDefinition(SqlSonKapananlar, new { adet }, cancellationToken: ct));
        return liste.AsList();
    }
```

---

## 5) YENİ DOSYA: `MarketOtomasyon/Models/ViewModels/ZRaporVm.cs`

```csharp
namespace MarketOtomasyon.Models.ViewModels;

/// <summary>
/// Vardiya ozeti. Vardiya acikken de okunabilir (X raporu),
/// kapandiktan sonra okununca Z raporu olur.
/// </summary>
public class ZRaporVm
{
    public int VardiyaId { get; set; }
    public int KullaniciId { get; set; }
    public DateTime AcilisTarihi { get; set; }
    public decimal AcilisTutari { get; set; }
    public DateTime? KapanisTarihi { get; set; }
    public decimal? SayilanTutar { get; set; }
    public byte Durum { get; set; }

    public int FisSayisi { get; set; }
    public decimal Ciro { get; set; }
    public decimal ToplamIndirim { get; set; }
    public decimal ToplamKdv { get; set; }

    public decimal NakitSatis { get; set; }
    public decimal KartSatis { get; set; }
    public decimal PuanSatis { get; set; }

    public int IadeSayisi { get; set; }
    public decimal IadeToplam { get; set; }
    public decimal NakitIade { get; set; }

    public bool Acik => Durum == 1;

    /// <summary>Kasada olmasi gereken nakit: acilis + nakit satis - nakit iade.</summary>
    public decimal BeklenenTutar => AcilisTutari + NakitSatis - NakitIade;

    /// <summary>Sayilan - beklenen. Artida ise kasa fazlasi, eksideyse kasa acigi.</summary>
    public decimal? Fark => SayilanTutar is null ? null : SayilanTutar.Value - BeklenenTutar;

    public decimal NetCiro => Ciro - IadeToplam;
}
```

---

## 6) YENİ DOSYA: `MarketOtomasyon/Models/ViewModels/VardiyaEkranVm.cs`

```csharp
using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

public class VardiyaEkranVm
{
    /// <summary>Acik vardiya yoksa null; ekran "vardiya ac" formunu gosterir.</summary>
    public ZRaporVm? Acik { get; set; }

    public List<Vardiya> SonKapananlar { get; set; } = new();

    public decimal AcilisTutari { get; set; }
    public decimal SayilanTutar { get; set; }

    public string? Hata { get; set; }
}
```

---

## 7) YENİ DOSYA: `MarketOtomasyon/Services/VardiyaService.cs`

```csharp
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Vardiya acma/kapatma ve Z raporu.
/// Beklenen tutar tek yerde (ZRaporVm.BeklenenTutar) hesaplanir; kapanista
/// o deger Vardiya satirina donduruluyor ki rapor sonradan degismesin.
/// </summary>
public class VardiyaService
{
    private const int SonKapananAdet = 10;

    private readonly VardiyaRepository _vardiyaRepository;

    public VardiyaService(VardiyaRepository vardiyaRepository) => _vardiyaRepository = vardiyaRepository;

    public async Task<VardiyaEkranVm> EkranAsync(int kullaniciId, CancellationToken ct = default)
    {
        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        return new VardiyaEkranVm
        {
            Acik = acik is null ? null : await _vardiyaRepository.ZRaporAsync(acik.Id, ct),
            SonKapananlar = await _vardiyaRepository.SonKapananlarAsync(SonKapananAdet, ct)
        };
    }

    public async Task<string?> AcAsync(int kullaniciId, decimal acilisTutari, CancellationToken ct = default)
    {
        if (acilisTutari < 0)
            return "Acilis tutari negatif olamaz.";

        if (await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct) is not null)
            return "Zaten acik bir vardiyaniz var. Once onu kapatin.";

        await _vardiyaRepository.AcAsync(kullaniciId, acilisTutari, ct);
        return null;
    }

    /// <summary>Basarili olursa kapanmis vardiyanin Z raporunu dondurur.</summary>
    public async Task<(ZRaporVm? Rapor, string? Hata)> KapatAsync(
        int kullaniciId, decimal sayilanTutar, CancellationToken ct = default)
    {
        if (sayilanTutar < 0)
            return (null, "Sayilan tutar negatif olamaz.");

        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        if (acik is null)
            return (null, "Acik vardiya bulunamadi.");

        var rapor = await _vardiyaRepository.ZRaporAsync(acik.Id, ct);
        if (rapor is null)
            return (null, "Vardiya ozeti okunamadi.");

        rapor.SayilanTutar = sayilanTutar;
        var beklenen = rapor.BeklenenTutar;
        var fark = sayilanTutar - beklenen;

        var etkilenen = await _vardiyaRepository.KapatAsync(acik.Id, sayilanTutar, beklenen, fark, ct);
        if (etkilenen != 1)
            return (null, "Vardiya baska bir islemde kapatilmis.");

        return (await _vardiyaRepository.ZRaporAsync(acik.Id, ct), null);
    }

    public Task<ZRaporVm?> RaporAsync(int vardiyaId, CancellationToken ct = default)
        => _vardiyaRepository.ZRaporAsync(vardiyaId, ct);
}
```

---

## 8) İadeyi vardiyaya bağla

### 8a) `Services/IadeService.cs`

`IadeEtAsync` imzasını değiştir:

```csharp
    public async Task<IadeSonucVm> IadeEtAsync(
        IadeFormVm form, int kullaniciId, int vardiyaId, CancellationToken ct = default)
```

Aynı dosyada `_iadeRepository.EkleAsync(conn, tx, new Iade { ... })` bloğunda
`FisId = fis.FisId,` satırının altına ekle:

```csharp
            VardiyaId = vardiyaId,
```

### 8b) `Controllers/IadeController.cs`

Üste `using MarketOtomasyon.Data.Repositories;` ekle. Ctor'u değiştir:

```csharp
    private readonly IadeService _iadeService;
    private readonly VardiyaRepository _vardiyaRepository;

    public IadeController(IadeService iadeService, VardiyaRepository vardiyaRepository)
    {
        _iadeService = iadeService;
        _vardiyaRepository = vardiyaRepository;
    }
```

`Olustur` metodundaki `var sonuc = await _iadeService.IadeEtAsync(form, GeciciKullaniciId, ct);`
satırını şununla değiştir:

```csharp
        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        if (acik is null)
        {
            var bos = await _iadeService.AraAsync(form.FisNo, ct);
            bos.Form = form;
            bos.Hata = "Acik vardiya yok. Once vardiya acin.";
            return View("Index", bos);
        }

        var sonuc = await _iadeService.IadeEtAsync(form, GeciciKullaniciId, acik.Id, ct);
```

---

## 9) `Controllers/KasaController.cs` — otomatik açmayı kaldır

`VardiyaIdAsync` metodunu şununla değiştir:

```csharp
    /// <summary>Acik vardiya yoksa -1 doner; cagiran uc 409 ile uyarir.</summary>
    private async Task<int> VardiyaIdAsync(CancellationToken ct)
        => (await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct))?.Id ?? -1;
```

Sınıfın başındaki XML yorumunda "acik vardiya yoksa otomatik aciliyor" cümlesini sil.

`Sepet`, `Ekle`, `MiktarGuncelle`, `SatirSil`, `SatirIndirimi`, `FisIndirimi`, `Iptal`
metotlarının başına şunu koy ve içerideki `await VardiyaIdAsync(ct)` çağrılarını
`vardiyaId` ile değiştir:

```csharp
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0)
            return Conflict(new { hata = "Acik vardiya yok. Vardiya ekranindan vardiya acin." });
```

---

## 10) YENİ DOSYA: `MarketOtomasyon/Controllers/VardiyaController.cs`

```csharp
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

public class VardiyaController : Controller
{
    // GECICI: oturum acma yok, kasiyer sabit.
    private const int GeciciKullaniciId = 1;

    private readonly VardiyaService _vardiyaService;

    public VardiyaController(VardiyaService vardiyaService) => _vardiyaService = vardiyaService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _vardiyaService.EkranAsync(GeciciKullaniciId, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ac(decimal acilisTutari, CancellationToken ct)
    {
        var hata = await _vardiyaService.AcAsync(GeciciKullaniciId, acilisTutari, ct);
        if (hata is not null)
        {
            var vm = await _vardiyaService.EkranAsync(GeciciKullaniciId, ct);
            vm.Hata = hata;
            vm.AcilisTutari = acilisTutari;
            return View(nameof(Index), vm);
        }

        TempData["Mesaj"] = $"Vardiya acildi. Acilis tutari: {acilisTutari:N2} TL";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kapat(decimal sayilanTutar, CancellationToken ct)
    {
        var (rapor, hata) = await _vardiyaService.KapatAsync(GeciciKullaniciId, sayilanTutar, ct);
        if (hata is not null)
        {
            var vm = await _vardiyaService.EkranAsync(GeciciKullaniciId, ct);
            vm.Hata = hata;
            vm.SayilanTutar = sayilanTutar;
            return View(nameof(Index), vm);
        }

        return RedirectToAction(nameof(Rapor), new { id = rapor!.VardiyaId });
    }

    [HttpGet]
    public async Task<IActionResult> Rapor(int id, CancellationToken ct)
    {
        var rapor = await _vardiyaService.RaporAsync(id, ct);
        return rapor is null ? NotFound() : View(rapor);
    }
}
```

---

## 11) YENİ DOSYA: `MarketOtomasyon/Views/Vardiya/Index.cshtml`

```cshtml
@model MarketOtomasyon.Models.ViewModels.VardiyaEkranVm
@using System.Globalization
@{
    ViewData["Title"] = "Vardiya";
    var tr = new CultureInfo("tr-TR");
    string Para(decimal d) => d.ToString("N2", tr);
}

@if (TempData["Mesaj"] is string mesaj)
{
    <div class="alert alert-success">@mesaj</div>
}
@if (!string.IsNullOrWhiteSpace(Model.Hata))
{
    <div class="alert alert-danger">@Model.Hata</div>
}

@if (Model.Acik is null)
{
    <div class="card mb-4">
        <div class="card-header">Vardiya aç</div>
        <div class="card-body">
            <form method="post" asp-action="Ac" class="row g-2 align-items-end">
                <div class="col-md-4">
                    <label class="form-label" for="acilisTutari">Açılış tutarı (kasadaki nakit)</label>
                    <input type="number" step="0.01" min="0" class="form-control" id="acilisTutari"
                           name="acilisTutari" value="@Model.AcilisTutari.ToString(CultureInfo.InvariantCulture)"
                           autofocus required />
                </div>
                <div class="col-auto">
                    <button type="submit" class="btn btn-primary">
                        <i class="bi bi-play-circle"></i> Vardiyayı Aç
                    </button>
                </div>
            </form>
        </div>
    </div>
}
else
{
    var r = Model.Acik;

    <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
            <span>Açık vardiya <strong>#@r.VardiyaId</strong></span>
            <span class="badge bg-success">
                @r.AcilisTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm") itibarıyla açık
            </span>
        </div>
        <div class="card-body">
            <table class="table table-sm mb-0">
                <tbody>
                    <tr><td>Açılış tutarı</td><td class="text-end tutar">@Para(r.AcilisTutari)</td></tr>
                    <tr><td>Nakit satış</td><td class="text-end tutar">@Para(r.NakitSatis)</td></tr>
                    <tr><td>Nakit iade</td><td class="text-end tutar text-danger">-@Para(r.NakitIade)</td></tr>
                    <tr class="table-light fw-bold">
                        <td>Kasada olması gereken</td>
                        <td class="text-end tutar">@Para(r.BeklenenTutar)</td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>

    <div class="card mb-4">
        <div class="card-header">Kapanış</div>
        <div class="card-body">
            <form method="post" asp-action="Kapat" class="row g-2 align-items-end">
                <div class="col-md-4">
                    <label class="form-label" for="sayilanTutar">Sayılan tutar</label>
                    <input type="number" step="0.01" min="0" class="form-control" id="sayilanTutar"
                           name="sayilanTutar" value="@Model.SayilanTutar.ToString(CultureInfo.InvariantCulture)"
                           autofocus required />
                </div>
                <div class="col-auto">
                    <button type="submit" class="btn btn-danger">
                        <i class="bi bi-stop-circle"></i> Vardiyayı Kapat ve Z Raporu Al
                    </button>
                </div>
            </form>
        </div>
    </div>
}

@if (Model.SonKapananlar.Count > 0)
{
    <div class="card">
        <div class="card-header">Son kapanan vardiyalar</div>
        <table class="table table-sm mb-0">
            <thead>
                <tr>
                    <th>#</th><th>Kapanış</th>
                    <th class="text-end">Beklenen</th><th class="text-end">Sayılan</th>
                    <th class="text-end">Fark</th><th></th>
                </tr>
            </thead>
            <tbody>
            @foreach (var v in Model.SonKapananlar)
            {
                <tr>
                    <td>@v.Id</td>
                    <td>@v.KapanisTarihi?.ToLocalTime().ToString("dd.MM.yyyy HH:mm")</td>
                    <td class="text-end tutar">@Para(v.BeklenenTutar ?? 0)</td>
                    <td class="text-end tutar">@Para(v.SayilanTutar ?? 0)</td>
                    <td class="text-end tutar @((v.Fark ?? 0) == 0 ? "" : "text-danger")">@Para(v.Fark ?? 0)</td>
                    <td class="text-end">
                        <a class="btn btn-sm btn-outline-secondary" asp-action="Rapor" asp-route-id="@v.Id">Z raporu</a>
                    </td>
                </tr>
            }
            </tbody>
        </table>
    </div>
}
```

> Not: ASP.NET Core, `<form method="post">` içine antiforgery token'ı otomatik ekler
> (Tag Helper aktifse). `Views/_ViewImports.cshtml` içinde `@addTagHelper` satırı
> zaten var, o yüzden ayrıca `@Html.AntiForgeryToken()` yazmana gerek yok.

---

## 12) YENİ DOSYA: `MarketOtomasyon/Views/Vardiya/Rapor.cshtml`

```cshtml
@model MarketOtomasyon.Models.ViewModels.ZRaporVm
@using System.Globalization
@{
    ViewData["Title"] = $"Z Raporu #{Model.VardiyaId}";
    var tr = new CultureInfo("tr-TR");
    string Para(decimal d) => d.ToString("N2", tr);
    var fark = Model.Fark ?? 0;
}

<div class="card">
    <div class="card-header d-flex justify-content-between align-items-center">
        <span>Z Raporu — Vardiya <strong>#@Model.VardiyaId</strong></span>
        <span class="badge bg-light text-dark">
            @Model.AcilisTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            – @(Model.KapanisTarihi?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "açık")
        </span>
    </div>
    <div class="card-body">
        <h6 class="text-muted">Ciro</h6>
        <table class="table table-sm">
            <tbody>
                <tr><td>İşlem (fiş) sayısı</td><td class="text-end">@Model.FisSayisi</td></tr>
                <tr><td>Brüt ciro</td><td class="text-end tutar">@Para(Model.Ciro)</td></tr>
                <tr><td>Toplam indirim</td><td class="text-end tutar">@Para(Model.ToplamIndirim)</td></tr>
                <tr><td>Toplam KDV</td><td class="text-end tutar">@Para(Model.ToplamKdv)</td></tr>
                <tr><td>İade (@Model.IadeSayisi adet)</td><td class="text-end tutar text-danger">-@Para(Model.IadeToplam)</td></tr>
                <tr class="table-light fw-bold"><td>Net ciro</td><td class="text-end tutar">@Para(Model.NetCiro)</td></tr>
            </tbody>
        </table>

        <h6 class="text-muted mt-4">Ödeme dağılımı</h6>
        <table class="table table-sm">
            <tbody>
                <tr><td>Nakit</td><td class="text-end tutar">@Para(Model.NakitSatis)</td></tr>
                <tr><td>Kart</td><td class="text-end tutar">@Para(Model.KartSatis)</td></tr>
                <tr><td>Puan</td><td class="text-end tutar">@Para(Model.PuanSatis)</td></tr>
            </tbody>
        </table>

        <h6 class="text-muted mt-4">Kasa</h6>
        <table class="table table-sm mb-0">
            <tbody>
                <tr><td>Açılış tutarı</td><td class="text-end tutar">@Para(Model.AcilisTutari)</td></tr>
                <tr><td>+ Nakit satış</td><td class="text-end tutar">@Para(Model.NakitSatis)</td></tr>
                <tr><td>− Nakit iade</td><td class="text-end tutar">@Para(Model.NakitIade)</td></tr>
                <tr class="table-light fw-bold"><td>Beklenen tutar</td><td class="text-end tutar">@Para(Model.BeklenenTutar)</td></tr>
                <tr><td>Sayılan tutar</td><td class="text-end tutar">@Para(Model.SayilanTutar ?? 0)</td></tr>
                <tr class="fw-bold @(fark == 0 ? "table-success" : "table-warning")">
                    <td>Fark @(fark > 0 ? "(kasa fazlası)" : fark < 0 ? "(kasa açığı)" : "")</td>
                    <td class="text-end tutar">@Para(fark)</td>
                </tr>
            </tbody>
        </table>
    </div>
</div>

<a class="btn btn-outline-secondary mt-3" asp-action="Index">
    <i class="bi bi-arrow-left"></i> Vardiya ekranı
</a>
```

---

## 13) `MarketOtomasyon/Program.cs`

`builder.Services.AddScoped<IadeService>();` satırının altına ekle:

```csharp
builder.Services.AddScoped<VardiyaService>();
```

---

## 14) `MarketOtomasyon/Views/Shared/_Layout.cshtml`

"Satış" menü başlığı altındaki İade linkinden **sonra** ekle:

```cshtml
                <a class="menu-oge @Aktif("Vardiya")" asp-controller="Vardiya" asp-action="Index">
                    <i class="menu-ikon bi bi-safe2"></i> Vardiya
                </a>
```

---

## Kabul testi (elle)

1. `05_vardiya.sql`'i çalıştır, projeyi başlat.
2. Vardiya → Açılış tutarı **200** ile aç.
3. Kasa → bir ürün ekle, **nakit 100 TL**'lik satış tamamla.
4. Kasa → ikinci satış, **kart 50 TL**.
5. İade → ilk fişten **30 TL**'lik iade yap.
6. Vardiya → "Kasada olması gereken" = 200 + 100 − 30 = **270,00** görünmeli.
7. Sayılan tutar **265** gir, kapat → Z raporunda Fark **−5,00 (kasa açığı)**,
   nakit 100 / kart 50, işlem sayısı 2, iade toplamı 30 çıkmalı.

### Dikkat edilecek iki nokta

- **Kart** ödemesi beklenen nakde girmez (kasada para yok), ama ciroya girer.
- Nakit ödemede **para üstü** de girmez: `Odeme.Tutar` fişe mahsup edilen tutardır,
  alınan tutar değil — kasada kalan doğru olarak budur.
- Eski (VardiyaId'si NULL) iade satırları hiçbir Z raporunda görünmez. Test verisi
  temizse sorun değil; değilse `UPDATE Iade SET VardiyaId = (SELECT VardiyaId FROM Fis WHERE Fis.Id = Iade.FisId) WHERE VardiyaId IS NULL;`
  ile bir defalık doldurabilirsin.
