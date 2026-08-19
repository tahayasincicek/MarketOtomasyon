using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Sepet islemleri. Ekran Gun 7'de yazilacak; simdilik JSON uclari.
///
/// GECICI: oturum acma henuz yok, kasiyer sabit (Id 1) kabul ediliyor ve
/// acik vardiya yoksa otomatik aciliyor. Vardiya ekrani Hafta 3'te gelecek.
/// </summary>
[Route("[controller]/[action]")]
public class KasaController : Controller
{
    private const int GeciciKullaniciId = 1;

    private readonly SepetService _sepetService;
    private readonly VardiyaRepository _vardiyaRepository;

    public KasaController(SepetService sepetService, VardiyaRepository vardiyaRepository)
    {
        _sepetService = sepetService;
        _vardiyaRepository = vardiyaRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Sepet(CancellationToken ct)
        => Ok(await _sepetService.GetirAsync(await VardiyaIdAsync(ct), ct));

    [HttpPost]
    public async Task<IActionResult> Ekle([FromForm] string? barkod, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        var (sepet, hata) = await _sepetService.BarkodEkleAsync(vardiyaId, GeciciKullaniciId, barkod, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> MiktarGuncelle([FromForm] int satirId, [FromForm] decimal miktar, CancellationToken ct)
    {
        var (sepet, hata) = await _sepetService.MiktarGuncelleAsync(await VardiyaIdAsync(ct), satirId, miktar, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> SatirSil([FromForm] int satirId, CancellationToken ct)
    {
        var (sepet, hata) = await _sepetService.SatirSilAsync(await VardiyaIdAsync(ct), satirId, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Iptal(CancellationToken ct)
        => Ok(await _sepetService.IptalEtAsync(await VardiyaIdAsync(ct), ct));

    private async Task<int> VardiyaIdAsync(CancellationToken ct)
    {
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        return vardiya?.Id ?? await _vardiyaRepository.AcAsync(GeciciKullaniciId, 0, ct);
    }
}
