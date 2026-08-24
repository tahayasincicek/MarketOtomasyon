using System.Reflection;
using System.Security.Claims;
using MarketOtomasyon.Controllers;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MarketOtomasyon.Tests;

public sealed class YetkilendirmeTests
{
    [Theory]
    [InlineData(typeof(RaporController))]
    [InlineData(typeof(MaliyetController))]
    [InlineData(typeof(IslemLogController))]
    public void MudurEkranlari_YalnizcaMudurRolunuKabulEder(Type controllerTipi)
    {
        var yetki = controllerTipi.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(yetki);
        Assert.Equal(Roller.Mudur, yetki!.Roles);
    }

    [Theory]
    [InlineData(typeof(KasaController))]
    [InlineData(typeof(IadeController))]
    [InlineData(typeof(UrunController))]
    [InlineData(typeof(StokController))]
    public void TemelSatisEkranlari_HemKasiyerHemMudurRolunuKabulEder(Type controllerTipi)
    {
        var yetki = controllerTipi.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(yetki);
        Assert.Equal(Roller.SatisRolleri, yetki!.Roles);
    }

    [Theory]
    [InlineData(typeof(UrunController), "Ekle")]
    [InlineData(typeof(UrunController), "Duzenle")]
    [InlineData(typeof(UrunController), "ResimleriCek")]
    [InlineData(typeof(UrunController), "BarkodEkle")]
    [InlineData(typeof(UrunController), "BarkodSil")]
    [InlineData(typeof(StokController), "Giris")]
    public void VeriDegistirenEylemler_YalnizcaMudurRolunuKabulEder(Type controllerTipi, string eylem)
    {
        var metotlar = controllerTipi.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == eylem)
            .ToList();

        Assert.NotEmpty(metotlar);
        Assert.All(metotlar, metot =>
        {
            var yetki = metot.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(yetki);
            Assert.Equal(Roller.Mudur, yetki!.Roles);
        });
    }

    [Fact]
    public void Claimlerden_KullaniciKimligiVeRolKoduOkunur()
    {
        var kullanici = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Role, Roller.Mudur)
        }, "Test"));

        Assert.Equal(42, kullanici.KullaniciId());
        Assert.Equal(Roller.MudurKodu, kullanici.RolKodu());
    }

    [Fact]
    public void PasswordHasher_DogruSifreyiKabulEder_YanlisiReddeder()
    {
        var kullanici = new Kullanici { Id = 1, KullaniciAdi = "kasiyer1" };
        var hasher = new PasswordHasher<Kullanici>();
        var hash = hasher.HashPassword(kullanici, "Guvenli123!");

        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(kullanici, hash, "Guvenli123!"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(kullanici, hash, "yanlis"));
        Assert.NotEqual("Guvenli123!", hash);
    }
}
