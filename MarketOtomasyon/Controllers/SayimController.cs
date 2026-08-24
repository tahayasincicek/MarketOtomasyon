using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Fiziksel sayim ve zayi/fire girisi.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class SayimController : Controller
{
    private const int SonHareketAdedi = 20;

    private readonly SayimRepository _sayimRepository;
    private readonly DepoRepository _depoRepository;
    private readonly UrunRepository _urunRepository;
    private readonly StokRepository _stokRepository;
    private readonly BarkodService _barkodService;
    private readonly SayimService _sayimService;

    public SayimController(
        SayimRepository sayimRepository,
        DepoRepository depoRepository,
        UrunRepository urunRepository,
        StokRepository stokRepository,
        BarkodService barkodService,
        SayimService sayimService)
    {
        _sayimRepository = sayimRepository;
        _depoRepository = depoRepository;
        _urunRepository = urunRepository;
        _stokRepository = stokRepository;
        _barkodService = barkodService;
        _sayimService = sayimService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int depoId = 0, CancellationToken ct = default)
    {
        var vm = new SayimEkranVm { DepoId = depoId };
        return View(await SayimFormunuHazirlaAsync(vm, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(SayimEkranVm form, CancellationToken ct)
    {
        var sonuc = await _sayimService.SayimKaydetAsync(form, User.KullaniciId(), ct);
        if (!sonuc.Basarili)
        {
            ModelState.AddModelError(string.Empty, sonuc.Hata!);
            return View(nameof(Index), await SayimFormunuHazirlaAsync(form, ct));
        }

        TempData["Mesaj"] = $"Sayım #{sonuc.SayimId} kaydedildi. "
            + $"{sonuc.SayilanSatirSayisi} satır sayıldı, "
            + $"{sonuc.DuzeltmeHareketiSayisi} stok düzeltme hareketi oluştu.";
        return RedirectToAction(nameof(Index), new { depoId = form.DepoId });
    }

    [HttpGet]
    public async Task<IActionResult> Zayi(CancellationToken ct)
        => View(await ZayiFormunuHazirlaAsync(new ZayiEkranVm(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zayi(ZayiEkranVm form, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(form.Barkod))
        {
            var barkod = await _barkodService.CozAsync(form.Barkod, ct);
            if (!barkod.Basarili)
                ModelState.AddModelError(nameof(form.Barkod), barkod.Hata!);
            else
                form.UrunId = barkod.UrunId;
        }

        if (!ModelState.IsValid)
            return View(await ZayiFormunuHazirlaAsync(form, ct));

        var sonuc = await _sayimService.ZayiKaydetAsync(
            form.UrunId, form.DepoId, form.Miktar, form.Sebep, User.KullaniciId(), ct);
        if (!sonuc.Basarili)
        {
            ModelState.AddModelError(string.Empty, sonuc.Hata!);
            return View(await ZayiFormunuHazirlaAsync(form, ct));
        }

        TempData["Mesaj"] = $"Zayi #{sonuc.ZayiId} kaydedildi. Yeni bakiye: {sonuc.YeniBakiye:0.###}.";
        return RedirectToAction(nameof(Zayi));
    }

    private async Task<SayimEkranVm> SayimFormunuHazirlaAsync(
        SayimEkranVm form, CancellationToken ct)
    {
        form.Depolar = await _depoRepository.AktifleriGetirAsync(ct);
        if (form.DepoId == 0 && form.Depolar.Count == 1)
            form.DepoId = form.Depolar[0].Id;

        if (form.DepoId > 0)
        {
            var girilenler = form.Satirlar
                .Where(s => s.SayilanMiktar.HasValue)
                .GroupBy(s => s.UrunId)
                .ToDictionary(g => g.Key, g => g.First().SayilanMiktar);

            form.Satirlar = await _sayimRepository.SayimUrunleriniGetirAsync(form.DepoId, ct);
            foreach (var satir in form.Satirlar)
                if (girilenler.TryGetValue(satir.UrunId, out var miktar))
                    satir.SayilanMiktar = miktar;
        }

        form.SonHareketler = await _stokRepository.SonSayimVeZayiHareketleriAsync(SonHareketAdedi, ct);
        return form;
    }

    private async Task<ZayiEkranVm> ZayiFormunuHazirlaAsync(
        ZayiEkranVm form, CancellationToken ct)
    {
        form.Depolar = await _depoRepository.AktifleriGetirAsync(ct);
        form.Urunler = await _urunRepository.AktifleriGetirAsync(ct);
        form.SonHareketler = await _stokRepository.SonSayimVeZayiHareketleriAsync(SonHareketAdedi, ct);
        if (form.DepoId == 0 && form.Depolar.Count == 1)
            form.DepoId = form.Depolar[0].Id;
        return form;
    }
}
