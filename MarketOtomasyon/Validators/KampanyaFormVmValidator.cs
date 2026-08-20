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
            .WithMessage(x => $"'{x.Kod}' kodu baska bir kampanyada kullaniliyor.");

        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Kampanya adi zorunludur.")
            .MaximumLength(200).WithMessage("Kampanya adi en fazla 200 karakter olabilir.");

        RuleFor(x => x.BitisTarihi)
            .GreaterThan(x => x.BaslangicTarihi).When(x => x.BitisTarihi.HasValue)
            .WithMessage("Bitis tarihi baslangictan sonra olmalidir.");

        // Kapsam: tutar barajı disindaki tiplerde urun ya da kategori secilmeli.
        RuleFor(x => x.UrunId)
            .NotNull().WithMessage("Urun seciniz.")
            .When(x => x.KampanyaTipi != KampanyaFormVm.TipTutarBaraji
                       && x.Kapsam == KampanyaFormVm.KapsamUrun);

        RuleFor(x => x.KategoriId)
            .NotNull().WithMessage("Kategori seciniz.")
            .When(x => x.KampanyaTipi != KampanyaFormVm.TipTutarBaraji
                       && x.Kapsam == KampanyaFormVm.KapsamKategori);

        // Tipe gore zorunlu alanlar.
        RuleFor(x => x.Yuzde)
            .InclusiveBetween(0.01m, 100m).WithMessage("Yuzde 0 ile 100 arasinda olmalidir.")
            .When(x => x.KampanyaTipi is KampanyaFormVm.TipYuzdeIndirim or KampanyaFormVm.TipTutarBaraji
                       && x.Tutar is null);

        RuleFor(x => x.Tutar)
            .GreaterThan(0).WithMessage("Indirim tutari sifirdan buyuk olmalidir.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipTutarIndirimi);

        RuleFor(x => x.MinSepetTutari)
            .GreaterThan(0).WithMessage("Baraj tutari sifirdan buyuk olmalidir.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipTutarBaraji);

        RuleFor(x => x.AlinacakMiktar)
            .GreaterThan(1).WithMessage("Alinacak miktar (N) 1'den buyuk olmalidir.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde);

        RuleFor(x => x.OdenecekMiktar)
            .GreaterThan(0).WithMessage("Odenecek miktar (M) sifirdan buyuk olmalidir.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde);

        // M >= N ise kampanya hicbir sey vermez; anlamsiz tanim engellenir.
        RuleFor(x => x.OdenecekMiktar)
            .LessThan(x => x.AlinacakMiktar)
            .WithMessage("Odenecek miktar (M), alinacak miktardan (N) kucuk olmalidir.")
            .When(x => x.KampanyaTipi == KampanyaFormVm.TipNAlMOde
                       && x.AlinacakMiktar.HasValue && x.OdenecekMiktar.HasValue);
    }
}
