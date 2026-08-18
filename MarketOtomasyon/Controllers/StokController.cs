using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

public class StokController : Controller
{
    private const int SayfaBoyutu = 20;
    private const int SonHareketAdedi = 15;

    private readonly StokRepository _stokRepository;
    private readonly DepoRepository _depoRepository;
    private readonly StokService _stokService;
    private readonly BarkodService _barkodService;

    public StokController(
        StokRepository stokRepository,
        DepoRepository depoRepository,
        StokService stokService,
        BarkodService barkodService)
    {
        _stokRepository = stokRepository;
        _depoRepository = depoRepository;
        _stokService = stokService;
        _barkodService = barkodService;
    }

    public async Task<IActionResult> Index(string? arama, bool sadeceKritik = false, int sayfa = 1, CancellationToken ct = default)
    {
        if (sayfa < 1) sayfa = 1;

        var (satirlar, toplam) = await _stokRepository.BakiyeListesiAsync(arama, sadeceKritik, sayfa, SayfaBoyutu, ct);

        return View(new StokListeVm
        {
            Arama = arama,
            SadeceKritik = sadeceKritik,
            Sayfa = sayfa,
            SayfaBoyutu = SayfaBoyutu,
            ToplamKayit = toplam,
            Satirlar = satirlar
        });
    }

    [HttpGet]
    public async Task<IActionResult> Giris(CancellationToken ct)
        => View(await FormHazirlaAsync(new MalKabulVm(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Giris(MalKabulVm form, CancellationToken ct)
    {
        // Barkod girildiyse urunu ondan cozeriz; kasiyer urun listesinde aramak zorunda kalmaz.
        if (!string.IsNullOrWhiteSpace(form.Barkod))
        {
            var sonuc = await _barkodService.CozAsync(form.Barkod, ct);
            if (!sonuc.Basarili)
                ModelState.AddModelError(nameof(form.Barkod), sonuc.Hata!);
            else
                form.UrunId = sonuc.UrunId;
        }

        if (form.UrunId <= 0)
            ModelState.AddModelError(nameof(form.UrunId), "Urun secilmedi veya barkoddan cozulemedi.");

        if (form.DepoId <= 0)
            ModelState.AddModelError(nameof(form.DepoId), "Depo seciniz.");

        if (form.Miktar <= 0)
            ModelState.AddModelError(nameof(form.Miktar), "Miktar sifirdan buyuk olmalidir.");

        if (!ModelState.IsValid)
            return View(await FormHazirlaAsync(form, ct));

        var yeniBakiye = await _stokService.MalKabulAsync(form.UrunId, form.DepoId, form.Miktar, form.Aciklama, ct);

        TempData["Mesaj"] = $"Giris islendi. Yeni bakiye: {yeniBakiye:0.###}";
        return RedirectToAction(nameof(Giris));
    }

    private async Task<MalKabulVm> FormHazirlaAsync(MalKabulVm form, CancellationToken ct)
    {
        form.Depolar = await _depoRepository.AktifleriGetirAsync(ct);
        form.SonHareketler = await _stokRepository.SonHareketlerAsync(SonHareketAdedi, ct);
        return form;
    }
}
