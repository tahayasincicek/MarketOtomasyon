using System.Diagnostics;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;

namespace MarketOtomasyon.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UrunRepository _urunRepository;
    private readonly OzetRepository _ozetRepository;
    private readonly VardiyaRepository _vardiyaRepository;

    public HomeController(
        ILogger<HomeController> logger,
        UrunRepository urunRepository,
        OzetRepository ozetRepository,
        VardiyaRepository vardiyaRepository)
    {
        _logger = logger;
        _urunRepository = urunRepository;
        _ozetRepository = ozetRepository;
        _vardiyaRepository = vardiyaRepository;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.IsInRole(Roller.Kasiyer))
            return RedirectToAction("Index", "Kasa");

        // "Bugun" yerel gundur; Tarih kolonlari UTC yazildigi icin
        // gun sinirlari UTC'ye cevrilerek sorgulanir.
        var bugun = DateTime.Now.Date;
        var baslangicUtc = bugun.ToUniversalTime();
        var bitisUtc = bugun.AddDays(1).ToUniversalTime();

        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(User.KullaniciId(), ct);

        return View(new AnaEkranVm
        {
            Gun = bugun,
            Ozet = await _ozetRepository.GunlukOzetAsync(baslangicUtc, bitisUtc, ct),
            AcikVardiyaId = acik?.Id
        });
    }

    /// <summary>Gecici saglik kontrolu: veritabani baglantisini dogrular.</summary>
    public async Task<IActionResult> DbTest(CancellationToken ct)
    {
        var sayi = await _urunRepository.AktifUrunSayisiAsync(ct);
        return Content($"Bağlantı tamam. Aktif ürün sayısı: {sayi}");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
