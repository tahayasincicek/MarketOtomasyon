using FluentValidation;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>Sadece istek alir, servisi cagirir, View doner. Hesap ve SQL burada yok.</summary>
public class UrunController : Controller
{
    private const int SayfaBoyutu = 20;

    private readonly UrunRepository _urunRepository;
    private readonly KategoriRepository _kategoriRepository;
    private readonly FiyatRepository _fiyatRepository;
    private readonly UrunService _urunService;
    private readonly IValidator<UrunFormVm> _validator;

    public UrunController(
        UrunRepository urunRepository,
        KategoriRepository kategoriRepository,
        FiyatRepository fiyatRepository,
        UrunService urunService,
        IValidator<UrunFormVm> validator)
    {
        _urunRepository = urunRepository;
        _kategoriRepository = kategoriRepository;
        _fiyatRepository = fiyatRepository;
        _urunService = urunService;
        _validator = validator;
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
    public async Task<IActionResult> Ekle(CancellationToken ct)
    {
        var vm = new UrunFormVm { Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct) };
        return View("Form", vm);
    }

    [HttpPost]
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(UrunFormVm form, CancellationToken ct)
    {
        if (!await DogrulaAsync(form, ct))
        {
            form.Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct);
            return View("Form", form);
        }

        await _urunService.GuncelleAsync(form, ct);
        TempData["Mesaj"] = $"{form.Ad} guncellendi.";
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
}
