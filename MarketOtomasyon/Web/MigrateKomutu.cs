using MarketOtomasyon.Data;

namespace MarketOtomasyon.Web;

/// <summary>
/// "dotnet run -- migrate" komutu. Web sunucusunu baslatmadan yalnizca
/// kurulum betiklerini uygular.
///
/// Uretimde tercih edilen yol: deploy adimi olarak calistirilir, cikis
/// kodu kontrol edilir, sema hazir olduktan sonra uygulama baslatilir.
/// Boylece yarim uygulanmis bir semayla trafik almaya baslanmaz.
/// </summary>
public static class MigrateKomutu
{
    public static int Calistir(IConfiguration yapilandirma, string[] args)
    {
        var baglantiDizesi = yapilandirma.GetConnectionString("MarketDb");
        if (string.IsNullOrWhiteSpace(baglantiDizesi))
        {
            Console.Error.WriteLine(
                "Bağlantı dizesi tanımlı değil. " +
                "ConnectionStrings__MarketDb ortam değişkenini ayarlayın.");
            return 1;
        }

        var demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var yalnizListe = args.Contains("--liste", StringComparer.OrdinalIgnoreCase);
        var baseline = args.Contains("--baseline", StringComparer.OrdinalIgnoreCase);

        try
        {
            if (baseline)
                return BaselineUygula(baglantiDizesi, demo);

            /* Elle kurulmus bir veritabaninda migrate calistirmak
               "tablo zaten var" hatasiyla patlar. Kullaniciyi o hataya
               dusurmek yerine ne yapmasi gerektigini soyle. */
            if (VeritabaniKurucu.BaselineGerekiyorMu(baglantiDizesi))
            {
                Console.Error.WriteLine(
                    "Bu veritabanı elle kurulmuş: uygulama tabloları var ama sürüm takibi yok.\n" +
                    "Şemanın güncel olduğundan eminsen önce şunu çalıştır:\n\n" +
                    "    dotnet MarketOtomasyon.dll migrate --baseline\n\n" +
                    "Bu komut hiçbir betik çalıştırmaz; mevcut şemayı 'uygulanmış' olarak işaretler.");
                return 1;
            }

            var bekleyenler = VeritabaniKurucu.BekleyenBetikler(baglantiDizesi, demo);

            if (bekleyenler.Count == 0)
            {
                Console.WriteLine("Veritabanı güncel; uygulanacak betik yok.");
                return 0;
            }

            Console.WriteLine($"Bekleyen betik sayısı: {bekleyenler.Count}");
            foreach (var ad in bekleyenler)
                Console.WriteLine("  - " + KisaAd(ad));

            if (yalnizListe)
            {
                Console.WriteLine("\n--liste verildi; hiçbir betik çalıştırılmadı.");
                return 0;
            }

            if (!demo)
                Console.WriteLine("\nDemo verisi hariç. Eklemek için: --demo");

            Console.WriteLine();

            var sonuc = VeritabaniKurucu.Calistir(baglantiDizesi, demo);

            if (!sonuc.Basarili)
            {
                /* Hata durumunda uygulanan betikler geri ALINMAZ; her
                   betik kendi transaction'inda calisti ve basarili
                   olanlar SemaSurumu'na yazildi. Sorunu duzeltip komutu
                   tekrar calistirmak kaldigi yerden devam eder. */
                Console.Error.WriteLine($"\nHATA: {sonuc.Hata}");
                Console.Error.WriteLine(
                    $"Bu komuttan önce {sonuc.Uygulananlar.Count} betik uygulandı ve kaydedildi. " +
                    "Sorunu giderip komutu yeniden çalıştırın; kaldığı yerden devam eder.");
                return 1;
            }

            Console.WriteLine($"Tamamlandı. {sonuc.Uygulananlar.Count} betik uygulandı.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Veritabanına bağlanılamadı veya kurulum başlatılamadı: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Mevcut semayi "uygulanmis" olarak isaretler. Betik CALISTIRMAZ.
    /// </summary>
    private static int BaselineUygula(string baglantiDizesi, bool demo)
    {
        if (!VeritabaniKurucu.BaselineGerekiyorMu(baglantiDizesi))
        {
            Console.WriteLine(
                "Baseline gerekmiyor: bu veritabanında sürüm takibi zaten var " +
                "ya da veritabanı boş. Boşsa doğrudan 'migrate' çalıştırın.");
            return 0;
        }

        var sonuc = VeritabaniKurucu.Baseline(baglantiDizesi, demo);

        if (!sonuc.Basarili)
        {
            Console.Error.WriteLine($"Baseline başarısız: {sonuc.Hata}");
            return 1;
        }

        Console.WriteLine($"{sonuc.Uygulananlar.Count} betik 'uygulanmış' olarak işaretlendi:");
        foreach (var ad in sonuc.Uygulananlar)
            Console.WriteLine("  - " + KisaAd(ad));

        Console.WriteLine(
            "\nHiçbiri çalıştırılmadı. Bundan sonra eklenecek betikler " +
            "'migrate' ile normal şekilde uygulanır.");
        return 0;
    }

    /// <summary>Gomulu kaynak adindan yalnizca dosya adini birakir.</summary>
    private static string KisaAd(string kaynakAdi)
    {
        const string ayrac = "Data.Sql.";
        var yer = kaynakAdi.IndexOf(ayrac, StringComparison.Ordinal);
        return yer < 0 ? kaynakAdi : kaynakAdi[(yer + ayrac.Length)..];
    }
}
