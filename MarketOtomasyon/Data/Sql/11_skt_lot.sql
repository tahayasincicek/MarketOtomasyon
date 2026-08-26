/* =========================================================
   Son kullanma tarihi, lot ve FEFO
   ---------------------------------------------------------
   StokParti maliyet ve kalan miktar tutuyordu ama parti bir
   gida marketinde bundan fazlasini tasimali: ne zaman bozulur,
   hangi lottan geldi, kimden alindi.

   Sevk sirasi FIFO'dan FEFO'ya gecer: raftan once son kullanma
   tarihi en yakin parti cikar. Maliyet de o partinin maliyeti
   oldugu icin muhasebe fiziksel akisi takip eder.

   FIFO ve FEFO ayni katmanda degildir: FIFO bir maliyet yontemi,
   FEFO bir sevk kuralidir. Bu projede ikisi tek yerde birlestigi
   icin siralamayi degistirmek her ikisini birden degistirir.

   NULL kurali: son kullanma tarihi olmayan urunler (kirtasiye,
   zuccaciye, tekstil) siranin SONUNA duser. SQL Server'da
   ORDER BY varsayilan olarak NULL'lari BASA koyar; duzeltilmezse
   tarihsiz urunler, yarin bozulacak sutten once tuketilir ve
   sistem sessizce yanlis calisir.

   Tekrar calistirilabilir.
   ========================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1) StokParti kolonlari ----------
   Ucu de NULL kabul eder: mevcut partilerin hicbirinde bu bilgi
   yok ve gecmise donuk uretilemez. */

/* Tarih DATE, DATETIME2 degil: son kullanma bir takvim gunudur,
   saat tasimaz. DATETIME2 olsaydi ayni gunun 00:00 ve 23:59
   degerleri farkli siralanir, ayrica projedeki UTC donusum
   mantigina yanlislikla dahil edilirdi. */
IF COL_LENGTH('dbo.StokParti', 'SonKullanmaTarihi') IS NULL
    ALTER TABLE dbo.StokParti ADD SonKullanmaTarihi DATE NULL;
GO

IF COL_LENGTH('dbo.StokParti', 'LotNo') IS NULL
    ALTER TABLE dbo.StokParti ADD LotNo NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.StokParti', 'TedarikciAdi') IS NULL
    ALTER TABLE dbo.StokParti ADD TedarikciAdi NVARCHAR(150) NULL;
GO

/* ---------- 2) Urun.SonKullanmaZorunlu ----------
   Sut girisinde tarihi yazmayi unutan kasiyer, o partiyi NULL ile
   kaydeder ve NULL'lar sona atildigi icin sut EN SON satilacak
   partiye donusur - tam tersi olmasi gerekirken. Bu bayrak, unutmayi
   mal kabulde engeller.

   "Gida disi = tarihsiz" DEGILDIR: deterjanin, sampuanin, pilin raf
   omru vardir. Ayrim kategori duzeyinde yapilamaz - ayni Temizlik
   kategorisinde deterjanin tarihi var, bulasik sungerinin yok.
   Kategori yalnizca baslangic degeri verir, karar urun bazindadir. */
IF COL_LENGTH('dbo.Urun', 'SonKullanmaZorunlu') IS NULL
    ALTER TABLE dbo.Urun
        ADD SonKullanmaZorunlu BIT NOT NULL
            CONSTRAINT DF_Urun_SktZorunlu DEFAULT(0);
GO

UPDATE u
SET SonKullanmaZorunlu = 1
FROM dbo.Urun u
JOIN dbo.Kategori k ON k.Id = u.KategoriId
WHERE k.Kod IN ('GIDA', 'ICEC', 'KAHV', 'ATIS', 'TEMZ', 'KBAK')
  AND u.SonKullanmaZorunlu = 0;
GO

/* ---------- 3) FEFO index ----------
   Mevcut IX_StokParti_FIFO silinmiyor: zarar vermiyor ve siralama
   kararindan geri donulmek istenirse gerekiyor. */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.StokParti') AND name = 'IX_StokParti_FEFO'
)
    CREATE INDEX IX_StokParti_FEFO
        ON dbo.StokParti (UrunId, DepoId, SonKullanmaTarihi, GirisTarihi, Id)
        INCLUDE (KalanMiktar, BirimMaliyet);
GO

/* Lot geri cagirma sorgusu icin. LotNo'ya UNIQUE KONULMAZ: ayni lot
   birden fazla partide bulunabilir (ayni sevkiyat iki depoya bolunur
   ya da ayni lot iki tarihte gelir). */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.StokParti') AND name = 'IX_StokParti_Lot'
)
    CREATE INDEX IX_StokParti_Lot
        ON dbo.StokParti (LotNo)
        WHERE LotNo IS NOT NULL;
GO

SELECT PartiKolonlari = (SELECT COUNT(*) FROM sys.columns
                         WHERE object_id = OBJECT_ID('dbo.StokParti')
                           AND name IN ('SonKullanmaTarihi','LotNo','TedarikciAdi')),
       SktZorunluUrun = (SELECT COUNT(*) FROM dbo.Urun WHERE SonKullanmaZorunlu = 1),
       ToplamUrun     = (SELECT COUNT(*) FROM dbo.Urun);
GO
