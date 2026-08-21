using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.ViewComponents;

/// <summary>
/// Ust seritteki vardiya gostergesi. Market otomasyonlarinda kasiyer
/// hangi vardiyada oldugunu her ekranda gorur; menuye gitmesi gerekmez.
///
/// GECICI: oturum acma henuz yok, kasiyer sabit (Id 1) kabul ediliyor.
/// </summary>
public class VardiyaDurumuViewComponent : ViewComponent
{
    private const int GeciciKullaniciId = 1;

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
        var vardiya = await _vardiyaRepository.AcikVardiyaGetirAsync(GeciciKullaniciId);

        return View(new UstSeritVm
        {
            KasiyerAdi = await _kullaniciRepository.AdSoyadGetirAsync(GeciciKullaniciId) ?? "Kasiyer",
            VardiyaId = vardiya?.Id,
            AcilisTarihi = vardiya?.AcilisTarihi
        });
    }
}
