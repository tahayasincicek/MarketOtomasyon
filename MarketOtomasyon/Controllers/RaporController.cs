using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;

namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.Mudur)]
public sealed class RaporController : Controller
{
    /// <summary>Varsayilan aralik: bugun dahil son 7 gun.</summary>
    private const int VarsayilanGunSayisi = 7;

    private const int EnCokSatanAdet = 10;

    private readonly RaporRepository _raporRepository;

    public RaporController(RaporRepository raporRepository)
        => _raporRepository = raporRepository;

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? baslangic, DateTime? bitis, CancellationToken ct)
    {
        var bitisTarihi = (bitis ?? DateTime.Today).Date;
        var baslangicTarihi = (baslangic ?? bitisTarihi.AddDays(-(VarsayilanGunSayisi - 1))).Date;

        if (bitisTarihi < baslangicTarihi)
        {
            ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            bitisTarihi = baslangicTarihi;
        }

        var rapor = await _raporRepository.RaporlariGetirAsync(
            YerelTarihiUtcYap(baslangicTarihi),
            YerelTarihiUtcYap(bitisTarihi.AddDays(1)),   // bitis gunu dahil olsun
            EnCokSatanAdet,
            ct);

        rapor.Baslangic = baslangicTarihi;
        rapor.Bitis = bitisTarihi;
        return View(rapor);
    }

    // MaliyetController ile ayni cevrim: iki ekran ayni gun icin ayni
    // rakami gostersin.
    private static DateTime YerelTarihiUtcYap(DateTime tarih)
        => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(tarih, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
}
