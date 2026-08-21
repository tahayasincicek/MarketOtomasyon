using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using MarketOtomasyon.Data.Repositories;


namespace MarketOtomasyon.Controllers;

public class IadeController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly IadeService _iadeService;
    private readonly VardiyaRepository _vardiyaRepository;

    public IadeController(IadeService iadeService, VardiyaRepository vardiyaRepository)
    {
        _iadeService = iadeService;
        _vardiyaRepository = vardiyaRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? fisNo, CancellationToken ct)
        => View(await _iadeService.AraAsync(fisNo, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Olustur(IadeFormVm form, CancellationToken ct)
    {
        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        if (acik is null)
        {
            var bos = await _iadeService.AraAsync(form.FisNo, ct);
            bos.Form = form;
            bos.Hata = "Acik vardiya yok. Once vardiya acin.";
            return View("Index", bos);
        }

        var sonuc = await _iadeService.IadeEtAsync(form, GeciciKullaniciId, acik.Id, ct);
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

