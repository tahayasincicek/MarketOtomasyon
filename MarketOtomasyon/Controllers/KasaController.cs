using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Services;
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

        var (sepet, hata) = await _sepetService.BarkodEkleAsync(vardiyaId, GeciciKullaniciId, barkod, ct);
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

    /// <summary>Yuzde 0 gonderilirse indirim kaldirilir. onaylayanKullaniciId verilirse
    /// yetki onun rolune gore denetlenir (mudur onayi).</summary>
    [HttpPost]
    public async Task<IActionResult> SatirIndirimi(
        [FromForm] int satirId, [FromForm] decimal yuzde, [FromForm] int? onaylayanKullaniciId, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var rol = await RolAsync(onaylayanKullaniciId, ct);
        var (sepet, hata) = await _sepetService.SatirIndirimiUygulaAsync(vardiyaId, satirId, yuzde, rol, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> FisIndirimi(
        [FromForm] decimal yuzde, [FromForm] int? onaylayanKullaniciId, CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        var rol = await RolAsync(onaylayanKullaniciId, ct);
        var (sepet, hata) = await _sepetService.FisIndirimiUygulaAsync(vardiyaId, yuzde, rol, ct);
        return hata is null ? Ok(sepet) : BadRequest(new { sepet, hata });
    }

    [HttpPost]
    public async Task<IActionResult> Iptal(CancellationToken ct)
    {
        var vardiyaId = await VardiyaIdAsync(ct);
        if (vardiyaId < 0) return VardiyaYok();

        return Ok(await _sepetService.IptalEtAsync(vardiyaId, ct));
    }

    /// <summary>Onaylayan verilmemisse islemi yapan kasiyerin rolu kullanilir.</summary>
    private async Task<byte> RolAsync(int? onaylayanKullaniciId, CancellationToken ct)
        => await _kullaniciRepository.RolGetirAsync(onaylayanKullaniciId ?? GeciciKullaniciId, ct)
           ?? IndirimYetkisi.RolKasiyer;

    /// <summary>Acik vardiya yoksa -1 doner; cagiran uc VardiyaYok() ile 409 dondurur.</summary>
    private async Task<int> VardiyaIdAsync(CancellationToken ct)
        => (await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId, ct))?.Id ?? -1;

    // Kasa ekranindaki JS, basarisiz yanitlarda govdedeki "hata" alanini gosterir.
    private ConflictObjectResult VardiyaYok()
        => Conflict(new { hata = "Açık vardiya yok. Vardiya ekranından vardiya açın." });
}
