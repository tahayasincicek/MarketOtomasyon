/* =========================================================
   Demo verisi - son 30 gunun satis gecmisi
   ---------------------------------------------------------
   20_ornek_veri.sql urun kartlarini ve acilis stogunu kurar,
   ama hic satis uretmez. Rapor, kar marji ve Z raporu
   ekranlari gecmis satis olmadan bos gorunur. Bu betik o
   gecmisi uretir.

   Uretilen kayitlar:
     - her gun icin bir vardiya (bugunki haric hepsi kapali)
     - gunde 8-25 fis, hafta sonu daha yogun
     - saat dagilimi ogle ve aksam tepe yapar
     - fis basina 1-8 satir, temel gidalar daha sik secilir
     - nakit/kart odemeler, nakitte para ustu
     - fislerin kucuk bir kismi iade edilir
     - her satis icin stok cikisi ve FIFO parti tuketimi

   URETIMDE CALISTIRILMAZ. Sahte satis kaydi uretir; gercek
   bir markette ciro raporlarini bozar.

   Tekrar calistirilabilir: urettigi her kayit DEMO onekiyle
   isaretlenir, zaten varsa hicbir sey yapmaz. Yeniden uretmek
   icin asagidaki @Temizle degiskenini 1 yap.
   ========================================================= */

USE MarketOtomasyon;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* StokPartiTuketim'in hesaplanan kolonu ve StokParti'nin filtreli
   index'i bu iki ayari zorunlu kilar. sqlcmd ikisini de varsayilan
   olarak KAPALI baslatir; ayarlanmazsa ilk yazmada hata alinir. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Temizle   BIT = 0;          -- 1: once mevcut demo verisini sil
DECLARE @GunSayisi INT = 30;
DECLARE @Tohum     INT = 20260825;   -- ayni tohum ayni veriyi uretir

/* Rastgelelik hakkinda:

   Bu betikte rastgele sayi HASHBYTES('MD5', ...) ile uretilir:

       (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(...)), 1, 4)) & 2147483647) % n

   Ilk akla gelen CHECKSUM(@Tohum, a, b) KULLANILMAZ. CHECKSUM kucuk
   tamsayilarda XOR tabanli calisir ve dusuk bitleri neredeyse sabit
   kalir; "% 8" gibi ikinin kuvvetine bolununce her satir icin ayni
   sonucu verir. Ilk surumde bu yuzden her fis tam 8 satir cikmisti.

   & 2147483647 maskesi ABS() yerine kullaniliyor: ABS(-2147483648)
   tasma hatasi verir, maske vermez.                                  */

/* ---------------------------------------------------------
   0) Temizlik
   Silme sirasi yabanci anahtarlarin tersi olmali.
   --------------------------------------------------------- */
IF @Temizle = 1
BEGIN
    DECLARE @DemoFis TABLE (Id INT PRIMARY KEY);
    INSERT INTO @DemoFis SELECT Id FROM Fis WHERE FisNo LIKE 'DEMO-%';

    DECLARE @DemoVardiya TABLE (Id INT PRIMARY KEY);
    INSERT INTO @DemoVardiya
    SELECT DISTINCT VardiyaId FROM Fis WHERE Id IN (SELECT Id FROM @DemoFis);

    DELETE t FROM StokPartiTuketim t
    JOIN StokHareket h ON h.Id = t.StokHareketId
    WHERE h.Aciklama LIKE 'DEMO %';

    DELETE FROM IadeSatir  WHERE IadeId IN (SELECT Id FROM Iade WHERE IadeNo LIKE 'DEMO-%');
    DELETE FROM Iade       WHERE IadeNo LIKE 'DEMO-%';
    DELETE FROM Odeme      WHERE FisId IN (SELECT Id FROM @DemoFis);
    DELETE FROM FisSatir   WHERE FisId IN (SELECT Id FROM @DemoFis);
    DELETE FROM Fis        WHERE Id    IN (SELECT Id FROM @DemoFis);

    DELETE FROM StokParti   WHERE Aciklama LIKE 'DEMO %';
    DELETE FROM StokHareket WHERE Aciklama LIKE 'DEMO %';
    DELETE FROM Vardiya     WHERE Id IN (SELECT Id FROM @DemoVardiya)
                              AND NOT EXISTS (SELECT 1 FROM Fis f WHERE f.VardiyaId = Vardiya.Id);

    PRINT 'Mevcut demo verisi silindi.';
END;

IF EXISTS (SELECT 1 FROM Fis WHERE FisNo LIKE 'DEMO-%')
BEGIN
    PRINT 'Demo verisi zaten mevcut, hicbir sey yapilmadi.';
    PRINT 'Yeniden uretmek icin betigin basindaki @Temizle degiskenini 1 yapin.';
    RETURN;
END;

/* ---------------------------------------------------------
   1) On kosullar
   --------------------------------------------------------- */
DECLARE @KasiyerId INT = (SELECT Id FROM Kullanici WHERE KullaniciAdi = 'kasiyer1');
DECLARE @DepoId    INT = (SELECT Id FROM Depo      WHERE Kod = 'MRK');

IF @KasiyerId IS NULL OR @DepoId IS NULL
BEGIN
    RAISERROR('Once 20_ornek_veri.sql calistirilmali: kasiyer1 kullanicisi veya MRK deposu bulunamadi.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM Urun u JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id WHERE u.Aktif = 1)
BEGIN
    RAISERROR('Fiyati tanimli aktif urun yok. Once 20_ornek_veri.sql calistirilmali.', 16, 1);
    RETURN;
END;

/* Uretim tek transaction'da. SET XACT_ABORT ON acik oldugu icin
   herhangi bir adim hata verirse tamami geri alinir: yarim satis
   gecmisi, satirsiz fis veya partisi olmayan stok hareketi kalmaz. */
BEGIN TRANSACTION;

/* Sayi tablosu: 0..199 */
SELECT TOP (200) n = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1
INTO #N
FROM sys.all_objects;

/* ---------------------------------------------------------
   2) Gunler ve vardiyalar

   Tarih kolonlari UTC saklanir. Asagidaki gun ve saatler
   YEREL kurgulanip UTC'ye cevrilir; boylece raporlarin saat
   kirilimi gercek market trafigine benzer.
   --------------------------------------------------------- */
DECLARE @BugunYerel DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Turkey Standard Time' AS DATE);

SELECT GunNo   = n.n,
       Tarih   = DATEADD(DAY, -(@GunSayisi - 1 - n.n), @BugunYerel),
       FisAdedi = 0,
       VardiyaId = CAST(NULL AS INT)
INTO #Gun
FROM #N n
WHERE n.n < @GunSayisi;

/* Hafta sonu daha yogun. DATEPART(WEEKDAY) sunucunun DATEFIRST
   ayarina bagli oldugu icin sabit bir referans gunden sayiyoruz:
   1900-01-01 pazartesidir, kalan 5 cumartesi 6 pazar demektir. */
UPDATE #Gun
SET FisAdedi = 8
             + (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':gun:', GunNo)), 1, 4)) & 2147483647) % 10
             + CASE WHEN (DATEDIFF(DAY, '19000101', Tarih) % 7) IN (5, 6) THEN 7 ELSE 0 END;

CREATE TABLE #VardiyaEsle (GunNo INT PRIMARY KEY, VardiyaId INT);

/* MERGE kullaniliyor cunku OUTPUT yan tumcesi yalnizca MERGE'de
   kaynak tablonun kolonlarina (GunNo) erisebilir; duz INSERT ile
   uretilen Id'yi hangi gune ait oldugu bilgisiyle eslestiremezdik. */
MERGE Vardiya AS h
USING #Gun AS g ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (KullaniciId, AcilisTarihi, AcilisTutari, Durum)
    VALUES (@KasiyerId,
            CAST(DATEADD(HOUR, 8, CAST(g.Tarih AS DATETIME2))
                 AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS DATETIME2),
            500,
            CASE WHEN g.GunNo = @GunSayisi - 1 THEN 1 ELSE 2 END)
OUTPUT inserted.Id, g.GunNo INTO #VardiyaEsle (VardiyaId, GunNo);

UPDATE g SET VardiyaId = v.VardiyaId
FROM #Gun g JOIN #VardiyaEsle v ON v.GunNo = g.GunNo;

/* ---------------------------------------------------------
   3) Fisler

   Saat havuzu: her saat listede kac kez geciyorsa o kadar
   olasi. Ogle (12-13) ve aksam (17-19) tepe yapar.
   --------------------------------------------------------- */
CREATE TABLE #SaatHavuz (Idx INT IDENTITY(1,1) PRIMARY KEY, Saat INT);
INSERT INTO #SaatHavuz (Saat) VALUES
    (8),(9),(9),(10),(10),(11),(11),(12),(12),(12),(13),(13),
    (14),(15),(16),(17),(17),(18),(18),(18),(19),(19),(19),(20),(20),(21);

DECLARE @SaatAdedi INT = (SELECT COUNT(*) FROM #SaatHavuz);

SELECT FisSira  = ROW_NUMBER() OVER (ORDER BY g.GunNo, n.n),
       g.GunNo,
       g.Tarih,
       g.VardiyaId,
       SiraNo    = n.n,
       Saat      = s.Saat,
       Dakika    = (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':dk:', g.GunNo, ':', n.n)), 1, 4)) & 2147483647) % 60,
       /* Sepet buyuklugu: cogu musteri birkac urun alir, kalabalik
          alisveris seyrektir. 1-6 satir bu dagilimi verir. */
       SatirAdedi = 1 + (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':sat:', g.GunNo, ':', n.n)), 1, 4)) & 2147483647) % 6,
       FisId     = CAST(NULL AS INT)
INTO #FisTaslak
FROM #Gun g
JOIN #N n ON n.n < g.FisAdedi
JOIN #SaatHavuz s ON s.Idx = 1 + (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':saat:', g.GunNo, ':', n.n)), 1, 4)) & 2147483647) % @SaatAdedi;

ALTER TABLE #FisTaslak ADD TarihUtc DATETIME2;

UPDATE #FisTaslak
SET TarihUtc = CAST(DATEADD(MINUTE, Dakika, DATEADD(HOUR, Saat, CAST(Tarih AS DATETIME2)))
                    AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS DATETIME2);

CREATE TABLE #FisEsle (FisSira INT PRIMARY KEY, FisId INT);

MERGE Fis AS h
USING #FisTaslak AS t ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (FisNo, VardiyaId, KullaniciId, Tarih, AraToplam, ToplamIndirim,
            ToplamKdv, GenelToplam, Durum, Askida)
    VALUES ('DEMO-' + RIGHT('000000' + CAST(t.FisSira AS VARCHAR(10)), 6),
            t.VardiyaId, @KasiyerId, t.TarihUtc, 0, 0, 0, 0, 2, 0)
OUTPUT inserted.Id, t.FisSira INTO #FisEsle (FisId, FisSira);

UPDATE t SET FisId = e.FisId
FROM #FisTaslak t JOIN #FisEsle e ON e.FisSira = t.FisSira;

/* ---------------------------------------------------------
   4) Fis satirlari

   Urun havuzu agirlikli: temel gidalar listede dort kez gecer,
   digerleri bir kez. Boylece ekmek ve sut cogu fiste cikar.
   --------------------------------------------------------- */
SELECT u.Id AS UrunId, u.Kod, u.Birim, u.KdvOrani, u.Tartili, gf.Fiyat
INTO #Urun
FROM Urun u
JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
WHERE u.Aktif = 1;

SELECT Idx = ROW_NUMBER() OVER (ORDER BY u.UrunId, k.n),
       u.UrunId, u.KdvOrani, u.Tartili, u.Fiyat
INTO #UrunHavuz
FROM #Urun u
JOIN #N k ON k.n < CASE WHEN u.Kod IN ('URN001','URN004','URN010','URN017',
                                       'URN018','URN020','URN021','URN022')
                        THEN 4 ELSE 1 END;

DECLARE @HavuzAdedi INT = (SELECT COUNT(*) FROM #UrunHavuz);

/* Ayni urun bir fiste iki kez cikarsa ikincisi elenir: kasiyer
   ayni urunu tekrar okutunca yeni satir acilmaz, miktar artar. */
SELECT t.FisId,
       h.UrunId,
       h.KdvOrani,
       h.Fiyat,
       /* Tartili urunlerde 0,2-2,1 kg. Adetli urunlerde musteri
          cogunlukla bir tane alir; ikiden fazlasi seyrektir. */
       Miktar = CASE
                  WHEN h.Tartili = 1 THEN CONVERT(DECIMAL(18,4),
                       0.2 + ((CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':kg:', t.FisSira, ':', n.n)), 1, 4)) & 2147483647) % 20) / 10.0)
                  WHEN (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':ad:', t.FisSira, ':', n.n)), 1, 4)) & 2147483647) % 10 < 7
                       THEN CONVERT(DECIMAL(18,4), 1)
                  ELSE CONVERT(DECIMAL(18,4),
                       2 + (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':ad2:', t.FisSira, ':', n.n)), 1, 4)) & 2147483647) % 2)
                END,
       Tekrar = ROW_NUMBER() OVER (PARTITION BY t.FisId, h.UrunId ORDER BY n.n)
INTO #SatirTaslak
FROM #FisTaslak t
JOIN #N n ON n.n < t.SatirAdedi
JOIN #UrunHavuz h ON h.Idx = 1 + (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':urun:', t.FisSira, ':', n.n)), 1, 4)) & 2147483647) % @HavuzAdedi;

DELETE FROM #SatirTaslak WHERE Tekrar > 1;

SELECT FisId,
       SatirNo = ROW_NUMBER() OVER (PARTITION BY FisId ORDER BY UrunId),
       UrunId,
       Miktar,
       BirimFiyat = Fiyat,
       KdvOrani,
       SatirToplam = CONVERT(DECIMAL(18,4), ROUND(Miktar * Fiyat, 2)),
       FisSatirId = CAST(NULL AS INT)
INTO #Satir
FROM #SatirTaslak;

CREATE TABLE #SatirEsle (FisId INT, SatirNo INT, FisSatirId INT, PRIMARY KEY (FisId, SatirNo));

MERGE FisSatir AS h
USING #Satir AS s ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (FisId, SatirNo, UrunId, Miktar, BirimFiyat, IndirimTutari,
            KdvOrani, SatirToplam, IadeEdilenMiktar)
    VALUES (s.FisId, s.SatirNo, s.UrunId, s.Miktar, s.BirimFiyat, 0,
            s.KdvOrani, s.SatirToplam, 0)
OUTPUT inserted.Id, s.FisId, s.SatirNo INTO #SatirEsle (FisSatirId, FisId, SatirNo);

UPDATE s SET FisSatirId = e.FisSatirId
FROM #Satir s JOIN #SatirEsle e ON e.FisId = s.FisId AND e.SatirNo = s.SatirNo;

/* Satiri olmayan fis kalmasin (ROW_NUMBER eleme sonrasi olabilir). */
DELETE FROM Fis
WHERE FisNo LIKE 'DEMO-%'
  AND NOT EXISTS (SELECT 1 FROM FisSatir fs WHERE fs.FisId = Fis.Id);

DELETE FROM #FisTaslak
WHERE NOT EXISTS (SELECT 1 FROM Fis f WHERE f.Id = #FisTaslak.FisId);

/* ---------------------------------------------------------
   5) Fis toplamlari

   Fiyat KDV DAHILDIR (SepetHesaplayici ile ayni model):
   KDV tutarin uzerine eklenmez, icinden ayristirilir.
   Ayristirma KDV orani grubu bazinda yapilir; satir satir
   yuvarlayip toplamak fisin oran dokumuyle bir kurus sapardi.
   --------------------------------------------------------- */
;WITH Grup AS (
    SELECT FisId, KdvOrani,
           GrupToplam = SUM(SatirToplam)
    FROM #Satir
    GROUP BY FisId, KdvOrani
),
FisToplam AS (
    SELECT FisId,
           Toplam = SUM(GrupToplam),
           Kdv    = SUM(ROUND(GrupToplam - GrupToplam / (1 + KdvOrani / 100.0), 2))
    FROM Grup
    GROUP BY FisId
)
UPDATE f
SET GenelToplam   = ft.Toplam,
    ToplamKdv     = ft.Kdv,
    AraToplam     = ft.Toplam - ft.Kdv,
    ToplamIndirim = 0
FROM Fis f
JOIN FisToplam ft ON ft.FisId = f.Id;

/* ---------------------------------------------------------
   6) Odemeler
   Nakitte musteri genelde bes liranin katini uzatir.
   --------------------------------------------------------- */
INSERT INTO Odeme (FisId, Tip, Tutar, AlinanTutar, ParaUstu, OnayKodu, Tarih)
SELECT f.Id,
       d.Tip,
       f.GenelToplam,
       CASE WHEN d.Tip = 1 THEN CEILING(f.GenelToplam / 5.0) * 5 END,
       CASE WHEN d.Tip = 1 THEN CEILING(f.GenelToplam / 5.0) * 5 - f.GenelToplam END,
       CASE WHEN d.Tip = 2
            THEN RIGHT('000000' + CAST((CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':onay:', t.FisSira)), 1, 4)) & 2147483647) % 1000000 AS VARCHAR(10)), 6)
       END,
       f.Tarih
FROM #FisTaslak t
JOIN Fis f ON f.Id = t.FisId
CROSS APPLY (SELECT Tip = CASE WHEN (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':odeme:', t.FisSira)), 1, 4)) & 2147483647) % 100 < 55
                               THEN 1 ELSE 2 END) d
WHERE f.GenelToplam > 0;

/* ---------------------------------------------------------
   7) Mal kabul: satisin dayandigi stok

   Satislar var olan bakiyeyi eksiye dusurmesin diye once tek
   bir demo mal kabulu yazilir. Tarihi ilk satis gununden bir
   gun oncedir; FIFO'da bu parti once tukenir.

   Birim maliyet, KDV haric satis fiyatinin %75'i alinarak
   uretilir. Boylece kar marji ekrani makul bir yuzde gosterir.
   --------------------------------------------------------- */
SELECT s.UrunId,
       SatisMiktari = SUM(s.Miktar),
       BirimMaliyet = CONVERT(DECIMAL(18,4),
                      ROUND(MAX(s.BirimFiyat) / (1 + MAX(s.KdvOrani) / 100.0) * 0.75, 4))
INTO #MalKabul
FROM #Satir s
GROUP BY s.UrunId;

DECLARE @MalKabulTarihi DATETIME2 =
    CAST(DATEADD(HOUR, 7, CAST(DATEADD(DAY, -@GunSayisi, @BugunYerel) AS DATETIME2))
         AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS DATETIME2);

CREATE TABLE #MalKabulEsle (UrunId INT PRIMARY KEY, HareketId BIGINT);

MERGE StokHareket AS h
USING #MalKabul AS m ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (UrunId, DepoId, Tarih, Yon, Miktar, KaynakTip, KaynakId, Aciklama)
    VALUES (m.UrunId, @DepoId, @MalKabulTarihi, 1, m.SatisMiktari * 2, 3, NULL,
            N'DEMO mal kabul')
OUTPUT inserted.Id, m.UrunId INTO #MalKabulEsle (HareketId, UrunId);

CREATE TABLE #PartiEsle (UrunId INT PRIMARY KEY, PartiId BIGINT);

MERGE StokParti AS h
USING (SELECT m.UrunId, m.SatisMiktari, m.BirimMaliyet, e.HareketId
       FROM #MalKabul m JOIN #MalKabulEsle e ON e.UrunId = m.UrunId) AS m ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (UrunId, DepoId, StokHareketId, GirisTarihi, GirisMiktari,
            KalanMiktar, BirimMaliyet, Aciklama)
    VALUES (m.UrunId, @DepoId, m.HareketId, @MalKabulTarihi, m.SatisMiktari * 2,
            m.SatisMiktari * 2, m.BirimMaliyet, N'DEMO mal kabul partisi')
OUTPUT inserted.Id, m.UrunId INTO #PartiEsle (PartiId, UrunId);

/* ---------------------------------------------------------
   8) Satis stok cikislari ve FIFO parti tuketimi
   --------------------------------------------------------- */
CREATE TABLE #CikisEsle (FisSatirId INT PRIMARY KEY, HareketId BIGINT);

MERGE StokHareket AS h
USING (SELECT s.FisSatirId, s.UrunId, s.Miktar, f.Tarih, f.Id AS FisId
       FROM #Satir s JOIN Fis f ON f.Id = s.FisId) AS s ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (UrunId, DepoId, Tarih, Yon, Miktar, KaynakTip, KaynakId, Aciklama)
    VALUES (s.UrunId, @DepoId, s.Tarih, 2, s.Miktar, 1, s.FisId, N'DEMO satis')
OUTPUT inserted.Id, s.FisSatirId INTO #CikisEsle (HareketId, FisSatirId);

INSERT INTO StokPartiTuketim (StokPartiId, StokHareketId, FisSatirId, Miktar, BirimMaliyet, Tarih)
SELECT p.PartiId, c.HareketId, s.FisSatirId, s.Miktar, m.BirimMaliyet, f.Tarih
FROM #Satir s
JOIN #CikisEsle c  ON c.FisSatirId = s.FisSatirId
JOIN #PartiEsle p  ON p.UrunId = s.UrunId
JOIN #MalKabul m   ON m.UrunId = s.UrunId
JOIN Fis f         ON f.Id = s.FisId;

/* Tuketilen miktar partiden dusulur; KalanMiktar kisiti bunu bekler. */
UPDATE sp
SET KalanMiktar = sp.KalanMiktar - t.Tuketilen
FROM StokParti sp
JOIN #PartiEsle p ON p.PartiId = sp.Id
JOIN (SELECT UrunId, Tuketilen = SUM(Miktar) FROM #Satir GROUP BY UrunId) t
     ON t.UrunId = p.UrunId;

/* ---------------------------------------------------------
   9) Iadeler

   Fislerin yaklasik %4'u. Yalnizca son 20 gun icinden secilir:
   30 gunluk iade suresinin sinirinda kalan fisler demo verisini
   gereksiz yere kenar duruma sokardi.

   Her iadede fisin ilk satiri tamamen iade edilir.
   --------------------------------------------------------- */
SELECT IadeSira = ROW_NUMBER() OVER (ORDER BY t.FisSira),
       t.FisSira,
       t.FisId,
       t.VardiyaId,
       s.FisSatirId,
       s.UrunId,
       s.Miktar,
       s.BirimFiyat,
       s.KdvOrani,
       s.SatirToplam,
       f.Tarih
INTO #Iade
FROM #FisTaslak t
JOIN Fis f ON f.Id = t.FisId
CROSS APPLY (SELECT TOP (1) * FROM #Satir x WHERE x.FisId = t.FisId ORDER BY x.SatirNo) s
WHERE t.GunNo >= @GunSayisi - 20
  AND (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':iade:', t.FisSira)), 1, 4)) & 2147483647) % 100 < 4;

/* Iade, iade edildigi gunun vardiyasina baglanir; satisin
   vardiyasina degil. Demo'da ikisi ayni gun oldugu icin ayni
   vardiya kullaniliyor. */
CREATE TABLE #IadeEsle (IadeSira INT PRIMARY KEY, IadeId INT);

MERGE Iade AS h
USING #Iade AS i ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (IadeNo, FisId, KullaniciId, VardiyaId, Tarih, ToplamTutar, OdemeTipi, Aciklama)
    VALUES ('DEMO-' + RIGHT('00000' + CAST(i.IadeSira AS VARCHAR(10)), 5),
            i.FisId, @KasiyerId, i.VardiyaId,
            DATEADD(HOUR, 2, i.Tarih), i.SatirToplam, 1, N'DEMO iade')
OUTPUT inserted.Id, i.IadeSira INTO #IadeEsle (IadeId, IadeSira);

INSERT INTO IadeSatir (IadeId, FisSatirId, UrunId, Miktar, BirimFiyat,
                       IndirimTutari, KdvOrani, Tutar)
SELECT e.IadeId, i.FisSatirId, i.UrunId, i.Miktar, i.BirimFiyat, 0, i.KdvOrani, i.SatirToplam
FROM #Iade i JOIN #IadeEsle e ON e.IadeSira = i.IadeSira;

UPDATE fs
SET IadeEdilenMiktar = i.Miktar
FROM FisSatir fs
JOIN #Iade i ON i.FisSatirId = fs.Id;

/* Iade edilen mal rafa geri doner: stok girisi ve yeni FIFO partisi.
   Parti maliyeti satis anindaki KDV haric tutardan tahmin edilir;
   IadeService de ayni varsayimi kullanir. */
CREATE TABLE #IadeHareket (IadeSira INT PRIMARY KEY, HareketId BIGINT);

MERGE StokHareket AS h
USING (SELECT i.IadeSira, i.UrunId, i.Miktar, i.Tarih, e.IadeId
       FROM #Iade i JOIN #IadeEsle e ON e.IadeSira = i.IadeSira) AS i ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (UrunId, DepoId, Tarih, Yon, Miktar, KaynakTip, KaynakId, Aciklama)
    VALUES (i.UrunId, @DepoId, DATEADD(HOUR, 2, i.Tarih), 1, i.Miktar, 2, i.IadeId,
            N'DEMO iade girisi')
OUTPUT inserted.Id, i.IadeSira INTO #IadeHareket (HareketId, IadeSira);

INSERT INTO StokParti (UrunId, DepoId, StokHareketId, GirisTarihi, GirisMiktari,
                       KalanMiktar, BirimMaliyet, Aciklama)
SELECT i.UrunId, @DepoId, h.HareketId, DATEADD(HOUR, 2, i.Tarih), i.Miktar, i.Miktar,
       CONVERT(DECIMAL(18,4), ROUND(i.BirimFiyat / (1 + i.KdvOrani / 100.0), 4)),
       N'DEMO iade partisi'
FROM #Iade i JOIN #IadeHareket h ON h.IadeSira = i.IadeSira;

/* ---------------------------------------------------------
   10) Vardiya kapanisi

   Beklenen kasa = acilis + nakit satis - nakit iade.
   Sayilan tutar gunlerin cogunda beklenenle ayni; birkac gun
   kucuk bir fark birakilir ki Z raporunun fark sutunu bos
   kalmasin.
   --------------------------------------------------------- */
;WITH NakitSatis AS (
    SELECT f.VardiyaId, Tutar = SUM(o.Tutar)
    FROM Odeme o
    JOIN Fis f ON f.Id = o.FisId
    WHERE o.Tip = 1 AND f.FisNo LIKE 'DEMO-%'
    GROUP BY f.VardiyaId
),
NakitIade AS (
    SELECT i.VardiyaId, Tutar = SUM(i.ToplamTutar)
    FROM Iade i
    WHERE i.OdemeTipi = 1 AND i.IadeNo LIKE 'DEMO-%'
    GROUP BY i.VardiyaId
)
UPDATE v
SET BeklenenTutar = v.AcilisTutari + ISNULL(ns.Tutar, 0) - ISNULL(ni.Tutar, 0),
    SayilanTutar  = v.AcilisTutari + ISNULL(ns.Tutar, 0) - ISNULL(ni.Tutar, 0) + s.Fark,
    Fark          = s.Fark,
    KapanisTarihi = CAST(DATEADD(HOUR, 21, CAST(g.Tarih AS DATETIME2))
                         AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC' AS DATETIME2)
FROM Vardiya v
JOIN #Gun g ON g.VardiyaId = v.Id
LEFT JOIN NakitSatis ns ON ns.VardiyaId = v.Id
LEFT JOIN NakitIade  ni ON ni.VardiyaId = v.Id
/* Gunlerin bes'te birinde kasa tutmaz; kalanlarda fark sifirdir.
   Hep sifir olsaydi Z raporunun fark sutunu hic denenmemis olurdu. */
CROSS APPLY (SELECT Fark = CASE
    WHEN (CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':kasa:', g.GunNo)), 1, 4)) & 2147483647) % 5 = 0
    THEN ((CONVERT(INT, SUBSTRING(HASHBYTES('MD5', CONCAT(@Tohum, ':fark:', g.GunNo)), 1, 4)) & 2147483647) % 41) - 20
    ELSE 0 END) s
WHERE g.GunNo < @GunSayisi - 1;   -- bugunku vardiya acik kalir

COMMIT TRANSACTION;

/* ---------------------------------------------------------
   11) Ozet
   --------------------------------------------------------- */
SELECT Uretilen = 'Vardiya', Adet = COUNT(*) FROM #Gun
UNION ALL SELECT 'Fis',        COUNT(*) FROM Fis      WHERE FisNo  LIKE 'DEMO-%'
UNION ALL SELECT 'Fis satiri', COUNT(*) FROM FisSatir WHERE FisId IN (SELECT Id FROM Fis WHERE FisNo LIKE 'DEMO-%')
UNION ALL SELECT 'Odeme',      COUNT(*) FROM Odeme    WHERE FisId IN (SELECT Id FROM Fis WHERE FisNo LIKE 'DEMO-%')
UNION ALL SELECT 'Iade',       COUNT(*) FROM Iade     WHERE IadeNo LIKE 'DEMO-%';

SELECT ToplamCiro = SUM(GenelToplam),
       FisSayisi  = COUNT(*),
       OrtSepet   = CONVERT(DECIMAL(18,2), AVG(GenelToplam)),
       IlkGun     = CAST(MIN(Tarih) AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS DATE),
       SonGun     = CAST(MAX(Tarih) AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time' AS DATE)
FROM Fis
WHERE FisNo LIKE 'DEMO-%' AND Durum = 2;

DROP TABLE #N, #Gun, #VardiyaEsle, #SaatHavuz, #FisTaslak, #FisEsle,
           #Urun, #UrunHavuz, #SatirTaslak, #Satir, #SatirEsle,
           #MalKabul, #MalKabulEsle, #PartiEsle, #CikisEsle,
           #Iade, #IadeEsle, #IadeHareket;
GO
