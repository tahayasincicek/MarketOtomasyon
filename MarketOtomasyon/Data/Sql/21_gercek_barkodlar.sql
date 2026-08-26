/* =========================================================
   Seed barkodlarini gercek Turk urunleriyle degistirir.
   ---------------------------------------------------------
   Ilk seed'deki 869 0000 000 xx barkodlari uydurmadir:
   869 Turkiye GS1 onekidir ama 0000000 diye bir firma yok.
   Bu yuzden hicbir acik urun veritabaninda karsiliklari
   bulunmuyor ve fotograf cekilemiyor.

   Asagidaki barkodlarin tamami Open Food Facts uzerinde
   tek tek dogrulandi; kayitlari ve fotograflari var.

   Yalnizca TEKLI (Tip = 1) barkodlar degisir. Koli
   barkodlari uydurma kalir: koli ayri bir ambalajdir,
   urunun kendi fotografini vermez.

   Tartili urunler (Domates, Elma, Kiyma, peynirler,
   kuruyemis) barkodsuz satildigi icin listede yoktur.
   ========================================================= */

USE MarketOtomasyon;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @yeni TABLE (Kod NVARCHAR(30), Barkod NVARCHAR(30), Karsilik NVARCHAR(100));

INSERT INTO @yeni (Kod, Barkod, Karsilik) VALUES
    ('URN001', '8690565100530', N'Pınar Süt 1 lt'),
    ('URN004', '8690698511760', N'UNO Çok Tahıllı Ekmek'),
    ('URN006', '8690579140614', 'Barilla Penne Rigate 500 g'),
    ('URN007', '8695077044198', N'Sole Ayçiçek Yağı 1 L'),
    ('URN018', '5000112664492', 'Coca-Cola 1 L'),
    ('URN020', '8690767710537', N'Sütaş Ayran 200 ml'),
    ('URN021', '8691381000486', N'Beypazarı Doğal Maden Suyu'),
    ('URN022', '8690504135913', N'Ülker Çikolata'),
    ('URN024', '8690504017301', N'Ülker Çubuk Kraker');

/* Ayni barkod baska bir urunde duruyorsa UNIQUE kisiti patlar.
   Once cakismayi bildirelim ki hata mesaji anlasilir olsun. */
IF EXISTS (
    SELECT 1
    FROM UrunBarkod b
    JOIN @yeni y ON y.Barkod = b.Barkod
    JOIN Urun u ON u.Id = b.UrunId
    WHERE u.Kod <> y.Kod
)
BEGIN
    RAISERROR(N'Bu barkodlardan biri başka bir üründe kayıtlı. Önce onu temizleyin.', 16, 1);
    RETURN;
END

UPDATE b
SET    b.Barkod = y.Barkod
FROM   UrunBarkod b
JOIN   Urun u ON u.Id = b.UrunId
JOIN   @yeni y ON y.Kod = u.Kod
WHERE  b.Tip = 1;

SELECT u.Kod, u.Ad, b.Barkod, y.Karsilik
FROM   Urun u
JOIN   UrunBarkod b ON b.UrunId = u.Id AND b.Tip = 1
JOIN   @yeni y ON y.Kod = u.Kod
ORDER BY u.Kod;
GO
