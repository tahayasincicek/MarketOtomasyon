using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace MarketOtomasyon.Web;

/// <summary>
/// Yakalanmayan her istisnayi loglar.
///
/// Onceden bu istisnalar sessizce /Home/Error sayfasina gidiyordu:
/// kullanici "bir hata olustu" goruyor, geriye hicbir iz kalmiyordu.
///
/// Kullaniciya gosterilen istek numarasi ile log kaydindaki numara
/// AYNIDIR (Error.cshtml bunu ekranda gosteriyor). Kullanici numarayi
/// soyledigi anda logda tam o istek bulunabilir.
/// </summary>
public sealed class GenelHataYakalayici : IExceptionHandler
{
    private readonly ILogger<GenelHataYakalayici> _kayit;

    public GenelHataYakalayici(ILogger<GenelHataYakalayici> kayit) => _kayit = kayit;

    public ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception hata, CancellationToken ct)
    {
        // ErrorViewModel.RequestId ile ayni kaynak: kullaniciya ekranda
        // gosterilen numara ile logdaki numara ayni olsun.
        var istekNo = Activity.Current?.Id ?? ctx.TraceIdentifier;

        // UseExceptionHandler bu noktaya gelmeden once istegi /Home/Error'a
        // yonlendiriyor; ctx.Request.Path artik hata sayfasini gosterir.
        // Hatanin GERCEKTEN olustugu yol bu ozellikte tasinir.
        var yol = ctx.Features.Get<IExceptionHandlerPathFeature>()?.Path
                  ?? ctx.Request.Path.Value;

        _kayit.LogError(hata,
            "Islenmeyen hata {IstekNo} {Yontem} {Yol} {Kullanici}",
            istekNo,
            ctx.Request.Method,
            yol,
            ctx.User.Identity?.Name ?? "anonim");

        // false: yaniti uretmeyi UseExceptionHandler'a birak. Burasi
        // yalnizca kayit tutar, kullaniciya ne gosterilecegine karismaz.
        return ValueTask.FromResult(false);
    }
}
