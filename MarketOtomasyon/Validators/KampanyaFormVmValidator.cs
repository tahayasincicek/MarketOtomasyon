using FluentValidation;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Validators;

public class KampanyaFormVmValidator : AbstractValidator<KampanyaFormVm>
{
    public KampanyaFormVmValidator(KampanyaRepository kampanyaRepository)
    {
        RuleFor(x => x.Kod)
            .NotEmpty().WithMessage("Kampanya kodu zorunludur.")
            .MaximumLength(30).WithMessage("Kampanya kodu en fazla 30 karakter olabilir.")
            .MustAsync(async (form, kod, ct) =>
                !await kampanyaRepository.KodVarMiAsync(kod.Trim(), form.Id == 0 ? null : form.Id, ct))
            .WithMessage(x => $"'{x.Kod}' kodu başka bir kampanyada kullanılıyor.");

        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Kampanya adı zorunludur.")
            .MaximumLength(200).WithMessage("Kampanya adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.BitisTarihi)
            .GreaterThan(x => x.BaslangicTarihi).When(x => x.BitisTarihi.HasValue)
            .WithMessage("Bitiş tarihi başlangıçtan sonra olmalıdır.");

        // Kapsam: tutar barajı disindaki tiplerde urun ya da kategori secilmeli.
        RuleFor(x => x.UrunId)
            .NotNull().WithMessage("Ürün seçiniz.")
            .When(x => x.KampanyaTipi != KampanyaFormVm.TipTutarBaraji
                       && x.Kapsam == KampanyaFormVm.KapsamUrun);

        RuleFor(x => x.KategoriId)
            .NotNull().WithMessage("Kategori seçiniz.")
            .When(x => x.KampanyaTipi != KampanyaFormVm.TipTutarBaraji
                       && x.Kapsam == KampanyaFormVm.KapsamKategori);

        // Tipe gore zorunlu alanlar.
        RuleFor(x => x.Yuzde)
            .InclusiveBetween(0.01m, 100m).WithMessage("Yüzde 0 ile 100 arasında olmalıdır.")
            .When(x => x.KampanyaTipi is KampanyaFormVm.TipYuzdeIndirim or KampanyaFormVm.TipTutarBaraji
                       && x.Tutar is null);

        RuleFor(x => x.Tutar)
            .GreaterThan(0).WithMessage("İndirim tutarı sıfırdan büyük olmalıdır.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipTutarIndirimi);

        RuleFor(x => x.MinSepetTutari)
            .GreaterThan(0).WithMessage("Baraj tutarı sıfırdan büyük olmalıdır.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipTutarBaraji);

        RuleFor(x => x.AlinacakMiktar)
            .GreaterThan(1).WithMessage("Alınacak miktar (N) 1'den büyük olmalıdır.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde);

        RuleFor(x => x.OdenecekMiktar)
            .GreaterThan(0).WithMessage("Ödenecek miktar (M) sıfırdan büyük olmalıdır.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde);

        // M >= N ise kampanya hicbir sey vermez; anlamsiz tanim engellenir.
        RuleFor(x => x.OdenecekMiktar)
            .LessThan(x => x.AlinacakMiktar)
            .WithMessage("Ödenecek miktar (M), alınacak miktardan (N) küçük olmalıdır.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde
                       && x.AlinacakMiktar.HasValue && x.OdenecekMiktar.HasValue);
    }
}
