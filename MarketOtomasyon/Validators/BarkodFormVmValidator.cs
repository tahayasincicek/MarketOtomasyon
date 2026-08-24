using FluentValidation;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Validators;

public class BarkodFormVmValidator : AbstractValidator<BarkodFormVm>
{
    public BarkodFormVmValidator(BarkodRepository barkodRepository)
    {
        RuleFor(x => x.Barkod)
            .NotEmpty().WithMessage("Barkod zorunludur.")
            .MaximumLength(30).WithMessage("Barkod en fazla 30 karakter olabilir.")
            .Matches("^[0-9A-Za-z]+$").WithMessage("Barkod yalnızca harf ve rakam içerebilir.")
            .MustAsync(async (barkod, ct) => !await barkodRepository.BarkodVarMiAsync(barkod.Trim(), ct))
            .WithMessage(x => $"'{x.Barkod}' barkodu zaten kayıtlı.");

        RuleFor(x => x.Carpan)
            .GreaterThan(0).WithMessage("Çarpan sıfırdan büyük olmalıdır.");

        RuleFor(x => x.Tip)
            .Must(t => t is 1 or 2).WithMessage("Barkod tipi tekli veya koli olmalıdır.");

        // Koli barkodunun anlami "bir okutmada N adet"; carpani 1 ise tekli barkoddan farki kalmaz.
        RuleFor(x => x.Carpan)
            .GreaterThan(1).When(x => x.Tip == 2)
            .WithMessage("Koli barkodunun çarpanı 1'den büyük olmalıdır.");
    }
}
