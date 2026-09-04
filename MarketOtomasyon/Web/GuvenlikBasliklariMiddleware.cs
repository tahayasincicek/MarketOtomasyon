namespace MarketOtomasyon.Web;

/// <summary>
/// Uygulamanin dinamik yanitlarina ortak tarayici guvenlik basliklarini ekler.
/// Statik dosyalar bu middleware'den once sunuldugu icin onlarin uzun sureli
/// onbellegi etkilenmez.
/// </summary>
public sealed class GuvenlikBasliklariMiddleware
{
    private const string IcerikGuvenlikIlkesi =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "media-src 'self' blob:; " +
        "worker-src 'self' blob:";

    private readonly RequestDelegate _next;

    public GuvenlikBasliklariMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = IcerikGuvenlikIlkesi;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] =
            "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // Kullaniciya ve kasaya ait dinamik veriler ortak ya da tarayici
        // onbelleginde tutulmasin.
        headers.CacheControl = "no-store, max-age=0";
        headers.Pragma = "no-cache";

        await _next(context);
    }
}
