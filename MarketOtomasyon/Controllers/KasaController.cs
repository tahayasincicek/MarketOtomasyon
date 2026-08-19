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
    private readonly KullaniciRepository _kullaniciRepository;

    public KasaController(SepetService sepetService, VardiyaRepository vardiyaRepository, KullaniciRepository kullaniciRepository)
    {
        _sepetService = sepetService;
        _vardiyaRepository = vardiyaRepository;
        _kullaniciRepository = kullaniciRepository;
    }

    [HttpGet("/Kasa")]
    [HttpGet]
    public IActionResult Index() => View();

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

    /// <summary>Yuzde 0 gonderilirse indirim kaldirilir. onaylayanKullaniciId verilirse
    /// yetki onun rolune gore denetlenir (mudur onayi).</summary>
    [HttpPost]
    public async Task<IActionResult> SatirIndirimi(
        [FromForm] int satirId, [FromForm] decimal yuzde, [FromForm] int? onaylayanKullaniciId, CancellationToken ct)
    {
        var rol = await RolAsync(onaylayanKullaniciId, ct);
        var (sepet, hata) = await _sepetService.SatirIndirimiUygulaAsync(await VardiyaIdAsync(ct), satirId, yuzde, rol, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> FisIndirimi(
        [FromForm] decimal yuzde, [FromForm] int? onaylayanKullaniciId, CancellationToken ct)
    {
        var rol = await RolAsync(onaylayanKullaniciId, ct);
        var (sepet, hata) = await _sepetService.FisIndirimiUygulaAsync(await VardiyaIdAsync(ct), yuzde, rol, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Iptal(CancellationToken ct)
        => Ok(await _sepetService.IptalEtAsync(await VardiyaIdAsync(ct), ct));

    /// <summary>Onaylayan verilmemisse islemi yapan kasiyerin rolu kullanilir.</summary>
    private async Task<byte> RolAsync(int? onaylayanKullaniciId, CancellationToken ct)
        => await _kullaniciRepository.RolGetirAsync(onaylayanKullaniciId ?? GeciciKullaniciId, ct)
           ?? IndirimYetkisi.RolKasiyer;

    private async Task<int> VardiyaIdAsync(CancellationToken ct)
    {
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct);
        return vardiya?.Id ?? await _vardiyaRepository.AcAsync(GeciciKullaniciId, 0, ct);
    }
}
