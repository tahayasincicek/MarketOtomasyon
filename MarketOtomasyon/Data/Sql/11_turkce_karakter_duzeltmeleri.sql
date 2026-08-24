/* =========================================================
   Ekranda gorunen mevcut ornek verileri Turkce karakterlere
   cevirir. Kodlara gore calistigi icin tekrar uygulanabilir.
   ========================================================= */

USE MarketOtomasyon;
GO

UPDATE Kategori
SET Ad = CASE Kod
    WHEN 'GIDA' THEN N'Gıda'
    WHEN 'ICEC' THEN N'İçecek'
    WHEN 'ATIS' THEN N'Atıştırmalık'
    WHEN 'KBAK' THEN N'Kişisel Bakım'
    WHEN 'KAHV' THEN N'Kahvaltılık'
    ELSE Ad
END
WHERE Kod IN ('GIDA', 'ICEC', 'ATIS', 'KBAK', 'KAHV');

UPDATE Urun
SET Ad = CASE Kod
    WHEN 'URN001' THEN N'Süt 1 L'
    WHEN 'URN003' THEN N'Çamaşır Suyu 1 L'
    WHEN 'URN005' THEN N'Pirinç 1 kg'
    WHEN 'URN007' THEN N'Ayçiçek Yağı 1 L'
    WHEN 'URN009' THEN N'Kıyma'
    WHEN 'URN010' THEN N'Yumurta 10''lu'
    WHEN 'URN012' THEN N'Kaşar Peyniri'
    WHEN 'URN013' THEN N'Tereyağı 250 g'
    WHEN 'URN016' THEN N'Reçel Vişne 380 g'
    WHEN 'URN022' THEN N'Çikolata 80 g'
    WHEN 'URN024' THEN N'Bisküvi 200 g'
    WHEN 'URN025' THEN N'Kuruyemiş Karışık'
    WHEN 'URN026' THEN N'Bulaşık Deterjanı 750 ml'
    WHEN 'URN027' THEN N'Çamaşır Deterjanı 3 kg'
    WHEN 'URN028' THEN N'Tuvalet Kağıdı 16''lı'
    WHEN 'URN029' THEN N'Şampuan 500 ml'
    WHEN 'URN030' THEN N'Diş Macunu 75 ml'
    ELSE Ad
END
WHERE Kod IN (
    'URN001', 'URN003', 'URN005', 'URN007', 'URN009', 'URN010',
    'URN012', 'URN013', 'URN016', 'URN022', 'URN024', 'URN025',
    'URN026', 'URN027', 'URN028', 'URN029', 'URN030'
);

UPDATE Depo SET Ad = N'Market Rafı' WHERE Kod = 'MRK';
UPDATE Kullanici SET AdSoyad = N'Test Müdür' WHERE KullaniciAdi = 'mudur';

/* Daha once sistem tarafindan uretilen hareket aciklamalari. Kullanici
   aciklamalari degil, yalnizca bilinen sabit on ekler donusturulur. */
UPDATE StokHareket
SET Aciklama = N'Satış ' + SUBSTRING(Aciklama, 7, LEN(Aciklama))
WHERE Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'Satis %';

UPDATE StokHareket
SET Aciklama = N'İade ' + SUBSTRING(Aciklama, 6, LEN(Aciklama))
WHERE Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'Iade %';

UPDATE StokHareket
SET Aciklama = REPLACE(Aciklama COLLATE Latin1_General_100_BIN2, N' / Fis ', N' / Fiş ')
WHERE Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'% / Fis %';

UPDATE StokHareket
SET Aciklama = N'Sayım ' + SUBSTRING(Aciklama, 7, LEN(Aciklama))
WHERE Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'Sayim %';

UPDATE StokHareket
SET Aciklama = REPLACE(Aciklama COLLATE Latin1_General_100_BIN2, N', sayilan ', N', sayılan ')
WHERE Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'Sayım %'
  AND Aciklama COLLATE Latin1_General_100_BIN2 LIKE N'%, sayilan %';

UPDATE StokHareket
SET Aciklama = N'Örnek veri açılış stoğu'
WHERE Aciklama COLLATE Latin1_General_100_BIN2 = N'Ornek veri acilis stogu';
GO

SELECT Kod, Ad FROM Kategori ORDER BY Kod;
SELECT Kod, Ad FROM Urun ORDER BY Kod;
SELECT Kod, Ad FROM Depo ORDER BY Kod;
GO
