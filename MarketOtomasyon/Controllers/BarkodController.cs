using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Kasa ekraninin JavaScript'ten cagiracagi barkod sorgulama ucu.
/// Hafta 2'de kasa yazildiginda ayni uc kullanilacak.
/// </summary>
[Route("[controller]")]
public class BarkodController : Controller
{
    private readonly BarkodService _barkodService;

    public BarkodController(BarkodService barkodService) => _barkodService = barkodService;

    [HttpGet("Coz")]
    public async Task<IActionResult> Coz(string? barkod, CancellationToken ct)
    {
        var sonuc = await _barkodService.CozAsync(barkod, ct);
        return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
    }
}
