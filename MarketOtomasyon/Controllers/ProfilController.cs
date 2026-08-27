using System.Security.Claims;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Kullanicinin kendi hesabi. Rol kisiti YOK: her oturum sahibi kendi
/// bilgilerini gorebilmeli ve sifresini degistirebilmeli.
///
/// Hedef kullanici hicbir uctan parametre olarak alinmaz; her zaman
/// User.KullaniciId() kullanilir. Id disaridan gelseydi bir kasiyer
/// baskasinin kaydini duzenlemek icin formu degistirebilirdi.
/// </summary>
[Authorize]
public class ProfilController : Controller
{
    private readonly ProfilService _profilService;

    public ProfilController(ProfilService profilService) => _profilService = profilService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ekran = await _profilService.EkranAsync(User.KullaniciId(), ct);
        return ekran is null ? NotFound() : View(ekran);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KullaniciAdiGuncelle(string? yeniKullaniciAdi, CancellationToken ct)
    {
        var hata = await _profilService.KullaniciAdiGuncelleAsync(
            User.KullaniciId(), yeniKullaniciAdi, ct);

        if (hata is null)
        {
            await OturumuTazeleAsync(ct);
            TempData["Mesaj"] = "Kullanıcı adı güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        var vm = await _profilService.EkranAsync(User.KullaniciId(), ct);
        if (vm is null) return NotFound();

        vm.KullaniciAdiHatasi = hata;
        vm.YeniKullaniciAdi = yeniKullaniciAdi;
        return View(nameof(Index), vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdSoyadGuncelle(string? yeniAdSoyad, CancellationToken ct)
    {
        var hata = await _profilService.AdSoyadGuncelleAsync(User.KullaniciId(), yeniAdSoyad, ct);

        if (hata is null)
        {
            await OturumuTazeleAsync(ct);
            TempData["Mesaj"] = "Ad soyad güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        var ekran = await _profilService.EkranAsync(User.KullaniciId(), ct);
        if (ekran is null) return NotFound();

        ekran.Hata = hata;
        ekran.YeniAdSoyad = yeniAdSoyad;
        return View(nameof(Index), ekran);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SifreDegistir(SifreDegistirVm form, CancellationToken ct)
    {
        var hata = await _profilService.SifreDegistirAsync(User.KullaniciId(), form, ct);

        if (hata is null)
        {
            TempData["Mesaj"] = "Şifreniz değiştirildi.";
            return RedirectToAction(nameof(Index));
        }

        var ekran = await _profilService.EkranAsync(User.KullaniciId(), ct);
        if (ekran is null) return NotFound();

        /* Yalnizca hata mesaji tasiniyor; girilen sifreler ekrana geri
           YAZILMIYOR. Aksi halde sifre HTML kaynaginda ve tarayici
           gecmisinde iz birakirdi. */
        ekran.SifreHatasi = hata;
        return View(nameof(Index), ekran);
    }

    /// <summary>
    /// Oturum cerezini guncel bilgilerle yeniden yazar.
    ///
    /// Cerez giris aninda uretiliyor ve ad soyad ile kullanici adini
    /// KOPYA olarak tasiyor. Tazelenmezse kullanici adini degistirdikten
    /// sonra ust seritte ve tum ekranlarda eski ad gorunmeye devam eder;
    /// kisi degisikligin uygulanmadigini sanip tekrar dener.
    ///
    /// Rol bilerek veritabanindan yeniden okunuyor: cerezdeki rolu
    /// kopyalamak, mudur tarafindan yetkisi dusurulmus bir kullanicinin
    /// eski yetkisini oturum boyunca tasimasina yol acardi.
    /// </summary>
    private async Task OturumuTazeleAsync(CancellationToken ct)
    {
        var ekran = await _profilService.EkranAsync(User.KullaniciId(), ct);
        if (ekran is null) return;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ekran.KullaniciId.ToString()),
            new Claim(ClaimTypes.Name, ekran.AdSoyad),
            new Claim(ClaimTypes.Role, ekran.RolKodu == Roller.MudurKodu ? Roller.Mudur : Roller.Kasiyer),
            new Claim("kullanici_adi", ekran.KullaniciAdi)
        };

        var kimlik = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(kimlik),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow
            });
    }
}
