/* =========================================================
   Modul demo verileri
   ---------------------------------------------------------
   Bos kalan tedarikci ve alis faturasi ekranlarini gercekci,
   birbiriyle tutarli verilerle doldurur. Her fatura satiri icin
   stok giris hareketi ve maliyet/SKT/lot partisi de olusturulur.

   Tekrar calistirilabilir: TEDxxx kodlu tedarikcileri ve
   DEMO-AF-xxx faturalarini ikinci kez eklemez.
   URETIMDE CALISTIRILMAZ.
   ========================================================= */

USE MarketOtomasyon;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.Tedarikci', 'U') IS NULL
   OR OBJECT_ID('dbo.AlisFaturasi', 'U') IS NULL
   OR OBJECT_ID('dbo.AlisFaturasiSatir', 'U') IS NULL
   OR OBJECT_ID('dbo.StokParti', 'U') IS NULL
BEGIN
    THROW 51000, N'Önce 16_tedarikci_fatura.sql dahil şema betiklerini çalıştırın.', 1;
END;
GO

BEGIN TRANSACTION;

/* ---------- Tedarikci kartlari ---------- */
IF OBJECT_ID('tempdb..#TedarikciPlan') IS NOT NULL DROP TABLE #TedarikciPlan;

CREATE TABLE #TedarikciPlan
(
    Kod           NVARCHAR(20)  NOT NULL PRIMARY KEY,
    Unvan         NVARCHAR(200) NOT NULL,
    VergiNo       NVARCHAR(11)  NULL,
    VergiDairesi  NVARCHAR(100) NULL,
    Telefon       NVARCHAR(20)  NULL,
    Eposta        NVARCHAR(150) NULL,
    Adres         NVARCHAR(300) NULL
);

INSERT INTO #TedarikciPlan VALUES
    (N'TED001', N'Anadolu Gıda Dağıtım A.Ş.', N'1234567890', N'İstanbul Kurumlar', N'0212 555 10 10', N'siparis@anadolugida.example', N'Bayrampaşa / İstanbul'),
    (N'TED002', N'Marmara İçecek Pazarlama Ltd. Şti.', N'2345678901', N'Üsküdar', N'0216 555 20 20', N'satis@marmaraicecek.example', N'Ümraniye / İstanbul'),
    (N'TED003', N'Bereket Süt ve Kahvaltılık Ürünleri', N'3456789012', N'Bursa', N'0224 555 30 30', N'bayi@bereketsut.example', N'Nilüfer / Bursa'),
    (N'TED004', N'TemizEv Tüketim Ürünleri A.Ş.', N'4567890123', N'Kocaeli', N'0262 555 40 40', N'kurumsal@temizev.example', N'Gebze / Kocaeli'),
    (N'TED005', N'Doğal Tarım Kooperatifi', N'5678901234', N'İzmir', N'0232 555 50 50', N'sevkiyat@dogaltarim.example', N'Torbalı / İzmir');

INSERT INTO dbo.Tedarikci
    (Kod, Unvan, VergiNo, VergiDairesi, Telefon, Eposta, Adres, Aktif)
SELECT v.Kod, v.Unvan, v.VergiNo, v.VergiDairesi,
       v.Telefon, v.Eposta, v.Adres, 1
FROM #TedarikciPlan v
WHERE NOT EXISTS (SELECT 1 FROM dbo.Tedarikci t WHERE t.Kod = v.Kod);

/* Seed kaydi daha once farkli bir kod sayfasiyla calistirildiysa da
   demo kartlarini dogru Turkce metne getirir. Kullanici kartlarina dokunmaz. */
UPDATE t
SET t.Unvan = v.Unvan,
    t.VergiNo = v.VergiNo,
    t.VergiDairesi = v.VergiDairesi,
    t.Telefon = v.Telefon,
    t.Eposta = v.Eposta,
    t.Adres = v.Adres
FROM dbo.Tedarikci t
JOIN #TedarikciPlan v ON v.Kod = t.Kod;

/* ---------- Fatura ve satir plani ---------- */
IF OBJECT_ID('tempdb..#FaturaPlan') IS NOT NULL DROP TABLE #FaturaPlan;
IF OBJECT_ID('tempdb..#SatirPlan') IS NOT NULL DROP TABLE #SatirPlan;

CREATE TABLE #FaturaPlan
(
    TedarikciKod NVARCHAR(20) NOT NULL,
    FaturaNo     NVARCHAR(30) NOT NULL,
    GunOnce      INT NOT NULL,
    DepoKod      NVARCHAR(20) NOT NULL,
    Aciklama     NVARCHAR(300) NULL,
    PRIMARY KEY (TedarikciKod, FaturaNo)
);

CREATE TABLE #SatirPlan
(
    TedarikciKod NVARCHAR(20) NOT NULL,
    FaturaNo     NVARCHAR(30) NOT NULL,
    SatirNo      INT NOT NULL,
    UrunKod      NVARCHAR(30) NOT NULL,
    Miktar       DECIMAL(18,4) NOT NULL,
    BirimFiyat   DECIMAL(18,4) NOT NULL,
    SktGun       INT NOT NULL,
    LotNo        NVARCHAR(50) NOT NULL,
    PRIMARY KEY (TedarikciKod, FaturaNo, SatirNo)
);

INSERT INTO #FaturaPlan VALUES
    (N'TED001', N'DEMO-AF-001', 29, N'DEP', N'Aylık temel gıda tedariki'),
    (N'TED002', N'DEMO-AF-002', 23, N'DEP', N'İçecek grubu haftalık sevkiyat'),
    (N'TED003', N'DEMO-AF-003', 17, N'MRK', N'Süt ve kahvaltılık ürün kabulü'),
    (N'TED004', N'DEMO-AF-004', 11, N'DEP', N'Temizlik ve kişisel bakım sevkiyatı'),
    (N'TED005', N'DEMO-AF-005',  6, N'MRK', N'Taze ürün günlük tedariki'),
    (N'TED001', N'DEMO-AF-006',  2, N'DEP', N'Hafta sonu öncesi stok takviyesi');

INSERT INTO #SatirPlan VALUES
    (N'TED001', N'DEMO-AF-001', 1, N'URN005',  30,  62.50, 365, N'AG-PR-260701'),
    (N'TED001', N'DEMO-AF-001', 2, N'URN006',  72,  13.25, 300, N'AG-MK-260701'),
    (N'TED001', N'DEMO-AF-001', 3, N'URN007',  24,  87.50, 420, N'AG-YG-260701'),

    (N'TED002', N'DEMO-AF-002', 1, N'URN017',  48,  21.50, 300, N'MI-SU-260702'),
    (N'TED002', N'DEMO-AF-002', 2, N'URN018',  60,  31.25, 240, N'MI-KL-260702'),
    (N'TED002', N'DEMO-AF-002', 3, N'URN021', 120,   7.60, 365, N'MI-MS-260702'),

    (N'TED003', N'DEMO-AF-003', 1, N'URN001',  72,  25.40,  90, N'BS-ST-260703'),
    (N'TED003', N'DEMO-AF-003', 2, N'URN013',  24, 112.00, 150, N'BS-TR-260703'),
    (N'TED003', N'DEMO-AF-003', 3, N'URN015',  36,  73.50, 270, N'BS-ZY-260703'),

    (N'TED004', N'DEMO-AF-004', 1, N'URN026',  36,  58.00, 540, N'TE-BD-260704'),
    (N'TED004', N'DEMO-AF-004', 2, N'URN027',  20, 218.00, 720, N'TE-CD-260704'),
    (N'TED004', N'DEMO-AF-004', 3, N'URN029',  24, 101.50, 540, N'TE-SP-260704'),

    (N'TED005', N'DEMO-AF-005', 1, N'URN002',  55,  17.80,  18, N'DT-DM-260705'),
    (N'TED005', N'DEMO-AF-005', 2, N'URN008',  60,  29.50,  35, N'DT-EL-260705'),
    (N'TED005', N'DEMO-AF-005', 3, N'URN010',  40,  66.00,  45, N'DT-YM-260705'),

    (N'TED001', N'DEMO-AF-006', 1, N'URN004', 120,  10.50,  12, N'AG-EK-260706'),
    (N'TED001', N'DEMO-AF-006', 2, N'URN024',  72,  19.20, 240, N'AG-BS-260706'),
    (N'TED001', N'DEMO-AF-006', 3, N'URN022',  48,  29.40, 300, N'AG-CK-260706');

IF EXISTS
(
    SELECT 1
    FROM #SatirPlan p
    LEFT JOIN dbo.Urun u ON u.Kod = p.UrunKod
    WHERE u.Id IS NULL
)
    THROW 51001, N'Fatura planındaki ürünlerden biri bulunamadı. Önce 90_ornek_veri.sql çalıştırılmalıdır.', 1;

DECLARE @KullaniciId INT =
    COALESCE((SELECT TOP (1) Id FROM dbo.Kullanici WHERE KullaniciAdi = N'mudur' AND Aktif = 1),
             (SELECT TOP (1) Id FROM dbo.Kullanici WHERE Aktif = 1 ORDER BY Id));

IF @KullaniciId IS NULL
    THROW 51002, N'Fatura kaydı için aktif kullanıcı bulunamadı.', 1;

/* Fatura toplamlari satir planindan hesaplanir. Alis fiyati KDV harictir. */
INSERT INTO dbo.AlisFaturasi
    (TedarikciId, FaturaNo, FaturaTarihi, KayitTarihi, KullaniciId,
     DepoId, AraToplam, ToplamKdv, GenelToplam, Aciklama)
SELECT t.Id,
       fp.FaturaNo,
       DATEADD(DAY, -fp.GunOnce, CONVERT(DATE, GETDATE())),
       DATEADD(HOUR, 10, CONVERT(DATETIME2, DATEADD(DAY, -fp.GunOnce, CONVERT(DATE, GETDATE())))),
       @KullaniciId,
       d.Id,
       x.AraToplam,
       x.ToplamKdv,
       x.AraToplam + x.ToplamKdv,
       fp.Aciklama
FROM #FaturaPlan fp
JOIN dbo.Tedarikci t ON t.Kod = fp.TedarikciKod
JOIN dbo.Depo d ON d.Kod = fp.DepoKod
CROSS APPLY
(
    SELECT AraToplam = SUM(sp.Miktar * sp.BirimFiyat),
           ToplamKdv = SUM(sp.Miktar * sp.BirimFiyat * u.KdvOrani / 100.0)
    FROM #SatirPlan sp
    JOIN dbo.Urun u ON u.Kod = sp.UrunKod
    WHERE sp.TedarikciKod = fp.TedarikciKod
      AND sp.FaturaNo = fp.FaturaNo
) x
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.AlisFaturasi f
    WHERE f.TedarikciId = t.Id AND f.FaturaNo = fp.FaturaNo
);

UPDATE f
SET f.Aciklama = fp.Aciklama
FROM dbo.AlisFaturasi f
JOIN dbo.Tedarikci t ON t.Id = f.TedarikciId
JOIN #FaturaPlan fp ON fp.TedarikciKod = t.Kod AND fp.FaturaNo = f.FaturaNo;

INSERT INTO dbo.AlisFaturasiSatir
    (FaturaId, SatirNo, UrunId, Miktar, BirimFiyat, KdvOrani,
     SatirMatrah, SatirKdv, SonKullanmaTarihi, LotNo)
SELECT f.Id,
       sp.SatirNo,
       u.Id,
       sp.Miktar,
       sp.BirimFiyat,
       u.KdvOrani,
       sp.Miktar * sp.BirimFiyat,
       sp.Miktar * sp.BirimFiyat * u.KdvOrani / 100.0,
       DATEADD(DAY, sp.SktGun, CONVERT(DATE, GETDATE())),
       sp.LotNo
FROM #SatirPlan sp
JOIN dbo.Tedarikci t ON t.Kod = sp.TedarikciKod
JOIN dbo.AlisFaturasi f ON f.TedarikciId = t.Id AND f.FaturaNo = sp.FaturaNo
JOIN dbo.Urun u ON u.Kod = sp.UrunKod
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.AlisFaturasiSatir s
    WHERE s.FaturaId = f.Id AND s.SatirNo = sp.SatirNo
);

/* KaynakId, fatura satirini stok hareketine izlenebilir sekilde baglar. */
INSERT INTO dbo.StokHareket
    (UrunId, DepoId, Tarih, Yon, Miktar, KaynakTip, KaynakId, Aciklama)
SELECT s.UrunId,
       f.DepoId,
       DATEADD(HOUR, 10, CONVERT(DATETIME2, f.FaturaTarihi)),
       1,
       s.Miktar,
       3,
       s.Id,
       N'Alış faturası ' + f.FaturaNo
FROM dbo.AlisFaturasiSatir s
JOIN dbo.AlisFaturasi f ON f.Id = s.FaturaId
JOIN dbo.Tedarikci t ON t.Id = f.TedarikciId
WHERE t.Kod LIKE N'TED00[1-5]'
  AND f.FaturaNo LIKE N'DEMO-AF-00[1-6]'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.StokHareket h
      WHERE h.KaynakTip = 3 AND h.KaynakId = s.Id
  );

INSERT INTO dbo.StokParti
    (UrunId, DepoId, StokHareketId, GirisTarihi, GirisMiktari,
     KalanMiktar, BirimMaliyet, Aciklama, SonKullanmaTarihi,
     LotNo, TedarikciId, AlisFaturasiSatirId)
SELECT s.UrunId,
       f.DepoId,
       h.Id,
       h.Tarih,
       s.Miktar,
       s.Miktar,
       s.BirimFiyat,
       N'Alış faturası ' + f.FaturaNo,
       s.SonKullanmaTarihi,
       s.LotNo,
       f.TedarikciId,
       s.Id
FROM dbo.AlisFaturasiSatir s
JOIN dbo.AlisFaturasi f ON f.Id = s.FaturaId
JOIN dbo.Tedarikci t ON t.Id = f.TedarikciId
JOIN dbo.StokHareket h ON h.KaynakTip = 3 AND h.KaynakId = s.Id
WHERE t.Kod LIKE N'TED00[1-5]'
  AND f.FaturaNo LIKE N'DEMO-AF-00[1-6]'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.StokParti p
      WHERE p.AlisFaturasiSatirId = s.Id
  );

UPDATE h
SET h.Aciklama = N'Alış faturası ' + f.FaturaNo
FROM dbo.StokHareket h
JOIN dbo.AlisFaturasiSatir s ON s.Id = h.KaynakId AND h.KaynakTip = 3
JOIN dbo.AlisFaturasi f ON f.Id = s.FaturaId
WHERE f.FaturaNo LIKE N'DEMO-AF-00[1-6]';

UPDATE p
SET p.Aciklama = N'Alış faturası ' + f.FaturaNo
FROM dbo.StokParti p
JOIN dbo.AlisFaturasiSatir s ON s.Id = p.AlisFaturasiSatirId
JOIN dbo.AlisFaturasi f ON f.Id = s.FaturaId
WHERE f.FaturaNo LIKE N'DEMO-AF-00[1-6]';

DROP TABLE #SatirPlan;
DROP TABLE #FaturaPlan;
DROP TABLE #TedarikciPlan;

COMMIT TRANSACTION;
GO

SELECT TedarikciSayisi = COUNT(*)
FROM dbo.Tedarikci
WHERE Kod LIKE N'TED00[1-5]';

SELECT FaturaSayisi = COUNT(*),
       GenelToplam = SUM(GenelToplam)
FROM dbo.AlisFaturasi
WHERE FaturaNo LIKE N'DEMO-AF-00[1-6]';

SELECT FaturaSatiri = COUNT(*),
       StokPartisi = COUNT(p.Id)
FROM dbo.AlisFaturasiSatir s
JOIN dbo.AlisFaturasi f ON f.Id = s.FaturaId
LEFT JOIN dbo.StokParti p ON p.AlisFaturasiSatirId = s.Id
WHERE f.FaturaNo LIKE N'DEMO-AF-00[1-6]';
GO
