using FluentValidation;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MarketOtomasyon.Controllers;

public class KampanyaController : Controller
{
    private readonly IDbConnectionFactory _factory;
    private readonly KampanyaRepository _kampanyaRepository;
    private readonly UrunRepository _urunRepository;
    private readonly KategoriRepository _kategoriRepository;
    private readonly IValidator<KampanyaFormVm> _validator;

    public KampanyaController(
        IDbConnectionFactory factory,
        KampanyaRepository kampanyaRepository,
        UrunRepository urunRepository,
        KategoriRepository kategoriRepository,
        IValidator<KampanyaFormVm> validator)
    {
        _factory = factory;
        _kampanyaRepository = kampanyaRepository;
        _urunRepository = urunRepository;
        _kategoriRepository = kategoriRepository;
        _validator = validator;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tanimlar = await _kampanyaRepository.HepsiniGetirAsync(ct);
        var urunler = await _urunRepository.AktifleriGetirAsync(ct);
        var kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct);
        var an = DateTime.UtcNow;

        var satirlar = tanimlar.Select(t =>
        {
            var form = KampanyaFormVm.TanimdanOlustur(t);
            var kosul = t.Kosullar.FirstOrDefault();

            var kapsam = kosul?.Tip switch
            {
                KosulTipi.Urun => urunler.FirstOrDefault(u => u.Id == kosul.UrunId)?.Ad ?? "(ürün)",
                KosulTipi.Kategori => kategoriler.FirstOrDefault(k => k.Id == kosul.KategoriId)?.Ad ?? "(kategori)",
                KosulTipi.SepetTutari => $"{kosul.MinTutar:N2} TL üstü sepet",
                _ => ""
            };

            return new KampanyaListeSatirVm
            {
                Id = t.Id,
                Kod = t.Kod,
                Ad = t.Ad,
                TipAdi = form.TipAdi,
                KapsamAdi = kapsam,
                Oncelik = t.Oncelik,
                BaslangicTarihi = t.BaslangicTarihi,
                BitisTarihi = t.BitisTarihi,
                Aktif = t.Aktif,
                SuAnGecerli = t.TarihGecerli(an)
            };
        }).ToList();

        return View(satirlar);
    }

    [HttpGet]
    public async Task<IActionResult> Ekle(CancellationToken ct)
        => View("Form", await ListeleriDoldurAsync(new KampanyaFormVm(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(KampanyaFormVm form, CancellationToken ct)
        => await KaydetAsync(form, ct);

    [HttpGet]
    public async Task<IActionResult> Duzenle(int id, CancellationToken ct)
    {
        var tanim = await _kampanyaRepository.GetirAsync(id, ct);
        if (tanim is null) return NotFound();

        return View("Form", await ListeleriDoldurAsync(KampanyaFormVm.TanimdanOlustur(tanim), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(KampanyaFormVm form, CancellationToken ct)
        => await KaydetAsync(form, ct);

    private async Task<IActionResult> KaydetAsync(KampanyaFormVm form, CancellationToken ct)
    {
        var sonuc = await _validator.ValidateAsync(form, ct);
        if (!sonuc.IsValid)
        {
            foreach (var hata in sonuc.Errors)
                ModelState.AddModelError(hata.PropertyName, hata.ErrorMessage);

            return View("Form", await ListeleriDoldurAsync(form, ct));
        }

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await _kampanyaRepository.KaydetAsync(conn, tx, form.TanimaCevir(), ct);
        tx.Commit();

        TempData["Mesaj"] = $"{form.Ad} kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<KampanyaFormVm> ListeleriDoldurAsync(KampanyaFormVm form, CancellationToken ct)
    {
        form.Urunler = await _urunRepository.AktifleriGetirAsync(ct);
        form.Kategoriler = await _kategoriRepository.AktifleriGetirAsync(ct);
        return form;
    }
}
