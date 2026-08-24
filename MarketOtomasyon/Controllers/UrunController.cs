using FluentValidation;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;

namespace MarketOtomasyon.Controllers;

/// <summary>Sadece istek alir, servisi cagirir, View doner. Hesap ve SQL burada yok.</summary>
[Authorize(Roles = Roller.SatisRolleri)]
public class UrunController : Controller
{
    private const int SayfaBoyutu = 20;

    private readonly UrunRepository _urunRepository;
    private readonly KategoriRepository _kategoriRepository;
    private readonly FiyatRepository _fiyatRepository;
    private readonly UrunService _urunService;
    private readonly IValidator<UrunFormVm> _validator;
    private readonly BarkodRepository _barkodRepository;
    private readonly IValidator<BarkodFormVm> _barkodValidator;
    private readonly UrunResimService _urunResimService;

    public UrunController(
        UrunRepository urunRepository,
        KategoriRepository kategoriRepository,
        FiyatRepository fiyatRepository,
        UrunService urunService,
        IValidator<UrunFormVm> validator,
        BarkodRepository barkodRepository,
        IValidator<BarkodFormVm> barkodValidator,
        UrunResimService urunResimService)
    {
        _urunRepository = urunRepository;
        _kategoriRepository = kategoriRepository;
        _fiyatRepository = fiyatRepository;
        _urunService = urunService;
        _validator = validator;
        _barkodRepository = barkodRepository;
        _barkodValidator = barkodValidator;
        _urunResimService = urunResimService;
    }

    /// <summary>
    /// Resmi olmayan urunlerin fotografini Open Food Facts'ten ceker.
    /// Yavas bir islemdir (hiz siniri geregi urun basina ~4.5 sn beklenir);
    /// bu yuzden kasa akisindan degil, yalnizca bu ekrandan cagrilir.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResimleriCek(CancellationToken ct)
    {
        var sonuc = await _urunResimService.TumEksikleriCekAsync(ct);

        if (sonuc.Denenen == 0)
        {
            TempData["Mesaj"] = "Resmi eksik, barkodu olan ürün kalmadı.";
        }
        else
        {
            TempData["Mesaj"] =
                $"{sonuc.Denenen} ürün denendi: {sonuc.Bulunan} resim indirildi, " +
                $"{sonuc.Bulunamayan} üründe kayıt bulunamadı.";
        }

        if (sonuc.Hatalar.Count > 0)
            TempData["Uyari"] = string.Join(" · ", sonuc.Hatalar.Take(3));

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(string? arama, int? kategoriId, bool sadeceAktif = true, int sayfa = 1, CancellationToken ct = default)
    {
        if (sayfa < 1) sayfa = 1;

        var (satirlar, toplam) = await _urunRepository.ListeleAsync(arama, kategoriId, sadeceAktif, sayfa, SayfaBoyutu, ct);

        var vm = new UrunListeVm
        {
            Arama = arama,
            KategoriId = kategoriId,
            SadeceAktif = sadeceAktif,
            Sayfa = sayfa,
            SayfaBoyutu = SayfaBoyutu,
            ToplamKayit = toplam,
            Satirlar = satirlar,
            Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct)
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = Roller.Mudur)]
    public async Task<IActionResult> Ekle(CancellationToken ct)
    {
        var vm = new UrunFormVm { Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct) };
        return View("Form", vm);
    }

    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(UrunFormVm form, CancellationToken ct)
    {
        if (!await DogrulaAsync(form, ct))
        {
            form.Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct);
            return View("Form", form);
        }

        await _urunService.EkleAsync(form, ct);
        TempData["Mesaj"] = $"{form.Ad} eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = Roller.Mudur)]
    public async Task<IActionResult> Duzenle(int id, CancellationToken ct)
    {
        var urun = await _urunRepository.GetirAsync(id, ct);
        if (urun is null) return NotFound();

        var vm = new UrunFormVm
        {
            Id = urun.Id,
            Kod = urun.Kod,
            Ad = urun.Ad,
            KategoriId = urun.KategoriId,
            Birim = urun.Birim,
            KdvOrani = urun.KdvOrani,
            MinStokSeviyesi = urun.MinStokSeviyesi,
            Tartili = urun.Tartili,
            Aktif = urun.Aktif,
            Fiyat = await _fiyatRepository.GuncelFiyatAsync(id, ct) ?? 0,
            Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct)
        };

        return View("Form", vm);
    }

    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(UrunFormVm form, CancellationToken ct)
    {
        if (!await DogrulaAsync(form, ct))
        {
            form.Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct);
            return View("Form", form);
        }

        await _urunService.GuncelleAsync(form, User.KullaniciId(), ct);
        TempData["Mesaj"] = $"{form.Ad} güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// FluentValidation elle cagriliyor: kod benzersizlik kurali veritabanina gittigi icin
    /// asenkron; MVC'nin otomatik dogrulamasi asenkron kurallari calistiramaz.
    /// </summary>
    private async Task<bool> DogrulaAsync(UrunFormVm form, CancellationToken ct)
    {
        var sonuc = await _validator.ValidateAsync(form, ct);
        if (sonuc.IsValid) return true;

        foreach (var hata in sonuc.Errors)
            ModelState.AddModelError(hata.PropertyName, hata.ErrorMessage);

        return false;
    }

    /// <summary>Barkod yonetimi ve fiyat gecmisi tek ekranda.</summary>
    [HttpGet]
    public async Task<IActionResult> Detay(int id, CancellationToken ct)
    {
        var vm = await DetayHazirlaAsync(id, null, ct);
        return vm is null ? NotFound() : View(vm);
    }

    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BarkodEkle(BarkodFormVm form, CancellationToken ct)
    {
        var sonuc = await _barkodValidator.ValidateAsync(form, ct);
        if (!sonuc.IsValid)
        {
            foreach (var hata in sonuc.Errors)
                ModelState.AddModelError($"YeniBarkod.{hata.PropertyName}", hata.ErrorMessage);

            var hataliVm = await DetayHazirlaAsync(form.UrunId, form, ct);
            return hataliVm is null ? NotFound() : View("Detay", hataliVm);
        }

        await _barkodRepository.EkleAsync(new UrunBarkod
        {
            UrunId = form.UrunId,
            Barkod = form.Barkod.Trim(),
            Carpan = form.Carpan,
            Tip = form.Tip
        }, ct);

        TempData["Mesaj"] = $"{form.Barkod} barkodu eklendi.";
        return RedirectToAction(nameof(Detay), new { id = form.UrunId });
    }

    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BarkodSil(int id, int urunId, CancellationToken ct)
    {
        var silinen = await _barkodRepository.SilAsync(id, urunId, ct);
        TempData["Mesaj"] = silinen > 0 ? "Barkod silindi." : "Barkod bulunamadı.";
        return RedirectToAction(nameof(Detay), new { id = urunId });
    }

    private async Task<UrunDetayVm?> DetayHazirlaAsync(int urunId, BarkodFormVm? form, CancellationToken ct)
    {
        var urun = await _urunRepository.GetirAsync(urunId, ct);
        if (urun is null) return null;

        return new UrunDetayVm
        {
            Urun = urun,
            GuncelFiyat = await _fiyatRepository.GuncelFiyatAsync(urunId, ct),
            Barkodlar = await _barkodRepository.UrunBarkodlariAsync(urunId, ct),
            FiyatGecmisi = await _fiyatRepository.GecmisAsync(urunId, ct),
            YeniBarkod = form ?? new BarkodFormVm { UrunId = urunId }
        };
    }
}
