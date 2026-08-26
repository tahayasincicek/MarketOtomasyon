/* =========================================================
   Kampanya motoru
   ---------------------------------------------------------
   Uc tablo: kampanya basligi, kosullari ve sonucu.

   Neden kosul/sonuc ayri tablolarda?
   "Sut'te %10 indirim" ile "3 al 2 ode" ve "200 TL ustu %5"
   birbirinden cok farkli kurallar. Her biri icin Kampanya
   tablosuna yeni kolon eklemek yerine, kosul (ne zaman gecerli)
   ve sonuc (ne yapar) ayri satirlar olarak tutulur. Yeni bir
   kampanya tipi eklemek sema degisikligi gerektirmez.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Kampanya basligi ---------- */
IF OBJECT_ID('Kampanya') IS NULL
BEGIN
    CREATE TABLE Kampanya (
        Id                   INT IDENTITY(1,1) PRIMARY KEY,
        Kod                  NVARCHAR(30)  NOT NULL,
        Ad                   NVARCHAR(200) NOT NULL,

        /* Kucuk deger once degerlendirilir. Iki kampanya ayni indirimi
           veriyorsa onceligi kucuk olan kazanir. */
        Oncelik              INT           NOT NULL CONSTRAINT DF_Kmp_Oncelik DEFAULT(100),

        BaslangicTarihi      DATETIME2     NOT NULL CONSTRAINT DF_Kmp_Bas DEFAULT(SYSUTCDATETIME()),
        BitisTarihi          DATETIME2     NULL,
        Aktif                BIT           NOT NULL CONSTRAINT DF_Kmp_Aktif DEFAULT(1),

        /* 0 ise bu kampanya uygulandigi satirda baska kampanya calismaz. */
        DigerleriyleBirlesir BIT           NOT NULL CONSTRAINT DF_Kmp_Birlesir DEFAULT(0),

        OlusturmaTarihi      DATETIME2     NOT NULL CONSTRAINT DF_Kmp_Tarih DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT UQ_Kampanya_Kod UNIQUE (Kod)
    );

    CREATE INDEX IX_Kampanya_Gecerli ON Kampanya (Aktif, BaslangicTarihi, BitisTarihi);
END
GO

/* ---------- Kampanya kosulu: ne zaman gecerli ----------
   Tip 1 (Urun)       : UrunId dolu. MinMiktar varsa "N al M ode"nin N'i.
   Tip 2 (Kategori)   : KategoriId dolu.
   Tip 3 (SepetTutari): MinTutar dolu; sepet geneli icin baraj.        */
IF OBJECT_ID('KampanyaKosul') IS NULL
BEGIN
    CREATE TABLE KampanyaKosul (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        KampanyaId  INT           NOT NULL,
        Tip         TINYINT       NOT NULL,
        UrunId      INT           NULL,
        KategoriId  INT           NULL,
        MinMiktar   DECIMAL(18,4) NULL,
        MinTutar    DECIMAL(18,4) NULL,
        CONSTRAINT FK_KmpKosul_Kampanya FOREIGN KEY (KampanyaId) REFERENCES Kampanya(Id),
        CONSTRAINT FK_KmpKosul_Urun     FOREIGN KEY (UrunId)     REFERENCES Urun(Id),
        CONSTRAINT FK_KmpKosul_Kategori FOREIGN KEY (KategoriId) REFERENCES Kategori(Id),
        CONSTRAINT CK_KmpKosul_Tip CHECK (Tip IN (1, 2, 3))
    );

    CREATE INDEX IX_KmpKosul_Kampanya ON KampanyaKosul (KampanyaId);
END
GO

/* ---------- Kampanya sonucu: ne yapar ----------
   Tip 1 (YuzdeIndirim)  : Yuzde dolu.
   Tip 2 (TutarIndirimi) : Tutar dolu.
   Tip 3 (NAlMOde)       : OdenecekMiktar dolu (M). N kosulda.        */
IF OBJECT_ID('KampanyaSonuc') IS NULL
BEGIN
    CREATE TABLE KampanyaSonuc (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        KampanyaId     INT           NOT NULL,
        Tip            TINYINT       NOT NULL,
        Yuzde          DECIMAL(5,2)  NULL,
        Tutar          DECIMAL(18,4) NULL,
        OdenecekMiktar DECIMAL(18,4) NULL,
        CONSTRAINT FK_KmpSonuc_Kampanya FOREIGN KEY (KampanyaId) REFERENCES Kampanya(Id),
        CONSTRAINT CK_KmpSonuc_Tip CHECK (Tip IN (1, 2, 3))
    );

    CREATE INDEX IX_KmpSonuc_Kampanya ON KampanyaSonuc (KampanyaId);
END
GO

/* ---------- FisSatir.KampanyaId ----------
   Kolon ilk semada zaten var; yalnizca yabanci anahtari simdi
   baglanabiliyor cunku Kampanya tablosu artik mevcut. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FisSatir_Kampanya')
BEGIN
    ALTER TABLE FisSatir
        ADD CONSTRAINT FK_FisSatir_Kampanya FOREIGN KEY (KampanyaId) REFERENCES Kampanya(Id);
END
GO
