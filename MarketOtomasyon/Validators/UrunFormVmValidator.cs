using FluentValidation;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Validators;

public class UrunFormVmValidator : AbstractValidator<UrunFormVm>
{
    private static readonly decimal[] GecerliKdvOranlari = [0m, 1m, 10m, 20m];
    private static readonly string[] GecerliBirimler = ["ADET", "KG"];

    public UrunFormVmValidator(UrunRepository urunRepository)
    {
        RuleFor(x => x.Kod)
            .NotEmpty().WithMessage("Urun kodu zorunludur.")
            .MaximumLength(30).WithMessage("Urun kodu en fazla 30 karakter olabilir.")
            .MustAsync(async (form, kod, ct) =>
                !await urunRepository.KodVarMiAsync(kod.Trim(), form.Id == 0 ? null : form.Id, ct))
            .WithMessage(x => $"'{x.Kod}' kodu baska bir urunde kullaniliyor.");

        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Urun adi zorunludur.")
            .MaximumLength(200).WithMessage("Urun adi en fazla 200 karakter olabilir.");

        RuleFor(x => x.KategoriId)
            .GreaterThan(0).WithMessage("Kategori seciniz.");

        RuleFor(x => x.Birim)
            .Must(b => GecerliBirimler.Contains(b)).WithMessage("Birim ADET veya KG olmalidir.");

        RuleFor(x => x.KdvOrani)
            .Must(o => GecerliKdvOranlari.Contains(o))
            .WithMessage("KDV orani 0, 1, 10 veya 20 olmalidir.");

        RuleFor(x => x.Fiyat)
            .GreaterThan(0).WithMessage("Fiyat sifirdan buyuk olmalidir.");

        RuleFor(x => x.MinStokSeviyesi)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stok seviyesi negatif olamaz.");

        // KG ile satilan urun tartili olmali; kasada miktar girisi buna gore degisir.
        RuleFor(x => x.Tartili)
            .Equal(true).When(x => x.Birim == "KG")
            .WithMessage("KG birimli urun tartili olarak isaretlenmelidir.");
    }
}
