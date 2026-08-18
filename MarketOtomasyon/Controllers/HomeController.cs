using System.Diagnostics;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UrunRepository _urunRepository;

    public HomeController(ILogger<HomeController> logger, UrunRepository urunRepository)
    {
        _logger = logger;
        _urunRepository = urunRepository;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Gecici saglik kontrolu: veritabani baglantisini dogrular.</summary>
    public async Task<IActionResult> DbTest(CancellationToken ct)
    {
        var sayi = await _urunRepository.AktifUrunSayisiAsync(ct);
        return Content($"Baglanti tamam. Aktif urun sayisi: {sayi}");
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
