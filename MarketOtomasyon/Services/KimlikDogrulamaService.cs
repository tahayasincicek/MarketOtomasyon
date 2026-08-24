using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarketOtomasyon.Services;

public sealed class KimlikDogrulamaService
{
    private readonly KullaniciRepository _kullaniciRepository;
    private readonly IPasswordHasher<Kullanici> _passwordHasher;

    public KimlikDogrulamaService(
        KullaniciRepository kullaniciRepository,
        IPasswordHasher<Kullanici> passwordHasher)
    {
        _kullaniciRepository = kullaniciRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Kullanici?> GirisDogrulaAsync(
        string? kullaniciAdi,
        string? sifre,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            return null;

        var kullanici = await _kullaniciRepository.KullaniciAdiylaGetirAsync(kullaniciAdi.Trim(), ct);
        if (kullanici is null) return null;

        var sonuc = _passwordHasher.VerifyHashedPassword(kullanici, kullanici.SifreHash, sifre);
        if (sonuc == PasswordVerificationResult.Failed) return null;

        if (sonuc == PasswordVerificationResult.SuccessRehashNeeded)
        {
            kullanici.SifreHash = _passwordHasher.HashPassword(kullanici, sifre);
            await _kullaniciRepository.SifreHashGuncelleAsync(kullanici.Id, kullanici.SifreHash, ct);
        }

        return kullanici;
    }
}
