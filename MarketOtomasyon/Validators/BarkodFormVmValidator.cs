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
            .Matches("^[0-9A-Za-z]+$").WithMessage("Barkod yalnizca harf ve rakam icerebilir.")
            .MustAsync(async (barkod, ct) => !await barkodRepository.BarkodVarMiAsync(barkod.Trim(), ct))
            .WithMessage(x => $"'{x.Barkod}' barkodu zaten kayitli.");

        RuleFor(x => x.Carpan)
            .GreaterThan(0).WithMessage("Carpan sifirdan buyuk olmalidir.");

        RuleFor(x => x.Tip)
            .Must(t => t is 1 or 2).WithMessage("Barkod tipi tekli veya koli olmalidir.");

        // Koli barkodunun anlami "bir okutmada N adet"; carpani 1 ise tekli barkoddan farki kalmaz.
        RuleFor(x => x.Carpan)
            .GreaterThan(1).When(x => x.Tip == 2)
            .WithMessage("Koli barkodunun carpani 1'den buyuk olmalidir.");
    }
}
