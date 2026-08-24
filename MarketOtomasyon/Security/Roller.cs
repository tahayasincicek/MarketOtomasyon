using System.Security.Claims;

namespace MarketOtomasyon.Security;

public static class Roller
{
    public const string Kasiyer = "Kasiyer";
    public const string Mudur = "Mudur";
    public const string SatisRolleri = Kasiyer + "," + Mudur;

    public const byte KasiyerKodu = 1;
    public const byte MudurKodu = 2;

    public static string Ad(byte rol)
        => rol == MudurKodu ? Mudur : Kasiyer;
}

public static class KullaniciClaimUzantilari
{
    public static int KullaniciId(this ClaimsPrincipal kullanici)
    {
        var deger = kullanici.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(deger, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Oturumdaki kullanıcı kimliği geçersiz.");

        return id;
    }

    public static byte RolKodu(this ClaimsPrincipal kullanici)
        => kullanici.IsInRole(Roller.Mudur) ? Roller.MudurKodu : Roller.KasiyerKodu;
}
