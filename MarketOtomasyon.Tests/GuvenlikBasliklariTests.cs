using MarketOtomasyon.Web;
using Microsoft.AspNetCore.Http;

namespace MarketOtomasyon.Tests;

public sealed class GuvenlikBasliklariTests
{
    [Fact]
    public async Task DinamikYanita_GuvenlikBasliklariniEkler()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GuvenlikBasliklariMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Contains("default-src 'self'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Contains("frame-ancestors 'none'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("no-store, max-age=0", context.Response.Headers.CacheControl.ToString());
        Assert.Contains("camera=(self)", context.Response.Headers["Permissions-Policy"].ToString());
    }
}
