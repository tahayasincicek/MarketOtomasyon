using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Builder;
using DbUp.Engine.Output;

namespace MarketOtomasyon.Data;

/// <summary>
/// Kurulum betiklerini sirayla calistirir ve hangilerinin uygulandigini
/// veritabaninda tutar.
///
/// Onceden betikler elle, dogru sirada calistiriliyordu. Deploy'un
/// ortasinda bir betik patlarsa hangisine kadar gelindigini soyleyen
/// hicbir kayit yoktu; tek yol tablolari tek tek kontrol etmekti.
/// Artik uygulanan her betik SemaSurumu tablosuna yaziliyor, yeniden
/// calistirildiginda yalnizca eksikler isleniyor.
///
/// SQL dosyalari degismedi: hala ham SQL, hala okunabilir. Degisen tek
/// sey nasil calistirildiklari.
/// </summary>
public static class VeritabaniKurucu
{
    /// <summary>
    /// Uygulanan betiklerin kaydedildigi tablo. DbUp'in varsayilan adi
    /// SchemaVersions; projenin geri kalani Turkce adlandirildigi icin
    /// bu da oyle.
    /// </summary>
    private const string SurumTablosu = "SemaSurumu";

    /// <summary>
    /// Demo betiklerinin numara oneki. Bunlar sahte satis gecmisi ve
    /// ornek tedarikci uretir; uretimde calistirilirsa ciro raporlarini
    /// bozar. Bu yuzden yalnizca acikca istendiginde dahil edilir.
    /// </summary>
    private const string DemoOneki = "Data.Sql.3";

    public sealed record Sonuc(bool Basarili, IReadOnlyList<string> Uygulananlar, string? Hata);

    /// <summary>
    /// Bekleyen betikleri uygular.
    /// </summary>
    /// <param name="demoVerisiDahil">
    /// true ise 30_/31_ demo betikleri de calisir. Uretimde daima false.
    /// </param>
    public static Sonuc Calistir(
        string baglantiDizesi, bool demoVerisiDahil, IUpgradeLog? kayit = null)
    {
        VeritabaniniOlustur(baglantiDizesi);

        var kurucu = Kurucu(baglantiDizesi, demoVerisiDahil)
            .WithTransactionPerScript();

        if (kayit is not null)
            kurucu = kurucu.LogTo(kayit);

        var sonuc = kurucu.Build().PerformUpgrade();

        var adlar = sonuc.Scripts.Select(s => s.Name).ToList();

        return new Sonuc(sonuc.Successful, adlar, sonuc.Error?.Message);
    }

    /// <summary>
    /// Mevcut bir veritabanini takip sistemine dahil eder: butun
    /// betikleri "uygulanmis" say, hicbirini CALISTIRMA.
    ///
    /// Bu, DbUp'a gecmeden once elle kurulmus veritabanlari icin.
    /// Betikler idempotent degil (01_ilk_sema korumasiz CREATE TABLE
    /// iceriyor), dolayisiyla dolu bir veritabaninda bastan
    /// calistirilamazlar. Baseline olmadan ilk migrate denemesi
    /// "tablo zaten var" hatasiyla patlardi.
    ///
    /// Yalnizca semasi guncel oldugu BILINEN veritabanlarinda
    /// kullanilmali: eksik bir betik varsa o da uygulanmis sayilir ve
    /// eksiklik sessizce kalir.
    /// </summary>
    public static Sonuc Baseline(string baglantiDizesi, bool demoVerisiDahil)
    {
        VeritabaniniOlustur(baglantiDizesi);

        var motor = Kurucu(baglantiDizesi, demoVerisiDahil).Build();
        var bekleyenler = motor.GetScriptsToExecute().Select(s => s.Name).ToList();

        var sonuc = motor.MarkAsExecuted();

        return new Sonuc(sonuc.Successful, bekleyenler, sonuc.Error?.Message);
    }

    /// <summary>
    /// Veritabaninda uygulama tablolari var ama takip tablosu yok mu?
    /// Bu durumda migrate calistirmak tehlikelidir; once baseline gerekir.
    /// </summary>
    public static bool BaselineGerekiyorMu(string baglantiDizesi)
    {
        using var baglanti = new Microsoft.Data.SqlClient.SqlConnection(baglantiDizesi);
        baglanti.Open();

        using var komut = baglanti.CreateCommand();
        komut.CommandText = $@"
SELECT CONVERT(BIT, CASE
    WHEN OBJECT_ID('dbo.{SurumTablosu}', 'U') IS NOT NULL THEN 0
    WHEN OBJECT_ID('dbo.Urun', 'U') IS NULL THEN 0
    ELSE 1
END);";

        return (bool)komut.ExecuteScalar()!;
    }

    /// <summary>Uygulanmayi bekleyen betiklerin adlari; hicbir betik calistirmaz.</summary>
    public static IReadOnlyList<string> BekleyenBetikler(
        string baglantiDizesi, bool demoVerisiDahil)
    {
        /* Burada da veritabani olusturulur: ilk kurulumda "neler
           bekliyor" diye bakabilmek icin once baglanabilmek gerekiyor.
           Bos bir veritabani yaratmak zararsiz - zaten bir sonraki
           adimda doldurulacak. */
        VeritabaniniOlustur(baglantiDizesi);

        return Kurucu(baglantiDizesi, demoVerisiDahil)
            .Build()
            .GetScriptsToExecute()
            .Select(s => s.Name)
            .ToList();
    }

    /* CREATE DATABASE, model veritabanini kopyaladigi ve dosya ayirdigi
       icin yavas sunucularda 30 saniyeyi asabiliyor. Bu bir kereye
       mahsus islem; comert bir sure vermek, kurulumun ilk adiminda
       anlamsiz bir zaman asimi almaktan iyi. */
    private const int VeritabaniOlusturmaSaniye = 180;

    /* Demo veri betigi 30 gunluk satis gecmisi uretiyor ve tek
       transaction icinde binlerce satir yaziyor; varsayilan komut
       zaman asimi buna yetmez. */
    private static readonly TimeSpan BetikZamanAsimi = TimeSpan.FromMinutes(10);

    /* Veritabani yoksa olusturulur: betiklerde artik CREATE DATABASE
       yok, cunku veritabani adi baglanti dizesinde yaziyor. Betige
       sabitlenseydi ayni betikler test ya da staging veritabanina
       uygulanamazdi. */
    private static void VeritabaniniOlustur(string baglantiDizesi)
        => EnsureDatabase.For.SqlDatabase(
            baglantiDizesi, new DbUp.Engine.Output.ConsoleUpgradeLog(), VeritabaniOlusturmaSaniye);

    private static UpgradeEngineBuilder Kurucu(string baglantiDizesi, bool demoVerisiDahil)
        => DeployChanges.To
            .SqlDatabase(baglantiDizesi)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                ad => BetikSecilsinMi(ad, demoVerisiDahil))
            .WithExecutionTimeout(BetikZamanAsimi)
            .JournalToSqlTable("dbo", SurumTablosu);

    /// <summary>
    /// Gomulu kaynak adindan betik secimi. Ad su bicimdedir:
    /// MarketOtomasyon.Data.Sql.01_ilk_sema.sql
    ///
    /// Dogrudan test edilebilsin diye public: demo betiklerinin
    /// uretimde disarida kalmasi bu metoda bagli ve yanlis calismasi
    /// gercek ciro raporlarina sahte satis karistirir.
    /// </summary>
    public static bool BetikSecilsinMi(string kaynakAdi, bool demoVerisiDahil)
    {
        if (!kaynakAdi.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!kaynakAdi.Contains("Data.Sql.", StringComparison.Ordinal))
            return false;

        if (!demoVerisiDahil && kaynakAdi.Contains(DemoOneki, StringComparison.Ordinal))
            return false;

        return true;
    }
}

/// <summary>DbUp ciktisini uygulamanin kendi loguna aktarir.</summary>
public sealed class SerilogUpgradeLog : IUpgradeLog
{
    private readonly ILogger _kayit;

    public SerilogUpgradeLog(ILogger kayit) => _kayit = kayit;

    public void LogTrace(string format, params object[] args) => _kayit.LogTrace(format, args);
    public void LogDebug(string format, params object[] args) => _kayit.LogDebug(format, args);
    public void LogInformation(string format, params object[] args) => _kayit.LogInformation(format, args);
    public void LogWarning(string format, params object[] args) => _kayit.LogWarning(format, args);
    public void LogError(string format, params object[] args) => _kayit.LogError(format, args);

    public void LogError(Exception ex, string format, params object[] args)
        => _kayit.LogError(ex, format, args);
}
