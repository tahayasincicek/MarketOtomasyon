using MarketOtomasyon.Services;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Vardiya acma/kapatma ve Z raporu.
/// </summary>
[Authorize(Roles = Roller.SatisRolleri)]
public class VardiyaController : Controller
{
    private readonly VardiyaService _vardiyaService;

    public VardiyaController(VardiyaService vardiyaService) => _vardiyaService = vardiyaService;

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl, CancellationToken ct)
    {
        var vm = await _vardiyaService.EkranAsync(User.KullaniciId(), ct);
        vm.ReturnUrl = YerelDonusAdresi(returnUrl);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ac(decimal acilisTutari, string? returnUrl, CancellationToken ct)
    {
        returnUrl = YerelDonusAdresi(returnUrl);
        var kullaniciId = User.KullaniciId();

        // Alan bos veya sayi disi gelirse decimal sessizce 0 olur. Ekrandaki
        // required/min yalnizca tarayicida calisir; istek dogrudan gonderilirse
        // vardiya 0 TL ile acilir ve kasa gun sonunda acilis tutari kadar fazla
        // gorunur. Bu yuzden binding hatasi burada da denetlenir.
        var bindingHatasi = TutarBindingHatasi(nameof(acilisTutari));
        if (bindingHatasi is not null)
        {
            var hataliVm = await _vardiyaService.EkranAsync(kullaniciId, ct);
            hataliVm.Hata = bindingHatasi;
            hataliVm.ReturnUrl = returnUrl;
            return View(nameof(Index), hataliVm);
        }

        var hata = await _vardiyaService.AcAsync(kullaniciId, acilisTutari, ct);
        if (hata is not null)
        {
            var vm = await _vardiyaService.EkranAsync(kullaniciId, ct);
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
        var kullaniciId = User.KullaniciId();

        // Kapanista bos alan acilistan daha kotu: sayilan tutar 0 sanilir,
        // Z raporuna beklenen tutar kadar kasa acigi yazilir ve vardiya
        // kapandigi icin geri alinamaz.
        var bindingHatasi = TutarBindingHatasi(nameof(sayilanTutar));
        if (bindingHatasi is not null)
        {
            var hataliVm = await _vardiyaService.EkranAsync(kullaniciId, ct);
            hataliVm.Hata = bindingHatasi;
            return View(nameof(Index), hataliVm);
        }

        var (rapor, hata) = await _vardiyaService.KapatAsync(kullaniciId, sayilanTutar, ct);
        if (hata is not null)
        {
            var vm = await _vardiyaService.EkranAsync(kullaniciId, ct);
            vm.Hata = hata;
            vm.SayilanTutar = sayilanTutar;
            return View(nameof(Index), vm);
        }

        return RedirectToAction(nameof(Rapor), new { id = rapor!.VardiyaId });
    }

    /// <summary>
    /// Z (ya da vardiya acikken X) raporu.
    ///
    /// gomulu=true yalnizca rapor govdesini dondurur; vardiya ekrani
    /// bunu onizleme penceresinde gosteriyor. Fisin yazdirma akisiyla
    /// ayni desen: kasiyer rapora bakmak icin sayfadan ayrilmasin.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Rapor(int id, [FromQuery] bool gomulu, CancellationToken ct)
    {
        var rapor = await _vardiyaService.RaporAsync(id, ct);
        if (rapor is null) return NotFound();

        return gomulu ? PartialView("_RaporIcerik", rapor) : View(rapor);
    }

    /// <summary>
    /// Tutar alanindaki model binding hatasini okunur bir mesaja cevirir.
    /// MVC'nin uretttigi varsayilan mesaj ("The value 'abc' is not valid for
    /// acilisTutari.") hem Ingilizce hem de alan adini oldugu gibi gosterir.
    /// </summary>
    private string? TutarBindingHatasi(string alanAdi)
        => ModelState.GetValidationState(alanAdi) == ModelValidationState.Invalid
            ? "Tutarı rakamla girin. Kuruş için nokta kullanın (örnek: 1500.50)."
            : null;

    private string? YerelDonusAdresi(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
