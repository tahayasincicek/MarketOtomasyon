using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;

namespace MarketOtomasyon.Controllers;

public class IadeController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly IadeService _iadeService;

    public IadeController(IadeService iadeService) => _iadeService = iadeService;

    [HttpGet]
    public async Task<IActionResult> Index(string? fisNo, CancellationToken ct)
        => View(await _iadeService.AraAsync(fisNo, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Olustur(IadeFormVm form, CancellationToken ct)
    {
        var sonuc = await _iadeService.IadeEtAsync(form, GeciciKullaniciId, ct);
        if (!sonuc.Basarili)
        {
            var vm = await _iadeService.AraAsync(form.FisNo, ct);
            vm.Form = form;
            vm.Hata = sonuc.Hata;
            return View("Index", vm);
        }

        TempData["Mesaj"] = $"{sonuc.IadeNo} numarali iade kaydedildi. Para iadesi: {sonuc.ToplamTutar:N2} TL";
        return RedirectToAction(nameof(Index), new { fisNo = form.FisNo });
    }
}

