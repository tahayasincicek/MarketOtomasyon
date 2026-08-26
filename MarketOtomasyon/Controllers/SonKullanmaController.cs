using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Son kullanma tarihi takibi: suresi gecmis ve yaklasan partiler.
///
/// Mudure ozel, cunku ekranin tek eylemi zayi yazmak ve zayi stok
/// dusuren, maliyet yazan bir islem.
///
/// Bu ekran bir RAPOR degil IS LISTESIdir: satirlar zayi'ye alindikca
/// listeden duser. Kasadaki satis reddi son savunma hatti; buradaki
/// liste sorunun kasaya hic ulasmamasi icin.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class SonKullanmaController : Controller
{
    private readonly SonKullanmaService _sonKullanmaService;
    private readonly SayimService _sayimService;

    public SonKullanmaController(
        SonKullanmaService sonKullanmaService, SayimService sayimService)
    {
        _sonKullanmaService = sonKullanmaService;
        _sayimService = sayimService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int gunSayisi = 30, int? depoId = null, CancellationToken ct = default)
        => View(await _sonKullanmaService.EkranAsync(gunSayisi, depoId, ct));

    /// <summary>
    /// Secili partinin kalanini zayi'ye alir ve listeye geri doner.
    /// Filtre parametreleri korunur ki kullanici her dusumden sonra
    /// bastan filtrelemek zorunda kalmasin.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZayiyeAl(
        long stokPartiId,
        string? sebep,
        int gunSayisi,
        int? depoId,
        CancellationToken ct)
    {
        var sonuc = await _sayimService.PartiZayiKaydetAsync(
            stokPartiId, sebep, User.KullaniciId(), ct);

        if (sonuc.Basarili)
            TempData["Mesaj"] = $"Parti zayi'ye alındı (zayi #{sonuc.ZayiId}).";
        else
            TempData["Hata"] = sonuc.Hata;

        return RedirectToAction(nameof(Index), new { gunSayisi, depoId });
    }
}
