using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Services;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Sepet islemleri. Ekran Gun 7'de yazilacak; simdilik JSON uclari.
///
/// Her fis, acildigi andaki acik vardiyaya baglanir. Acik vardiya yoksa
/// kasa calismaz: kasiyer once Vardiya ekranindan vardiya acmali.
///
/// GECICI: oturum acma henuz yok, kasiyer sabit (Id 1) kabul ediliyor.
/// </summary>
[Route("[controller]/[action]")]
[Authorize(Roles = Roller.SatisRolleri)]
public class KasaController : Controller
{
    private readonly SepetService _sepetService;
    private readonly VardiyaRepository _vardiyaRepository;
    private readonly HizliUrunRepository _hizliUrunRepository;

    public KasaController(
        SepetService sepetService,
        VardiyaRepository vardiyaRepository,
        HizliUrunRepository hizliUrunRepository)
    {
        _sepetService = sepetService;
        _vardiyaRepository = vardiyaRepository;
        _hizliUrunRepository = hizliUrunRepository;
    }

    [HttpGet("/Kasa")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var hizliUrunlerTask = _hizliUrunRepository.ListeleAsync(ct: ct);
        var vardiyaTask = _vardiyaRepository.AcikVardiyaGetirAsync(User.KullaniciId(), ct);

        await Task.WhenAll(hizliUrunlerTask, vardiyaTask);

        return View(new KasaEkranVm
        {
            HizliUrunler = await hizliUrunlerTask,
            AcikVardiyaId = (await vardiyaTask)?.Id
        });
    }

    [HttpGet]
    public async Task<IActionResult> Sepet(CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        return Ok(await _sepetService.GetirAsync(vardiyaId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Ekle([FromForm] string? barkod, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var (sepet, hata) = await _sepetService.BarkodEkleAsync(vardiyaId, User.KullaniciId(), barkod, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> MiktarGuncelle([FromForm] int satirId, [FromForm] decimal miktar, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var (sepet, hata) = await _sepetService.MiktarGuncelleAsync(vardiyaId, satirId, miktar, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> SatirSil([FromForm] int satirId, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var (sepet, hata) = await _sepetService.SatirSilAsync(vardiyaId, satirId, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    /// <summary>Yetki, formdan gelen bir kimlige degil oturumdaki role gore denetlenir.</summary>
    [HttpPost]
    public async Task<IActionResult> SatirIndirimi(
        [FromForm] int satirId, [FromForm] decimal yuzde, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var (sepet, hata) = await _sepetService.SatirIndirimiUygulaAsync(
            vardiyaId, satirId, yuzde, User.RolKodu(), User.KullaniciId(), ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> FisIndirimi(
        [FromForm] decimal yuzde, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var (sepet, hata) = await _sepetService.FisIndirimiUygulaAsync(
            vardiyaId, yuzde, User.RolKodu(), User.KullaniciId(), ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Iptal(CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        return Ok(await _sepetService.IptalEtAsync(vardiyaId, User.KullaniciId(), ct));
    }

    /// <summary>Acik vardiya yoksa -1 doner; cagiran uc VardiyaYok() ile 409 dondurur.</summary>
    private async Task<int> VardiyaIdAsync(CancellationToken ct)
        => (await _vardiyaRepository.AcikVardiyaGetirAsync(User.KullaniciId(), ct))?.Id ?? -1;

    // Kasa ekranindaki JS, basarisiz yanitlarda govdedeki "hata" alanini gosterir.
    private ConflictObjectResult VardiyaYok()
        => Conflict(new { hata = "Açık vardiya yok. Vardiya ekranından vardiya açın." });
}
