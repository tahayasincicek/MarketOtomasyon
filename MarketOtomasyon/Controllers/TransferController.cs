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
/// Transfer satirlari sunucuda saklanmaz; form her gonderimde satirlarin
/// tamamini tasir. Sepet gibi kalici bir tasarim gerekmiyor cunku transfer
/// tek oturumda tamamlanan bir islem.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class TransferController : Controller
{
    private readonly TransferService _transferService;
    private readonly BarkodService _barkodService;

    public TransferController(TransferService transferService, BarkodService barkodService)
    {
        _transferService = transferService;
        _barkodService = barkodService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await EkranHazirlaAsync(new TransferEkranVm(), ct));

    /// <summary>Barkod okutarak ya da urun secerek listeye satir ekler.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SatirEkle(TransferEkranVm form, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(form.Barkod))
        {
            var sonuc = await _barkodService.CozAsync(form.Barkod, ct);
            if (!sonuc.Basarili)
            {
                form.Hata = sonuc.Hata;
                return View(nameof(Index), await EkranHazirlaAsync(form, ct));
            }

            // Ayni urun ikinci kez okutulursa yeni satir acilmaz, miktar
            // artar: UQ_TransferSatir ayni urunu iki satirda kabul etmiyor
            // ve kasa ekrani da barkod tekrarinda ayni sekilde davraniyor.
            var mevcut = form.Satirlar.FirstOrDefault(s => s.UrunId == sonuc.UrunId);
            if (mevcut is not null)
            {
                mevcut.Miktar += sonuc.Miktar;
            }
            else
            {
                form.Satirlar.Add(new TransferSatirVm
                {
                    UrunId = sonuc.UrunId,
                    UrunKod = sonuc.Kod,
                    UrunAd = sonuc.Ad,
                    Birim = sonuc.Birim,
                    Miktar = sonuc.Miktar
                });
            }
        }

        form.Barkod = null;
        return View(nameof(Index), await EkranHazirlaAsync(form, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SatirSil(TransferEkranVm form, int urunId, CancellationToken ct)
    {
        form.Satirlar.RemoveAll(s => s.UrunId == urunId);
        form.Barkod = null;
        return View(nameof(Index), await EkranHazirlaAsync(form, ct));
    }

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
    /// Bakiye ekranda gosteriliyor ki mudur olmayan stogu tasimaya calisip
    /// hata almasin.
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
