using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;

namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.Mudur)]
public sealed class MaliyetController : Controller
{
    private readonly MaliyetRepository _maliyetRepository;

    public MaliyetController(MaliyetRepository maliyetRepository)
        => _maliyetRepository = maliyetRepository;

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? baslangic,
        DateTime? bitis,
        CancellationToken ct)
    {
        var baslangicTarihi = (baslangic ?? DateTime.Today).Date;
        var bitisTarihi = (bitis ?? DateTime.Today).Date;

        if (bitisTarihi < baslangicTarihi)
        {
            ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            bitisTarihi = baslangicTarihi;
        }

        var baslangicUtc = YerelTarihiUtcYap(baslangicTarihi);
        var bitisUtc = YerelTarihiUtcYap(bitisTarihi.AddDays(1));

        return View(new KarMarjiRaporVm
        {
            Baslangic = baslangicTarihi,
            Bitis = bitisTarihi,
            Satirlar = await _maliyetRepository.KarMarjiRaporuAsync(baslangicUtc, bitisUtc, ct)
        });
    }

    private static DateTime YerelTarihiUtcYap(DateTime tarih)
        => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(tarih, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
}
