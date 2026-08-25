using MarketOtomasyon.Models.ViewModels;
using MarketOtomasyon.Security;
using MarketOtomasyon.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

/// <summary>
/// Personel yonetimi. Tumu mudure ozel: kasiyer bu ekrani goremez,
/// uclarina dogrudan istek de gonderemez.
/// </summary>
[Authorize(Roles = Roller.Mudur)]
public class PersonelController : Controller
{
    private readonly PersonelService _personelService;

    public PersonelController(PersonelService personelService)
        => _personelService = personelService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _personelService.EkranAsync(User.KullaniciId(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Olustur(
        [Bind(Prefix = nameof(PersonelEkranVm.YeniPersonel))] PersonelFormVm form,
        CancellationToken ct)
    {
        var hata = await _personelService.OlusturAsync(form, User.KullaniciId(), ct);
        if (hata is not null)
        {
            // Sifre geri doldurulmaz: ekranda tekrar gosterilmemeli.
            form.Sifre = "";
            return await HatayiGosterAsync(hata, form, ct);
        }

        TempData["Mesaj"] = $"{form.KullaniciAdi} kullanıcısı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AktiflikDegistir(int id, bool aktif, CancellationToken ct)
    {
        var hata = await _personelService.AktiflikDegistirAsync(id, aktif, User.KullaniciId(), ct);
        if (hata is not null) return await HatayiGosterAsync(hata, null, ct);

        TempData["Mesaj"] = aktif ? "Kullanıcı aktife alındı." : "Kullanıcı pasife alındı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RolDegistir(int id, byte rol, CancellationToken ct)
    {
        var hata = await _personelService.RolDegistirAsync(id, rol, User.KullaniciId(), ct);
        if (hata is not null) return await HatayiGosterAsync(hata, null, ct);

        TempData["Mesaj"] = "Kullanıcının rolü güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SifreSifirla(int id, string? yeniSifre, CancellationToken ct)
    {
        var hata = await _personelService.SifreSifirlaAsync(id, yeniSifre, User.KullaniciId(), ct);
        if (hata is not null) return await HatayiGosterAsync(hata, null, ct);

        TempData["Mesaj"] = "Şifre sıfırlandı. Yeni şifreyi personele iletin.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hata durumunda liste yeniden okunur: kullanici baska bir oturumda
    /// degismis olabilir ve mudur karari guncel veriyle vermeli.
    /// </summary>
    private async Task<IActionResult> HatayiGosterAsync(
        string hata, PersonelFormVm? form, CancellationToken ct)
    {
        var vm = await _personelService.EkranAsync(User.KullaniciId(), ct);
        vm.Hata = hata;
        if (form is not null) vm.YeniPersonel = form;
        return View(nameof(Index), vm);
    }
}
