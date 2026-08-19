using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Odeme uclari. Kasa ekranindaki odeme penceresi bunlari cagirir.
/// GECICI: oturum acma yok, kasiyer sabit (Id 1) kabul ediliyor.
/// </summary>
[Route("[controller]/[action]")]
public class OdemeController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly OdemeService _odemeService;
    private readonly VardiyaRepository _vardiyaRepository;

    public OdemeController(OdemeService odemeService, VardiyaRepository vardiyaRepository)
    {
        _odemeService = odemeService;
        _vardiyaRepository = vardiyaRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Durum(CancellationToken ct)
        => Ok(await _odemeService.DurumAsync(await VardiyaIdAsync(ct), ct));

    [HttpPost]
    public async Task<IActionResult> Ekle(
        [FromForm] byte tip, [FromForm] decimal tutar,
        [FromForm] decimal? alinanTutar, [FromForm] string? onayKodu, CancellationToken ct)
    {
        var (durum, hata) = await _odemeService.OdemeEkleAsync(
            await VardiyaIdAsync(ct), tip, tutar, alinanTutar, onayKodu, ct);

        return hata is null ? Ok(durum) : BadRequest(new { durum, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Iptal([FromForm] int fisId, [FromForm] int odemeId, CancellationToken ct)
    {
        var (durum, hata) = await _odemeService.OdemeIptalAsync(fisId, odemeId, ct);
        return hata is null ? Ok(durum) : BadRequest(new { durum, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Vazgec([FromForm] int fisId, CancellationToken ct)
        => Ok(await _odemeService.OdemedenVazgecAsync(fisId, ct));

    private async Task<int> VardiyaIdAsync(CancellationToken ct)
    {
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        return vardiya?.Id ?? await _vardiyaRepository.AcAsync(GeciciKullaniciId, 0, ct);
    }
}
