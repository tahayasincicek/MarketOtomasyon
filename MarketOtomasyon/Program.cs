using System.Globalization;
using FluentValidation;
using MarketOtomasyon.Models.Entities;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Services;
using MarketOtomasyon.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Log kurallari appsettings.json'daki Serilog bolumunde; ayar kod icine
// dagilmasin. Enrich.FromLogContext, istek basina eklenen Kullanici ve
// IstekNo alanlarini okuyabilmek icin sart.
builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext());

// Dogrulama tek yerden yonetilsin: MVC'nin non-nullable alanlar icin urettigi
// otomatik "required" mesajlari kapali, kurallar FluentValidation'da.
builder.Services.AddControllersWithViews(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MarketOtomasyon.Oturum";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Hesap/Giris";
        options.AccessDeniedPath = "/Hesap/Yetkisiz";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddExceptionHandler<GenelHataYakalayici>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IPasswordHasher<Kullanici>, PasswordHasher<Kullanici>>();

// Dapper baglanti fabrikasi - tum repository'ler bunu kullanir.
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// Repository'ler
builder.Services.AddScoped<UrunRepository>();
builder.Services.AddScoped<KategoriRepository>();
builder.Services.AddScoped<FiyatRepository>();
builder.Services.AddScoped<BarkodRepository>();
builder.Services.AddScoped<IBarkodRepository>(sp => sp.GetRequiredService<BarkodRepository>());
builder.Services.AddScoped<StokRepository>();
builder.Services.AddScoped<DepoRepository>();
builder.Services.AddScoped<FisRepository>();
builder.Services.AddScoped<VardiyaRepository>();
builder.Services.AddScoped<KullaniciRepository>();
builder.Services.AddScoped<OdemeRepository>();
builder.Services.AddScoped<KampanyaRepository>();
builder.Services.AddScoped<IadeRepository>();
builder.Services.AddScoped<OzetRepository>();
builder.Services.AddScoped<UrunResimRepository>();
builder.Services.AddScoped<SayimRepository>();
builder.Services.AddScoped<HizliUrunRepository>();
builder.Services.AddScoped<MaliyetRepository>();
builder.Services.AddScoped<TransferRepository>();
builder.Services.AddScoped<TedarikciRepository>();
builder.Services.AddScoped<SonKullanmaRepository>();
builder.Services.AddScoped<AlisFaturasiRepository>();
builder.Services.AddScoped<IslemLogRepository>();

// Servisler
builder.Services.AddScoped<UrunService>();
builder.Services.AddScoped<BarkodService>();
builder.Services.AddScoped<StokService>();
builder.Services.AddScoped<SepetService>();
builder.Services.AddScoped<OdemeService>();
builder.Services.AddScoped<SatisService>();
builder.Services.AddScoped<KampanyaService>();
builder.Services.AddScoped<IadeService>();
builder.Services.AddScoped<VardiyaService>();
builder.Services.AddScoped<UrunResimService>();
builder.Services.AddScoped<RaporRepository>();
builder.Services.AddScoped<SayimService>();
builder.Services.AddScoped<PersonelService>();
builder.Services.AddScoped<MudurOnayService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<SonKullanmaService>();
builder.Services.AddScoped<TedarikciService>();
builder.Services.AddScoped<AlisFaturasiService>();
builder.Services.AddScoped<MaliyetService>();
builder.Services.AddScoped<KimlikDogrulamaService>();

// Isletmeye gore degisen satis kurallari
builder.Services.Configure<SatisAyarlari>(builder.Configuration.GetSection("Satis"));
builder.Services.Configure<IadeAyarlari>(builder.Configuration.GetSection("Iade"));
builder.Services.Configure<UrunResimAyarlari>(builder.Configuration.GetSection("UrunResim"));

// Open Food Facts kimliksiz istekleri bot sayip engelliyor; User-Agent zorunlu.
builder.Services.AddHttpClient("acikUrunVeritabani", (sp, istemci) =>
{
    var ayar = sp.GetRequiredService<IOptions<UrunResimAyarlari>>().Value;
    istemci.DefaultRequestHeaders.UserAgent.ParseAdd(ayar.KullaniciAjani);
    istemci.Timeout = TimeSpan.FromSeconds(ayar.ZamanAsimiSaniye);
});

// Dogrulayicilar (controller icinde elle cagriliyor)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

// Sunucunun ne zaman yeniden basladigi, hata ararken ilk sorulan sey.
app.Logger.LogInformation("Uygulama basladi. Ortam: {Ortam}", app.Environment.EnvironmentName);

// Kosulsuz: hata gelistirmede de loglanmali. GenelHataYakalayici
// yalnizca kayit tutar, kullaniciya gosterilecek yaniti bu ara katman
// uretir.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// HTML number alanlari ondalik ayirici olarak her zaman nokta gonderir.
// Sunucu tr-TR kulturunde calisirsa nokta binlik ayirici sanilir ve 18.75 -> 1875 olur.
// Bu yuzden model binding invariant kulturde yapilir; ekrandaki gosterim ayrica bicimlendirilir.
var invariant = new List<CultureInfo> { CultureInfo.InvariantCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = invariant,
    SupportedUICultures = invariant
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Kullanici ve istek kimligi, o istek boyunca yazilan TUM log satirlarina
// otomatik eklenir; her cagriya elle parametre gecmeye gerek kalmaz.
//
// Iki sira kurali var:
//   - UseAuthentication SONRASINA: oncesinde ctx.User dolmamis olur ve
//     her satirda "anonim" yazar.
//   - UseSerilogRequestLogging ONCESINE: istek ozeti satiri da bu
//     alanlari tasisin, yoksa ozet kayitlari kimin yaptigi bilinmez.
app.Use(async (ctx, next) =>
{
    using (Serilog.Context.LogContext.PushProperty("Kullanici", ctx.User.Identity?.Name ?? "anonim"))
    using (Serilog.Context.LogContext.PushProperty("IstekNo", ctx.TraceIdentifier))
    {
        await next();
    }
});

// Her istek icin tek satir ozet (yol, durum kodu, sure). ASP.NET Core'un
// ayrintili istek loglarinin yerine gecer, cok daha sessizdir.
app.UseSerilogRequestLogging();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
