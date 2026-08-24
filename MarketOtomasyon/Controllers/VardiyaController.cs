using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Vardiya acma/kapatma ve Z raporu.
///
/// GECICI: oturum acma henuz yok, kasiyer sabit (Id 1) kabul ediliyor.
/// </summary>
public class VardiyaController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly VardiyaService _vardiyaService;

    public VardiyaController(VardiyaService vardiyaService) => _vardiyaService = vardiyaService;

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl, CancellationToken ct)
    {
        var vm = await _vardiyaService.EkranAsync(GeciciKullaniciId, ct);
        vm.ReturnUrl = YerelDonusAdresi(returnUrl);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ac(decimal acilisTutari, string? returnUrl, CancellationToken ct)
    {
        returnUrl = YerelDonusAdresi(returnUrl);
        var hata = await _vardiyaService.AcAsync(GeciciKullaniciId, acilisTutari, ct);
        if (hata is not null)
        {
            var vm = await _vardiyaService.EkranAsync(GeciciKullaniciId, ct);
            vm.Hata = hata;
            vm.AcilisTutari = acilisTutari;
            vm.ReturnUrl = returnUrl;
            return View(nameof(Index), vm);
        }

        TempData["Mesaj"] = $"Vardiya açıldı. Açılış tutarı: {acilisTutari:N2} TL";
        return returnUrl is not null ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    // Kapanistan sonra dogrudan Z raporuna gidilir: kasiyerin gormesi gereken sayfa odur.
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

    private string? YerelDonusAdresi(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
