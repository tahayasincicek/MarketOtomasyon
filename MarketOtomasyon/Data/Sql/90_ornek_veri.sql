/* =========================================================
   Market Otomasyonu - Ornek/test verisi
   Sema degil: bu dosya sadece gelistirme ve deneme verisidir.

   Tekrar tekrar calistirilabilir (idempotent): var olan kayitlari
   atlar, sadece eksikleri ekler. Uretimde CALISTIRILMAZ.

   Icerik: 6 kategori, 30 urun, barkodlar (tekli + koli),
           acilis fiyatlari ve acilis stok hareketleri.
   ========================================================= */

USE MarketOtomasyon;
GO

/* ---------- Depolar ---------- */
INSERT INTO Depo (Kod, Ad)
SELECT v.Kod, v.Ad
FROM (VALUES
    ('MRK', 'Market Rafi'),
    ('DEP', 'Arka Depo')
) AS v (Kod, Ad)
WHERE NOT EXISTS (SELECT 1 FROM Depo d WHERE d.Kod = v.Kod);

/* ---------- Kullanicilar ----------
   SifreHash gercek bir hash degil; oturum acma yazilinca degisecek. */
INSERT INTO Kullanici (KullaniciAdi, AdSoyad, SifreHash, Rol)
SELECT v.KullaniciAdi, v.AdSoyad, v.SifreHash, v.Rol
FROM (VALUES
    ('kasiyer1', 'Test Kasiyer', 'DEGISTIR', 1),
    ('mudur',    'Test Mudur',   'DEGISTIR', 2)
) AS v (KullaniciAdi, AdSoyad, SifreHash, Rol)
WHERE NOT EXISTS (SELECT 1 FROM Kullanici k WHERE k.KullaniciAdi = v.KullaniciAdi);
GO

/* ---------- Kategoriler ---------- */
INSERT INTO Kategori (Kod, Ad)
SELECT v.Kod, v.Ad
FROM (VALUES
    ('GIDA', 'Gida'),
    ('TEMZ', 'Temizlik'),
    ('ICEC', 'Icecek'),
    ('ATIS', 'Atistirmalik'),
    ('KBAK', 'Kisisel Bakim'),
    ('KAHV', 'Kahvaltilik')
) AS v (Kod, Ad)
WHERE NOT EXISTS (SELECT 1 FROM Kategori k WHERE k.Kod = v.Kod);
GO

/* ---------- Urun listesi ----------
   Tum veri tek tabloda toplanip asagidaki adimlarda kullanilir:
   urun karti, barkod, acilis fiyati, acilis stogu.               */
IF OBJECT_ID('tempdb..#OrnekUrun') IS NOT NULL DROP TABLE #OrnekUrun;

CREATE TABLE #OrnekUrun (
    Kod          NVARCHAR(30),
    Ad           NVARCHAR(200),
    KategoriKod  NVARCHAR(20),
    Birim        NVARCHAR(10),
    KdvOrani     DECIMAL(5,2),
    Tartili      BIT,
    MinStok      DECIMAL(18,4),
    Fiyat        DECIMAL(18,4),
    Barkod       NVARCHAR(30),
    KoliBarkod   NVARCHAR(30),   -- NULL ise koli barkodu yok
    KoliCarpan   DECIMAL(18,4),
    AcilisStok   DECIMAL(18,4)
);

INSERT INTO #OrnekUrun
    (Kod, Ad, KategoriKod, Birim, KdvOrani, Tartili, MinStok, Fiyat, Barkod, KoliBarkod, KoliCarpan, AcilisStok)
VALUES
    -- Gida (temel gida %1)
    ('URN001', 'Sut 1 L',                  'GIDA', 'ADET',  1, 0, 12,  32.50, '8690000000017', '8690000000024', 12,  48),
    ('URN002', 'Domates',                  'GIDA', 'KG',    1, 1,  5,  24.90, NULL,            NULL,          NULL,  35.500),
    ('URN004', 'Ekmek 250 g',              'GIDA', 'ADET',  1, 0, 20,  15.00, '8690000000048', NULL,          NULL, 120),
    ('URN005', 'Pirinc 1 kg',              'GIDA', 'ADET',  1, 0,  6,  78.90, '8690000000055', '8690000000062',  6,  30),
    ('URN006', 'Makarna 500 g',            'GIDA', 'ADET',  1, 0, 12,  18.75, '8690000000079', '8690000000086', 24,  96),
    ('URN007', 'Aycicek Yagi 1 L',         'GIDA', 'ADET',  1, 0,  8, 112.00, '8690000000093', '8690000000109', 12,  36),
    ('URN008', 'Elma',                     'GIDA', 'KG',    1, 1, 10,  39.90, NULL,            NULL,          NULL,  62.250),
    ('URN009', 'Kiyma',                    'GIDA', 'KG',    1, 1,  3, 389.00, NULL,            NULL,          NULL,   8.400),
    ('URN010', 'Yumurta 10 lu',            'GIDA', 'ADET',  1, 0, 10,  84.50, '8690000000116', NULL,          NULL,  45),

    -- Kahvaltilik
    ('URN011', 'Beyaz Peynir',             'KAHV', 'KG',    1, 1,  4, 289.00, NULL,            NULL,          NULL,  11.750),
    ('URN012', 'Kasar Peyniri',            'KAHV', 'KG',    1, 1,  4, 419.00, NULL,            NULL,          NULL,   6.300),
    ('URN013', 'Tereyagi 250 g',           'KAHV', 'ADET',  1, 0,  6, 147.50, '8690000000123', NULL,          NULL,  24),
    ('URN014', 'Bal 850 g',                'KAHV', 'ADET',  1, 0,  3, 265.00, '8690000000130', NULL,          NULL,  12),
    ('URN015', 'Zeytin Siyah 400 g',       'KAHV', 'ADET',  1, 0,  6,  96.00, '8690000000147', '8690000000154', 12,  36),
    ('URN016', 'Recel Visne 380 g',        'KAHV', 'ADET',  1, 0,  6,  67.50, '8690000000161', NULL,          NULL,  28),

    -- Icecek (%10)
    ('URN017', 'Su 5 L',                   'ICEC', 'ADET', 10, 0, 15,  29.90, '8690000000178', '8690000000185',  4,  60),
    ('URN018', 'Kola 1 L',                 'ICEC', 'ADET', 10, 0, 12,  42.00, '8690000000192', '8690000000208', 12,  72),
    ('URN019', 'Portakal Suyu 1 L',        'ICEC', 'ADET', 10, 0,  8,  54.90, '8690000000215', '8690000000222', 12,  48),
    ('URN020', 'Ayran 300 ml',             'ICEC', 'ADET',  1, 0, 24,  14.50, '8690000000239', '8690000000246', 24,  96),
    ('URN021', 'Maden Suyu 200 ml',        'ICEC', 'ADET', 10, 0, 24,  11.00, '8690000000253', '8690000000260', 24, 144),

    -- Atistirmalik (%10)
    ('URN022', 'Cikolata 80 g',            'ATIS', 'ADET', 10, 0, 20,  38.90, '8690000000277', '8690000000284', 24, 120),
    ('URN023', 'Cips 110 g',               'ATIS', 'ADET', 10, 0, 15,  47.50, '8690000000291', NULL,          NULL,  60),
    ('URN024', 'Biskuvi 200 g',            'ATIS', 'ADET', 10, 0, 18,  26.00, '8690000000307', '8690000000314', 24,  96),
    ('URN025', 'Kuruyemis Karisik',        'ATIS', 'KG',   10, 1,  3, 549.00, NULL,            NULL,          NULL,   7.800),

    -- Temizlik (%20)
    ('URN003', 'Camasir Suyu 1 L',         'TEMZ', 'ADET', 20, 0,  8,  89.00, '8690000000031', NULL,          NULL,  32),
    ('URN026', 'Bulasik Deterjani 750 ml', 'TEMZ', 'ADET', 20, 0,  8,  76.50, '8690000000321', '8690000000338', 12,  36),
    ('URN027', 'Camasir Deterjani 3 kg',   'TEMZ', 'ADET', 20, 0,  5, 289.00, '8690000000345', NULL,          NULL,  20),
    ('URN028', 'Tuvalet Kagidi 16 lu',     'TEMZ', 'ADET', 20, 0,  6, 219.00, '8690000000352', NULL,          NULL,  24),

    -- Kisisel bakim (%20)
    ('URN029', 'Sampuan 500 ml',           'KBAK', 'ADET', 20, 0,  6, 134.90, '8690000000369', '8690000000376',  6,  18),
    ('URN030', 'Dis Macunu 75 ml',         'KBAK', 'ADET', 20, 0,  8,  68.00, '8690000000383', NULL,          NULL,  40);
GO

/* ---------- 1) Urun kartlari ---------- */
INSERT INTO Urun (Kod, Ad, KategoriId, Birim, KdvOrani, MinStokSeviyesi, Tartili, Aktif)
SELECT o.Kod, o.Ad, k.Id, o.Birim, o.KdvOrani, o.MinStok, o.Tartili, 1
FROM #OrnekUrun o
JOIN Kategori k ON k.Kod = o.KategoriKod
WHERE NOT EXISTS (SELECT 1 FROM Urun u WHERE u.Kod = o.Kod);

/* ---------- 2) Tekli barkodlar ---------- */
INSERT INTO UrunBarkod (UrunId, Barkod, Carpan, Tip)
SELECT u.Id, o.Barkod, 1, 1
FROM #OrnekUrun o
JOIN Urun u ON u.Kod = o.Kod
WHERE o.Barkod IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM UrunBarkod b WHERE b.Barkod = o.Barkod);

/* ---------- 3) Koli barkodlari ---------- */
INSERT INTO UrunBarkod (UrunId, Barkod, Carpan, Tip)
SELECT u.Id, o.KoliBarkod, o.KoliCarpan, 2
FROM #OrnekUrun o
JOIN Urun u ON u.Kod = o.Kod
WHERE o.KoliBarkod IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM UrunBarkod b WHERE b.Barkod = o.KoliBarkod);

/* ---------- 4) Acilis fiyatlari ----------
   Yalnizca hic acik fiyati olmayan urunlere yazilir; mevcut
   fiyat gecmisi bozulmaz. */
INSERT INTO UrunFiyat (UrunId, Fiyat)
SELECT u.Id, o.Fiyat
FROM #OrnekUrun o
JOIN Urun u ON u.Kod = o.Kod
WHERE NOT EXISTS (
    SELECT 1 FROM UrunFiyat f WHERE f.UrunId = u.Id AND f.BitisTarihi IS NULL
);

/* ---------- 5) Acilis stogu ----------
   KaynakTip 6 = acilis. Market rafina (MRK) giris olarak islenir.
   Zaten hareketi olan urune ikinci kez acilis yazilmaz. */
INSERT INTO StokHareket (UrunId, DepoId, Yon, Miktar, KaynakTip, Aciklama)
SELECT u.Id, d.Id, 1, o.AcilisStok, 6, 'Ornek veri acilis stogu'
FROM #OrnekUrun o
JOIN Urun u ON u.Kod = o.Kod
CROSS JOIN (SELECT Id FROM Depo WHERE Kod = 'MRK') d
WHERE o.AcilisStok > 0
  AND NOT EXISTS (SELECT 1 FROM StokHareket h WHERE h.UrunId = u.Id);

DROP TABLE #OrnekUrun;
GO

/* ---------- Ozet ---------- */
SELECT 'Kategori' AS Tablo, COUNT(*) AS Adet FROM Kategori
UNION ALL SELECT 'Urun',        COUNT(*) FROM Urun
UNION ALL SELECT 'UrunBarkod',  COUNT(*) FROM UrunBarkod
UNION ALL SELECT 'UrunFiyat',   COUNT(*) FROM UrunFiyat
UNION ALL SELECT 'StokHareket', COUNT(*) FROM StokHareket;
GO
