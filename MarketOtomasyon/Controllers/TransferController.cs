using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Depolar arasi transfer. Mudure ozel: depolar arasi stok tasimak yetki
/// isteyen bir istir ve kasiyerin gunluk isi degildir.
///
/// Satir ekleme/cikarma istemci tarafinda yapilir (wwwroot/js/transfer.js)
/// ve barkod cozumu icin ortak /Barkod/Coz ucu kullanilir. Sunucuya
/// yalnizca tamamlanmis transfer gelir.
///
/// Bunun sebebi kamera: her barkod okumasinda form gonderilseydi sayfa
/// yenilenir, kamera kapanip yeniden acilirdi. Zayi ve mal kabul
/// ekranlari da ayni nedenle barkodu AJAX ile cozuyor.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class TransferController : Controller
{
    private readonly TransferService _transferService;

    public TransferController(TransferService transferService)
        => _transferService = transferService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await EkranHazirlaAsync(new TransferEkranVm(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tamamla(TransferEkranVm form, CancellationToken ct)
    {
        var (transferNo, hata) = await _transferService.TransferEtAsync(
            form.KaynakDepoId,
            form.HedefDepoId,
            form.Satirlar,
            form.Aciklama,
            User.KullaniciId(),
            ct);

        if (hata is not null)
        {
            form.Hata = hata;
            return View(nameof(Index), await EkranHazirlaAsync(form, ct));
        }

        TempData["Mesaj"] = $"{transferNo} numaralı transfer tamamlandı.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Depolari, gecmisi ve satirlarin kaynak depo bakiyelerini doldurur.
    /// Bakiye yalnizca hata sonrasi ekran yeniden cizilirken anlamli;
    /// normal akista satirlar istemcide birikiyor.
    /// </summary>
    private async Task<TransferEkranVm> EkranHazirlaAsync(TransferEkranVm form, CancellationToken ct)
    {
        form.Depolar = await _transferService.DepolarAsync(ct);
        form.SonTransferler = await _transferService.SonTransferlerAsync(ct);

        if (form.KaynakDepoId > 0)
        {
            foreach (var satir in form.Satirlar)
                satir.KaynakBakiye = await _transferService.BakiyeAsync(
                    satir.UrunId, form.KaynakDepoId, ct);
        }

        return form;
    }
}
