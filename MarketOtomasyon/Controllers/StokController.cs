using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;

namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.SatisRolleri)]
public class StokController : Controller
{
    private const int SayfaBoyutu = 20;
    private const int SonHareketAdedi = 15;

    private readonly StokRepository _stokRepository;
    private readonly DepoRepository _depoRepository;
    private readonly StokService _stokService;
    private readonly BarkodService _barkodService;
    private readonly TedarikciRepository _tedarikciRepository;

    public StokController(
        StokRepository stokRepository,
        DepoRepository depoRepository,
        StokService stokService,
        BarkodService barkodService,
        TedarikciRepository tedarikciRepository)
    {
        _stokRepository = stokRepository;
        _depoRepository = depoRepository;
        _stokService = stokService;
        _barkodService = barkodService;
        _tedarikciRepository = tedarikciRepository;
    }

    public async Task<IActionResult> Index(string? arama, bool sadeceKritik = false, int sayfa = 1, CancellationToken ct = default)
    {
        if (sayfa < 1) sayfa = 1;

        var (satirlar, toplam) = await _stokRepository.BakiyeListesiAsync(arama, sadeceKritik, sayfa, SayfaBoyutu, ct);

        return View(new StokListeVm
        {
            Arama = arama,
            SadeceKritik = sadeceKritik,
            Sayfa = sayfa,
            SayfaBoyutu = SayfaBoyutu,
            ToplamKayit = toplam,
            Satirlar = satirlar
        });
    }

    [HttpGet]
    [Authorize(Roles = Roller.Mudur)]
    public async Task<IActionResult> Giris(CancellationToken ct)
        => View(await FormHazirlaAsync(new MalKabulVm(), ct));

    [HttpPost]
    [Authorize(Roles = Roller.Mudur)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Giris(MalKabulVm form, CancellationToken ct)
    {
        // Barkod girildiyse urunu ondan cozeriz; kasiyer urun listesinde aramak zorunda kalmaz.
        if (!string.IsNullOrWhiteSpace(form.Barkod))
        {
            var sonuc = await _barkodService.CozAsync(form.Barkod, ct);
            if (!sonuc.Basarili)
                ModelState.AddModelError(nameof(form.Barkod), sonuc.Hata!);
            else
                form.UrunId = sonuc.UrunId;
        }

        if (form.UrunId <= 0)
            ModelState.AddModelError(nameof(form.UrunId), "Ürün seçilmedi veya barkoddan çözülemedi.");

        if (form.DepoId <= 0)
            ModelState.AddModelError(nameof(form.DepoId), "Depo seçiniz.");

        if (form.Miktar <= 0)
            ModelState.AddModelError(nameof(form.Miktar), "Miktar sıfırdan büyük olmalıdır.");

        if (form.BirimMaliyet <= 0)
            ModelState.AddModelError(nameof(form.BirimMaliyet), "Birim maliyet sıfırdan büyük olmalıdır.");

        if (!ModelState.IsValid)
            return View(await FormHazirlaAsync(form, ct));

        decimal yeniBakiye;
        try
        {
            yeniBakiye = await _stokService.MalKabulAsync(
                form.UrunId,
                form.DepoId,
                form.Miktar,
                form.BirimMaliyet,
                form.Aciklama,
                form.SonKullanmaTarihi,
                form.LotNo,
                form.TedarikciId,
                ct);
        }
        catch (ArgumentException ex)
        {
            // Parti kurallari (son kullanma zorunlulugu, gecmis tarih, lot
            // uzunlugu) servisten exception olarak geliyor. Yakalanmazsa
            // kullanici hata sayfasi gorur; oysa bunlar duzeltilebilir
            // form hatalari.
            var alan = ex.ParamName switch
            {
                "sonKullanmaTarihi" => nameof(form.SonKullanmaTarihi),
                "lotNo" => nameof(form.LotNo),
                "urunId" => nameof(form.UrunId),
                _ => string.Empty
            };

            ModelState.AddModelError(alan, ex.Message.Split(" (Parameter")[0]);
            return View(await FormHazirlaAsync(form, ct));

        }
        TempData["Mesaj"] = $"Giriş ve stok partisi kaydedildi. Yeni bakiye: {yeniBakiye:0.###}";
        return RedirectToAction(nameof(Giris));
    }

    private async Task<MalKabulVm> FormHazirlaAsync(MalKabulVm form, CancellationToken ct)
    {
        form.Depolar = await _depoRepository.AktifleriGetirAsync(ct);
        form.Tedarikciler = await _tedarikciRepository.AktifleriGetirAsync(ct);
        form.SonHareketler = await _stokRepository.SonHareketlerAsync(SonHareketAdedi, ct);
        return form;
    }
}
