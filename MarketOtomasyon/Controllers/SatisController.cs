using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Satis kapatma, askiya alma ve fis yazdirma.
/// GECICI: oturum acma yok, kasiyer sabit (Id 1) kabul ediliyor.
/// </summary>
[Route("[controller]/[action]")]
public class SatisController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly SatisService _satisService;
    private readonly VardiyaRepository _vardiyaRepository;
    private readonly FisRepository _fisRepository;
    private readonly OdemeRepository _odemeRepository;
    private readonly KullaniciRepository _kullaniciRepository;

    public SatisController(
        SatisService satisService,
        VardiyaRepository vardiyaRepository,
        FisRepository fisRepository,
        OdemeRepository odemeRepository,
        KullaniciRepository kullaniciRepository)
    {
        _satisService = satisService;
        _vardiyaRepository = vardiyaRepository;
        _fisRepository = fisRepository;
        _odemeRepository = odemeRepository;
        _kullaniciRepository = kullaniciRepository;
    }

    /// <summary>Yazdirilabilir fis. Yeni sekmede acilir, kendi sade duzenini kullanir.</summary>
    [HttpGet("/Satis/Fis/{id:int}")]
    public async Task<IActionResult> Fis(int id, CancellationToken ct)
    {
        var fis = await _fisRepository.GetirAsync(id, ct);
        if (fis is null) return NotFound();

        var satirlar = await _fisRepository.SatirlariGetirAsync(id, ct);
        var sepet = SepetHesaplayici.Topla(satirlar);

        return View(new FisYazdirVm
        {
            Fis = fis,
            Satirlar = sepet.Satirlar,
            KdvKirilimi = sepet.KdvKirilimi,
            Odemeler = await _odemeRepository.FisOdemeleriAsync(id, ct),
            KasiyerAdi = await _kullaniciRepository.AdSoyadGetirAsync(fis.KullaniciId, ct) ?? ""
        });
    }

    [HttpGet]
    public async Task<IActionResult> Bekleyenler(CancellationToken ct)
        => Ok(await _satisService.BekleyenleriGetirAsync(await VardiyaIdAsync(ct), ct));

    [HttpPost]
    public async Task<IActionResult> AskiyaAl(CancellationToken ct)
    {
        var (basarili, hata) = await _satisService.AskiyaAlAsync(await VardiyaIdAsync(ct), ct);
        return basarili ? Ok(new { basarili }) : BadRequest(new { hata });
    }

    [HttpPost]
    public async Task<IActionResult> GeriCagir([FromForm] int fisId, CancellationToken ct)
    {
        var (basarili, hata) = await _satisService.GeriCagirAsync(await VardiyaIdAsync(ct), fisId, ct);
        return basarili ? Ok(new { basarili }) : BadRequest(new { hata });
    }

    private async Task<int> VardiyaIdAsync(CancellationToken ct)
    {
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        return vardiya?.Id ?? await _vardiyaRepository.AcAsync(GeciciKullaniciId, 0, ct);
    }
}
