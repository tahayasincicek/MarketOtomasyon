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
            .NotEmpty().WithMessage("Ürün kodu zorunludur.")
            .MaximumLength(30).WithMessage("Ürün kodu en fazla 30 karakter olabilir.")
            .MustAsync(async (form, kod, ct) =>
                !await urunRepository.KodVarMiAsync(kod.Trim(), form.Id == 0 ? null : form.Id, ct))
            .WithMessage(x => $"'{x.Kod}' kodu başka bir üründe kullanılıyor.");

        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Ürün adı zorunludur.")
            .MaximumLength(200).WithMessage("Ürün adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.KategoriId)
            .GreaterThan(0).WithMessage("Kategori seçiniz.");

        RuleFor(x => x.Birim)
            .Must(b => GecerliBirimler.Contains(b)).WithMessage("Birim ADET veya KG olmalıdır.");

        RuleFor(x => x.KdvOrani)
            .Must(o => GecerliKdvOranlari.Contains(o))
            .WithMessage("KDV oranı 0, 1, 10 veya 20 olmalıdır.");

        RuleFor(x => x.Fiyat)
            .GreaterThan(0).WithMessage("Fiyat sıfırdan büyük olmalıdır.");

        RuleFor(x => x.MinStokSeviyesi)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stok seviyesi negatif olamaz.");

        // KG ile satilan urun tartili olmali; kasada miktar girisi buna gore degisir.
        RuleFor(x => x.Tartili)
            .Equal(true).When(x => x.Birim == "KG")
            .WithMessage("KG birimli ürün tartılı olarak işaretlenmelidir.");
    }
}
