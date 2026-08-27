using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
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
    public async Task<IActionResult> AdSoyadGuncelle(string? yeniAdSoyad, CancellationToken ct)
    {
        var hata = await _profilService.AdSoyadGuncelleAsync(User.KullaniciId(), yeniAdSoyad, ct);

        if (hata is null)
        {
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
}
