using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using MarketOtomasyon.Security;

namespace MarketOtomasyon.ViewComponents;

/// <summary>
/// Ust seritteki vardiya gostergesi. Market otomasyonlarinda kasiyer
/// hangi vardiyada oldugunu her ekranda gorur; menuye gitmesi gerekmez.
/// </summary>
public class VardiyaDurumuViewComponent : ViewComponent
{
    private readonly VardiyaRepository _vardiyaRepository;
    private readonly KullaniciRepository _kullaniciRepository;

    public VardiyaDurumuViewComponent(
        VardiyaRepository vardiyaRepository, KullaniciRepository kullaniciRepository)
    {
        _vardiyaRepository = vardiyaRepository;
        _kullaniciRepository = kullaniciRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var kullaniciId = UserClaimsPrincipal.KullaniciId();
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId);

        return View(new UstSeritVm
        {
            KasiyerAdi = await _kullaniciRepository.AdSoyadGetirAsync(kullaniciId) ?? "Kullanıcı",
            VardiyaId = vardiya?.Id,
            AcilisTarihi = vardiya?.AcilisTarihi
        });
    }
}
