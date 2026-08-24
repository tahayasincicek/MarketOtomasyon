using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

[Authorize(Roles = Roller.Mudur)]
public sealed class IslemLogController : Controller
{
    private readonly IslemLogRepository _islemLogRepository;

    public IslemLogController(IslemLogRepository islemLogRepository)
        => _islemLogRepository = islemLogRepository;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _islemLogRepository.SonKayitlarAsync(ct: ct));
}
