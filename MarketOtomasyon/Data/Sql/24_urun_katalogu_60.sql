/* Örnek ürün kataloğunu URN001-URN060 aralığına tamamlar. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID('tempdb..#YeniUrun') IS NOT NULL DROP TABLE #YeniUrun;

CREATE TABLE #YeniUrun (
    Kod          NVARCHAR(30)  NOT NULL,
    Ad           NVARCHAR(200) NOT NULL,
    KategoriKod  NVARCHAR(20)  NOT NULL,
    Birim        NVARCHAR(10)  NOT NULL,
    KdvOrani     DECIMAL(5,2)  NOT NULL,
    Tartili      BIT           NOT NULL,
    MinStok      DECIMAL(18,4) NOT NULL,
    Fiyat        DECIMAL(18,4) NOT NULL,
    Barkod       NVARCHAR(30)  NULL,
    TeraziKodu   NVARCHAR(30)  NULL,
    AcilisStok   DECIMAL(18,4) NOT NULL,
    AlisMaliyeti DECIMAL(18,4) NOT NULL
);

INSERT INTO #YeniUrun
    (Kod, Ad, KategoriKod, Birim, KdvOrani, Tartili, MinStok, Fiyat,
     Barkod, TeraziKodu, AcilisStok, AlisMaliyeti)
VALUES
    ('URN031', N'Yoğurt 1 kg',                 'KAHV', 'ADET',  1, 0,  8,  74.90, '8690000000395', NULL,      36,  52.00),
    ('URN032', N'Salatalık',                   'GIDA', 'KG',    1, 1,  5,  27.50, NULL,            '2800007', 42,  18.50),
    ('URN033', N'Patates',                     'GIDA', 'KG',    1, 1, 10,  18.90, NULL,            '2800008', 75,  12.25),
    ('URN034', N'Muz',                         'GIDA', 'KG',    1, 1,  8,  74.90, NULL,            '2800009', 38,  51.00),
    ('URN035', N'Tavuk Göğüs',                 'GIDA', 'KG',    1, 1,  5, 249.00, NULL,            '2800010', 22, 181.00),
    ('URN036', N'Kırmızı Mercimek 1 kg',       'GIDA', 'ADET',  1, 0,  8,  84.90, '8690000000449', NULL,      42,  61.50),
    ('URN037', N'Un 1 kg',                     'GIDA', 'ADET',  1, 0, 10,  34.50, '8690000000456', NULL,      60,  24.00),
    ('URN038', N'Domates Salçası 700 g',       'GIDA', 'ADET',  1, 0,  6,  89.90, '8690000000463', NULL,      30,  64.00),
    ('URN039', N'Mısır Konservesi 400 g',      'GIDA', 'ADET',  1, 0,  8,  44.90, '8690000000470', NULL,      36,  31.50),
    ('URN040', N'Toz Şeker 1 kg',              'GIDA', 'ADET',  1, 0, 10,  49.90, '8690000000487', NULL,      54,  35.50),
    ('URN041', N'Su 1,5 L',                    'ICEC', 'ADET', 10, 0, 24,  12.50, '8690000000494', NULL,     144,   7.25),
    ('URN042', N'Gazoz 1 L',                   'ICEC', 'ADET', 10, 0, 12,  36.90, '8690000000500', NULL,      72,  24.00),
    ('URN043', N'Şeftalili Soğuk Çay 1 L',     'ICEC', 'ADET', 10, 0, 10,  44.90, '8690000000517', NULL,      60,  29.50),
    ('URN044', N'Türk Kahvesi 100 g',          'KAHV', 'ADET', 10, 0,  8,  89.90, '8690000000524', NULL,      36,  62.00),
    ('URN045', N'Siyah Çay 500 g',             'KAHV', 'ADET', 10, 0,  6, 154.00, '8690000000531', NULL,      24, 111.00),
    ('URN046', N'Çikolatalı Gofret',           'ATIS', 'ADET', 10, 0, 20,  18.50, '8690000000548', NULL,     120,  11.25),
    ('URN047', N'Çubuk Kraker 100 g',          'ATIS', 'ADET', 10, 0, 18,  21.90, '8690000000555', NULL,      96,  13.50),
    ('URN048', N'Sakız 10''lu',                'ATIS', 'ADET', 10, 0, 20,  24.90, '8690000000562', NULL,     100,  15.00),
    ('URN049', N'Fındık',                      'ATIS', 'KG',   10, 1,  3, 649.00, NULL,            '2800011', 12, 465.00),
    ('URN050', N'Kuru Üzüm',                   'ATIS', 'KG',   10, 1,  3, 249.00, NULL,            '2800012', 16, 174.00),
    ('URN051', N'Yüzey Temizleyici 1 L',       'TEMZ', 'ADET', 20, 0,  8,  79.90, '8690000000593', NULL,      32,  51.00),
    ('URN052', N'Cam Temizleyici 750 ml',      'TEMZ', 'ADET', 20, 0,  8,  64.90, '8690000000609', NULL,      32,  41.50),
    ('URN053', N'Çöp Torbası Büyük Boy',       'TEMZ', 'ADET', 20, 0, 10,  58.50, '8690000000616', NULL,      48,  37.00),
    ('URN054', N'Kağıt Havlu 2''li',           'TEMZ', 'ADET', 20, 0, 10,  54.90, '8690000000623', NULL,      48,  34.50),
    ('URN055', N'Sıvı Sabun 500 ml',           'KBAK', 'ADET', 20, 0,  8,  49.90, '8690000000630', NULL,      36,  31.50),
    ('URN056', N'Duş Jeli 500 ml',             'KBAK', 'ADET', 20, 0,  6,  94.90, '8690000000647', NULL,      24,  62.00),
    ('URN057', N'Deodorant 150 ml',            'KBAK', 'ADET', 20, 0,  8, 119.00, '8690000000654', NULL,      30,  78.00),
    ('URN058', N'Diş Fırçası',                 'KBAK', 'ADET', 20, 0, 10,  54.90, '8690000000661', NULL,      48,  34.50),
    ('URN059', N'Bebek Bezi 30''lu',           'KBAK', 'ADET', 20, 0,  5, 329.00, '8690000000678', NULL,      18, 238.00),
    ('URN060', N'Islak Mendil 90''lı',         'KBAK', 'ADET', 20, 0, 12,  44.90, '8690000000685', NULL,      60,  28.50);

IF EXISTS (
    SELECT 1 FROM #YeniUrun y
    LEFT JOIN Kategori k ON k.Kod = y.KategoriKod
    WHERE k.Id IS NULL
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51000, 'Yeni ürünler için gerekli kategoriler bulunamadı.', 1;
END;

INSERT INTO Urun
    (Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif,
     ResimYolu, ResimKaynagi, ResimTarihi)
SELECT y.Kod, y.Ad, k.Id, y.Birim, y.KdvOrani, y.MinStok, y.Tartili, 1,
       N'/urun-gorsel/' + y.Kod + N'.webp', NULL, SYSUTCDATETIME()
FROM #YeniUrun y
JOIN Kategori k ON k.Kod = y.KategoriKod
WHERE NOT EXISTS (SELECT 1 FROM Urun u WHERE u.Kod = y.Kod);

/* Betik daha önce çalışmışsa görsel yollarını da doğru tut. */
UPDATE u
SET u.ResimYolu = N'/urun-gorsel/' + u.Kod + N'.webp',
    u.ResimKaynagi = NULL,
    u.ResimTarihi = COALESCE(u.ResimTarihi, SYSUTCDATETIME())
FROM Urun u
JOIN #YeniUrun y ON y.Kod = u.Kod;

INSERT INTO UrunBarkod (UrunId, Barkod, Carpan, Tip)
SELECT u.Id, y.Barkod, 1, 1
FROM #YeniUrun y
JOIN Urun u ON u.Kod = y.Kod
WHERE y.Barkod IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM UrunBarkod b WHERE b.Barkod = y.Barkod);

INSERT INTO UrunBarkod (UrunId, Barkod, Carpan, Tip)
SELECT u.Id, y.TeraziKodu, 1, 3
FROM #YeniUrun y
JOIN Urun u ON u.Kod = y.Kod
WHERE y.TeraziKodu IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM UrunBarkod b WHERE b.Barkod = y.TeraziKodu);

INSERT INTO UrunFiyat (UrunId, Fiyat)
SELECT u.Id, y.Fiyat
FROM #YeniUrun y
JOIN Urun u ON u.Kod = y.Kod
WHERE NOT EXISTS (
    SELECT 1 FROM UrunFiyat f WHERE f.UrunId = u.Id AND f.BitisTarihi IS NULL
);

INSERT INTO StokHareket (UrunId, DepoId, Yon, Miktar, KaynakTip, Aciklama)
SELECT u.Id, d.Id, 1, y.AcilisStok, 6, N'60 ürünlük katalog açılış stoğu'
FROM #YeniUrun y
JOIN Urun u ON u.Kod = y.Kod
CROSS JOIN (SELECT Id FROM Depo WHERE Kod = 'MRK') d
WHERE y.AcilisStok > 0
  AND NOT EXISTS (SELECT 1 FROM StokHareket h WHERE h.UrunId = u.Id);

IF OBJECT_ID('StokParti', 'U') IS NOT NULL
BEGIN
    INSERT INTO StokParti
        (UrunId, DepoId, StokHareketId, GirisTarihi, GirisMiktari,
         KalanMiktar, BirimMaliyet, Aciklama)
    SELECT u.Id, d.Id, h.Id, h.Tarih, y.AcilisStok,
           y.AcilisStok, y.AlisMaliyeti, N'60 ürünlük katalog FIFO açılış partisi'
    FROM #YeniUrun y
    JOIN Urun u ON u.Kod = y.Kod
    CROSS JOIN (SELECT Id FROM Depo WHERE Kod = 'MRK') d
    CROSS APPLY (
        SELECT TOP (1) sh.Id, sh.Tarih
        FROM StokHareket sh
        WHERE sh.UrunId = u.Id
          AND sh.DepoId = d.Id
          AND sh.KaynakTip = 6
        ORDER BY sh.Id
    ) h
    WHERE y.AcilisStok > 0
      AND NOT EXISTS (
          SELECT 1 FROM StokParti p
          WHERE p.UrunId = u.Id AND p.DepoId = d.Id
      );
END;

IF (SELECT COUNT(*) FROM Urun WHERE Kod LIKE N'URN[0-9][0-9][0-9]' AND TRY_CONVERT(INT, RIGHT(Kod, 3)) BETWEEN 1 AND 60) <> 60
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51000, 'URN001-URN060 ürün kataloğu eksik oluşturuldu.', 1;
END;

DROP TABLE #YeniUrun;
COMMIT TRANSACTION;
GO
