using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Alis faturasi girisi. Satir ekleme/cikarma istemci tarafinda yapilir
/// (wwwroot/js/alis-faturasi.js) ve barkod cozumu icin ortak /Barkod/Coz
/// ucu kullanilir - Transfer ekraniyla ayni desen. Sunucuya yalnizca
/// tamamlanmis fatura gelir.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class AlisFaturasiController : Controller
{
    private readonly AlisFaturasiService _faturaService;

    public AlisFaturasiController(AlisFaturasiService faturaService)
        => _faturaService = faturaService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await EkranHazirlaAsync(new AlisFaturasiEkranVm(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(AlisFaturasiEkranVm form, CancellationToken ct)
    {
        var (faturaNo, hata) = await _faturaService.KaydetAsync(
            form.TedarikciId,
            form.DepoId,
            form.FaturaNo,
            form.FaturaTarihi,
            form.Satirlar,
            form.Aciklama,
            User.KullaniciId(),
            ct);

        if (hata is not null)
        {
            form.Hata = hata;
            return View(nameof(Index), await EkranHazirlaAsync(form, ct));
        }

        TempData["Mesaj"] = $"{faturaNo} numaralı fatura kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detay(int id, CancellationToken ct)
    {
        var detay = await _faturaService.DetayGetirAsync(id, ct);
        return detay is null ? NotFound() : View(detay);
    }

    private async Task<AlisFaturasiEkranVm> EkranHazirlaAsync(AlisFaturasiEkranVm form, CancellationToken ct)
    {
        form.Tedarikciler = await _faturaService.TedarikcilerAsync(ct);
        form.Depolar = await _faturaService.DepolarAsync(ct);
        form.SonFaturalar = await _faturaService.SonFaturalarAsync(ct);
        return form;
    }
}
