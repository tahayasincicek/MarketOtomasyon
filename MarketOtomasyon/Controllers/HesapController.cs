using System.Security.Claims;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MarketOtomasyon.Controllers;

[Route("Hesap/[action]")]
public sealed class HesapController : Controller
{
    private readonly KimlikDogrulamaService _kimlikDogrulamaService;

    public HesapController(KimlikDogrulamaService kimlikDogrulamaService)
        => _kimlikDogrulamaService = kimlikDogrulamaService;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Giris(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new GirisVm { ReturnUrl = YerelDonusAdresi(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("giris")]
    public async Task<IActionResult> Giris(GirisVm form, CancellationToken ct)
    {
        form.ReturnUrl = YerelDonusAdresi(form.ReturnUrl);
        if (!ModelState.IsValid) return View(form);

        var kullanici = await _kimlikDogrulamaService.GirisDogrulaAsync(
            form.KullaniciAdi,
            form.Sifre,
            ct);

        if (kullanici is null)
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
            return View(form);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, kullanici.Id.ToString()),
            new Claim(ClaimTypes.Name, kullanici.AdSoyad),
            new Claim(ClaimTypes.Role, Roller.Ad(kullanici.Rol)),
            new Claim("kullanici_adi", kullanici.KullaniciAdi)
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

        return form.ReturnUrl is not null
            ? LocalRedirect(form.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cikis()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Giris));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Yetkisiz() => View();

    private string? YerelDonusAdresi(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
