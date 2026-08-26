/* =========================================================
   Tedarikci ve alis faturasi
   ---------------------------------------------------------
   Mal kabul calisiyordu ama kimden alindigi serbest metin
   olarak (StokParti.TedarikciAdi) tutuluyordu: ayni firma
   "Ulker", "ULKER", "Ulker A.S." diye bes kez yazilir ve
   "bu firmadan bu ay ne aldik" sorusu cevaplanamaz.

   Bu betik tedarikci kartini ve cok satirli alis faturasini
   ekler. Fatura mal kabulu DEGISTIRMEZ, SARMALAR: kaydedilince
   her satir icin ayni stok hareketi + FEFO partisi aciliir,
   tumu TEK transaction icinde.

   KAPSAM DISI (bilincli): cari hesap, odeme takibi, siparis ve
   irsaliye. Bunlar eksik degil, kapsam disi - README'de de
   boyle belirtiliyor.

   Tekrar calistirilabilir.
   ========================================================= */

USE MarketOtomasyon;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1) Tedarikci ---------- */
IF OBJECT_ID('Tedarikci') IS NULL
BEGIN
    CREATE TABLE Tedarikci (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        Kod          NVARCHAR(20)  NOT NULL,
        Unvan        NVARCHAR(200) NOT NULL,
        VergiNo      NVARCHAR(11)  NULL,
        VergiDairesi NVARCHAR(100) NULL,
        Telefon      NVARCHAR(20)  NULL,
        Eposta       NVARCHAR(150) NULL,
        Adres        NVARCHAR(300) NULL,
        Aktif        BIT NOT NULL CONSTRAINT DF_Tedarikci_Aktif DEFAULT(1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Tedarikci_Tarih DEFAULT(SYSUTCDATETIME()),

        CONSTRAINT UQ_Tedarikci_Kod UNIQUE (Kod)
    );
END
GO

/* ---------- 2) Alis faturasi basligi ---------- */
IF OBJECT_ID('AlisFaturasi') IS NULL
BEGIN
    CREATE TABLE AlisFaturasi (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        TedarikciId  INT           NOT NULL,
        FaturaNo     NVARCHAR(30)  NOT NULL,
        FaturaTarihi DATE          NOT NULL,
        KayitTarihi  DATETIME2     NOT NULL
            CONSTRAINT DF_AlisFat_Kayit DEFAULT(SYSUTCDATETIME()),
        KullaniciId  INT           NOT NULL,
        DepoId       INT           NOT NULL,
        AraToplam    DECIMAL(18,4) NOT NULL,
        ToplamKdv    DECIMAL(18,4) NOT NULL,
        GenelToplam  DECIMAL(18,4) NOT NULL,
        Aciklama     NVARCHAR(300) NULL,

        CONSTRAINT FK_AlisFat_Tedarikci FOREIGN KEY (TedarikciId) REFERENCES Tedarikci(Id),
        CONSTRAINT FK_AlisFat_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id),
        CONSTRAINT FK_AlisFat_Depo      FOREIGN KEY (DepoId)      REFERENCES Depo(Id),

        -- Ayni tedarikciden ayni fatura iki kez girilemez. Girilirse stok
        -- iki katina cikar; FaturaNo tek basina benzersiz degil cunku
        -- farkli tedarikciler ayni numarayi kullanabilir.
        CONSTRAINT UQ_AlisFat_No UNIQUE (TedarikciId, FaturaNo)
    );
END
GO

/* ---------- 3) Alis faturasi satiri ---------- */
IF OBJECT_ID('AlisFaturasiSatir') IS NULL
BEGIN
    CREATE TABLE AlisFaturasiSatir (
        Id                INT           IDENTITY(1,1) PRIMARY KEY,
        FaturaId          INT           NOT NULL,
        SatirNo           INT           NOT NULL,
        UrunId            INT           NOT NULL,
        Miktar            DECIMAL(18,4) NOT NULL,
        BirimFiyat        DECIMAL(18,4) NOT NULL,   -- KDV HARIC
        KdvOrani          DECIMAL(5,2)  NOT NULL,
        SatirMatrah       DECIMAL(18,4) NOT NULL,
        SatirKdv          DECIMAL(18,4) NOT NULL,
        SonKullanmaTarihi DATE          NULL,
        LotNo             NVARCHAR(50)  NULL,

        CONSTRAINT FK_AlisFatSatir_Fatura FOREIGN KEY (FaturaId) REFERENCES AlisFaturasi(Id),
        CONSTRAINT FK_AlisFatSatir_Urun   FOREIGN KEY (UrunId)   REFERENCES Urun(Id),
        CONSTRAINT UQ_AlisFatSatir UNIQUE (FaturaId, SatirNo),
        CONSTRAINT CK_AlisFatSatir_Miktar CHECK (Miktar > 0),
        CONSTRAINT CK_AlisFatSatir_Fiyat  CHECK (BirimFiyat >= 0)
    );
END
GO

/* ---------- 4) StokParti baglantisi ---------- */
IF COL_LENGTH('dbo.StokParti', 'TedarikciId') IS NULL
    ALTER TABLE dbo.StokParti ADD TedarikciId INT NULL;
GO

IF OBJECT_ID('FK_StokParti_Tedarikci', 'F') IS NULL
    ALTER TABLE dbo.StokParti
        ADD CONSTRAINT FK_StokParti_Tedarikci
            FOREIGN KEY (TedarikciId) REFERENCES Tedarikci(Id);
GO

IF COL_LENGTH('dbo.StokParti', 'AlisFaturasiSatirId') IS NULL
    ALTER TABLE dbo.StokParti ADD AlisFaturasiSatirId INT NULL;
GO

IF OBJECT_ID('FK_StokParti_AlisFaturasiSatir', 'F') IS NULL
    ALTER TABLE dbo.StokParti
        ADD CONSTRAINT FK_StokParti_AlisFaturasiSatir
            FOREIGN KEY (AlisFaturasiSatirId) REFERENCES AlisFaturasiSatir(Id);
GO

/* Serbest metin kaldiriliyor: hicbir partide dolu deger yoktu (asagidaki
   kontrol sorgusuyla dogrulandi), veri kaybi olmadan temiz gecis. */
IF COL_LENGTH('dbo.StokParti', 'TedarikciAdi') IS NOT NULL
    ALTER TABLE dbo.StokParti DROP COLUMN TedarikciAdi;
GO

/* ---------- 5) Indexler ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlisFatura_Tedarikci')
    CREATE INDEX IX_AlisFatura_Tedarikci ON AlisFaturasi (TedarikciId, FaturaTarihi DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlisFatSatir_Urun')
    CREATE INDEX IX_AlisFatSatir_Urun ON AlisFaturasiSatir (UrunId);
GO

SELECT Tablolar = (SELECT COUNT(*) FROM sys.tables
                   WHERE name IN ('Tedarikci','AlisFaturasi','AlisFaturasiSatir')),
       PartiKolonlari = (SELECT COUNT(*) FROM sys.columns
                         WHERE object_id = OBJECT_ID('dbo.StokParti')
                           AND name IN ('TedarikciId','AlisFaturasiSatirId')),
       EskiKolonKaldi = (SELECT COUNT(*) FROM sys.columns
                         WHERE object_id = OBJECT_ID('dbo.StokParti')
                           AND name = 'TedarikciAdi');
GO
