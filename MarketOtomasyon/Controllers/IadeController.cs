using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;


namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.SatisRolleri)]
public class IadeController : Controller
{
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
        var kullaniciId = User.KullaniciId();
        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        if (acik is null)
        {
            var bos = await _iadeService.AraAsync(form.FisNo, ct);
            bos.Form = form;
            bos.Hata = "Açık vardiya yok. Önce vardiya açın.";
            return View("Index", bos);
        }

        var sonuc = await _iadeService.IadeEtAsync(form, kullaniciId, acik.Id, ct);
        if (!sonuc.Basarili)
        {
            var vm = await _iadeService.AraAsync(form.FisNo, ct);
            vm.Form = form;
            vm.Hata = sonuc.Hata;
            return View("Index", vm);
        }

        TempData["Mesaj"] = $"{sonuc.IadeNo} numaralı iade kaydedildi. Para iadesi: {sonuc.ToplamTutar:N2} TL";
        return RedirectToAction(nameof(Index), new { fisNo = form.FisNo });
    }
}
