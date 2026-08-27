using System.Globalization;
using FluentValidation;
using MarketOtomasyon.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Services;
using MarketOtomasyon.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Docker HEALTHCHECK, web sunucusundan bagimsiz kisa omurlu bir surec
// calistirir. Veritabani ayarlarini dogrulamadan yerel canlilik ucunu yoklar.
if (args.Length > 0 && args[0].Equals("healthcheck", StringComparison.OrdinalIgnoreCase))
    return await ContainerSaglikKomutu.CalistirAsync();

/* Baglanti dizesi acilista dogrulanir.
   appsettings.json'da bilerek yok: uretim sifresi kaynak koda girmesin.
   Gelistirmede appsettings.Development.json'dan, uretimde
   ConnectionStrings__MarketDb ortam degiskeninden gelir.

   Kontrol burada yapiliyor cunku aksi halde uygulama sorunsuz acilir,
   hata ancak ilk veritabani istegi geldiginde ortaya cikardi: kasiyer
   giris ekranini gorur, sifresini girer ve orada patlardi. */
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("MarketDb")))
{
    throw new InvalidOperationException(
        "Veritabanı bağlantı dizesi tanımlı değil. " +
        "Üretimde ConnectionStrings__MarketDb ortam değişkenini ayarlayın, " +
        "geliştirmede appsettings.Development.json dosyasına ekleyin.");
}

/* Veritabani kurulumu ayri bir komut olarak da calistirilabilir:

     dotnet run -- migrate              semayi guncelle
     dotnet run -- migrate --demo       demo verisini de ekle
     dotnet run -- migrate --liste      neyin bekledigini goster, uygulama

   Uretimde tercih edilen yol budur: deploy sirasinda kontrollu
   calistirilir, uygulama ayaga kalkmadan once sema hazir olur.
   Web sunucusu baslatilmaz; komut biter ve surec kapanir. */
if (args.Length > 0 && args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase))
{
    return MigrateKomutu.Calistir(builder.Configuration, args);
}

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

/* Kimlik doğrulama çerezini şifreleyen Data Protection anahtarları container
   yeniden oluşturulunca kaybolmamalı. Aynı uygulama adını ve anahtar klasörünü
   kullanan instance'lar birbirlerinin oluşturduğu oturum çerezini açabilir. */
var veriKorumaAyarlari = builder.Configuration
    .GetSection(VeriKorumaAyarlari.Bolum)
    .Get<VeriKorumaAyarlari>() ?? new VeriKorumaAyarlari();
var containerdaCalisiyor = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var veriKorumaHatalari = veriKorumaAyarlari.DogrulamaHatalari(containerdaCalisiyor);

if (veriKorumaHatalari.Count > 0)
    throw new InvalidOperationException(string.Join(" ", veriKorumaHatalari));

var veriKoruma = builder.Services
    .AddDataProtection()
    .SetApplicationName(veriKorumaAyarlari.UygulamaAdi);

if (!string.IsNullOrWhiteSpace(veriKorumaAyarlari.AnahtarKlasoru))
{
    var anahtarKlasoru = Path.GetFullPath(veriKorumaAyarlari.AnahtarKlasoru);
    Directory.CreateDirectory(anahtarKlasoru);
    veriKoruma.PersistKeysToFileSystem(new DirectoryInfo(anahtarKlasoru));
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MarketOtomasyon.Oturum";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        /* Uretimde cerez YALNIZCA HTTPS uzerinden gonderilir.
           SameAsRequest birakilsaydi, HTTP'ye dusen tek bir istekte
           oturum cerezi sifresiz gider ve ag trafigini dinleyen biri
           onu kopyalayip mudur oturumunu devralabilirdi.
           Gelistirmede Always kullanilamaz: http://localhost ile
           calisirken cerez hic yazilmaz ve giris yapilamaz. */
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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

var girisGuvenligi = builder.Configuration
    .GetSection(GirisGuvenligiAyarlari.Bolum)
    .Get<GirisGuvenligiAyarlari>() ?? new GirisGuvenligiAyarlari();
var girisGuvenligiHatalari = girisGuvenligi.DogrulamaHatalari();

if (girisGuvenligiHatalari.Count > 0)
    throw new InvalidOperationException(string.Join(" ", girisGuvenligiHatalari));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var bekleme = TimeSpan.FromSeconds(girisGuvenligi.PencereSaniye);
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var onerilenBekleme))
            bekleme = onerilenBekleme;

        context.HttpContext.Response.Headers.RetryAfter =
            Math.Max(1, (int)Math.Ceiling(bekleme.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GirisGuvenligi");
        logger.LogWarning(
            "Giris denemesi siniri asildi. IP: {Ip}",
            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor");

        await context.HttpContext.Response.WriteAsync(
            "Çok fazla giriş denemesi yapıldı. Lütfen kısa bir süre sonra tekrar deneyin.",
            ct);
    };

    options.AddPolicy("giris", httpContext =>
    {
        var istemci = httpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: istemci,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = girisGuvenligi.IzinSayisi,
                Window = TimeSpan.FromSeconds(girisGuvenligi.PencereSaniye),
                SegmentsPerWindow = girisGuvenligi.DilimSayisi,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddExceptionHandler<GenelHataYakalayici>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddCheck<VeritabaniSaglikKontrolu>("veritabani", tags: ["hazirlik"]);

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
builder.Services.AddScoped<ProfilService>();
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
builder.Services.Configure<TersProxyAyarlari>(builder.Configuration.GetSection(TersProxyAyarlari.Bolum));
builder.Services.Configure<GirisGuvenligiAyarlari>(builder.Configuration.GetSection(GirisGuvenligiAyarlari.Bolum));

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

/* Gelistirmede bekleyen betikler acilista uygulanir: depoyu klonlayip
   "dotnet run" demek yeterli olsun, once ayri bir kurulum adimi
   hatirlamak gerekmesin.

   Uretimde BILEREK yapilmiyor. Uygulama ayaga kalkarken sema
   degistirmek, birden fazla ornegin ayni anda migration calistirmasina
   ve yavas bir betigin acilisi kilitlemesine yol acar. Orada
   "dotnet MarketOtomasyon.dll migrate" ayri bir deploy adimidir. */
if (app.Environment.IsDevelopment())
{
    var baglanti = builder.Configuration.GetConnectionString("MarketDb")!;

    /* Elle kurulmus bir veritabanina betikleri bastan uygulamak
       "tablo zaten var" hatasiyla patlar: 01_ilk_sema korumasiz
       CREATE TABLE iceriyor. Kullaniciyi o hataya dusurmek yerine ne
       yapmasi gerektigini soyle ve dokunma. */
    if (VeritabaniKurucu.BaselineGerekiyorMu(baglanti))
    {
        throw new InvalidOperationException(
            "Veritabanı elle kurulmuş: tablolar var ama sürüm takibi yok. " +
            "Şemanın güncel olduğundan eminseniz önce şunu çalıştırın: " +
            "dotnet run --project MarketOtomasyon -- migrate --baseline");
    }

    var kurulum = VeritabaniKurucu.Calistir(
        baglanti,
        demoVerisiDahil: false,
        new SerilogUpgradeLog(app.Logger));

    if (!kurulum.Basarili)
    {
        app.Logger.LogError("Veritabani kurulumu basarisiz: {Hata}", kurulum.Hata);
        throw new InvalidOperationException(
            $"Veritabanı kurulum betikleri uygulanamadı: {kurulum.Hata}");
    }

    if (kurulum.Uygulananlar.Count > 0)
        app.Logger.LogInformation(
            "Veritabani guncellendi. Uygulanan betik sayisi: {Adet}",
            kurulum.Uygulananlar.Count);
}

/* Vekil basliklari HER SEYDEN once islenmeli.
   Sonraki ara katmanlar (HSTS, HTTPS yonlendirmesi, kimlik dogrulama,
   loglama) istegin sema ve istemci adresi bilgisine bakiyor; duzeltme
   sonra yapilirsa onlar duz HTTP goruyor ve yanlis karar veriyor. */
var proxyAyarlari = app.Services.GetRequiredService<IOptions<TersProxyAyarlari>>().Value;

if (proxyAyarlari.Etkin)
{
    var yapilandirmaHatalari = proxyAyarlari.DogrulamaHatalari();
    if (yapilandirmaHatalari.Count > 0)
    {
        throw new InvalidOperationException(
            "Ters proxy yapılandırması geçersiz: " +
            string.Join(" ", yapilandirmaHatalari));
    }

    var secenekler = new ForwardedHeadersOptions
    {
        // Proto: HTTPS yonlendirme dongusunu engelleyen asil baslik.
        // For: denetim kayitlarinda vekilin degil istemcinin adresi.
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,

        // Varsayilan tek vekilli topoloji. Birden fazla vekil varsa bu
        // deger bilincli olarak artirilmali; sinirsiz birakilmaz.
        ForwardLimit = 1
    };

    var (proxyler, _) = proxyAyarlari.ProxyleriCoz();
    var (aglar, _) = proxyAyarlari.AglariCoz();

    /* Varsayilan listeler HER DURUMDA temizlenir.
       ASP.NET Core acilista loopback'i guvenilir sayar. Uzerine ekleme
       yapilsaydi "yalnizca 10.0.0.0/8'e guven" denmesine ragmen ayni
       makineden gelen sahte bir X-Forwarded-Proto: https basligi kabul
       edilirdi. Guvenilen kaynak listesi, adindan beklendigi gibi,
       tam liste olmali. */
    secenekler.KnownProxies.Clear();
    secenekler.KnownNetworks.Clear();

    if (proxyAyarlari.TumProxylereGuven)
    {
        /* Bos KnownProxies/KnownNetworks listesi tum kaynaklara guvenmek
           demektir. Buraya yalnizca acik TumProxylereGuven tercihiyle
           girilebilir; yanlis veya eksik ayar bunu dolayli acamaz. */
        app.Logger.LogWarning(
            "Tum proxy basliklari guvenilir kabul ediliyor. Uygulama portuna " +
            "dogrudan erisim ag seviyesinde engellenmis olmalidir.");
    }
    else
    {
        foreach (var adres in proxyler)
            secenekler.KnownProxies.Add(adres);

        foreach (var (adres, uzunluk) in aglar)
            secenekler.KnownNetworks.Add(new IPNetwork(adres, uzunluk));
    }

    app.UseForwardedHeaders(secenekler);

    app.Logger.LogInformation(
        "Ters vekil basliklari etkin. Guvenilen proxy: {Proxy}, ag: {Ag}, tumu: {Tumu}",
        proxyler.Count, aglar.Count, proxyAyarlari.TumProxylereGuven);
}

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

// Container ve yuk dengeleyici saglik yoklamalari ayni agda HTTP ile gelir.
// Bu iki teknik uc disindaki tum istekler HTTPS'e yonlendirilmeye devam eder.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/saglik"),
    dal => dal.UseHttpsRedirection());
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

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

// Canlilik yalnizca web surecinin cevap verdigini, hazirlik ise SQL Server'a
// erisilebildigini bildirir. Ikisi de kimlik dogrulama gerektirmez.
app.MapHealthChecks("/saglik/canli", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/saglik/hazir", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = kayit => kayit.Tags.Contains("hazirlik")
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// migrate komutu erken donebildigi icin giris noktasi int dondurur:
// normal calismada sunucu kapandiginda basarili cikis kodu verilir.
return 0;
