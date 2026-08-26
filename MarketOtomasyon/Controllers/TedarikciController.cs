using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.Mudur)]
public class TedarikciController : Controller
{
    private readonly TedarikciService _tedarikciService;

    public TedarikciController(TedarikciService tedarikciService)
        => _tedarikciService = tedarikciService;

    [HttpGet]
    public async Task<IActionResult> Index(string? arama, bool sadeceAktif = true, CancellationToken ct = default)
        => View(new TedarikciListeVm
        {
            Arama = arama,
            SadeceAktif = sadeceAktif,
            Satirlar = await _tedarikciService.ListeleAsync(arama, sadeceAktif, ct)
        });

    [HttpGet]
    public IActionResult Ekle() => View(new TedarikciFormVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(TedarikciFormVm form, CancellationToken ct)
    {
        var hata = await _tedarikciService.KaydetAsync(form, ct);
        if (hata is not null)
        {
            ModelState.AddModelError(string.Empty, hata);
            return View(form);
        }

        TempData["Mesaj"] = $"{form.Unvan} eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Duzenle(int id, CancellationToken ct)
    {
        var tedarikci = await _tedarikciService.GetirAsync(id, ct);
        if (tedarikci is null) return NotFound();

        return View(new TedarikciFormVm
        {
            Id = tedarikci.Id,
            Kod = tedarikci.Kod,
            Unvan = tedarikci.Unvan,
            VergiNo = tedarikci.VergiNo,
            VergiDairesi = tedarikci.VergiDairesi,
            Telefon = tedarikci.Telefon,
            Eposta = tedarikci.Eposta,
            Adres = tedarikci.Adres,
            Aktif = tedarikci.Aktif
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(TedarikciFormVm form, CancellationToken ct)
    {
        var hata = await _tedarikciService.KaydetAsync(form, ct);
        if (hata is not null)
        {
            ModelState.AddModelError(string.Empty, hata);
            return View(form);
        }

        TempData["Mesaj"] = $"{form.Unvan} güncellendi.";
        return RedirectToAction(nameof(Index));
    }
}
