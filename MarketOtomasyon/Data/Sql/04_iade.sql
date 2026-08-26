/* =========================================================
   Iade
   ---------------------------------------------------------
   Tamamlanmis bir satisin tamami yerine secilen satir ve
   miktarlar iade edilebilir. Para iadesi Iade basliginda,
   urun/fiyat anlik goruntusu IadeSatir'da tutulur.

   FisSatir.IadeEdilenMiktar toplam iade miktarini saklar.
   Stok girisi StokHareket'te KaynakTip 2 ile yazilir.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('IadeNoSeq') IS NULL
BEGIN
    CREATE SEQUENCE IadeNoSeq AS INT START WITH 1 INCREMENT BY 1;
END
GO

IF OBJECT_ID('Iade') IS NULL
BEGIN
    CREATE TABLE Iade (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        IadeNo       NVARCHAR(20)  NOT NULL,
        FisId        INT           NOT NULL,
        KullaniciId  INT           NOT NULL,
        Tarih        DATETIME2     NOT NULL CONSTRAINT DF_Iade_Tarih DEFAULT(SYSUTCDATETIME()),
        ToplamTutar  DECIMAL(18,4) NOT NULL,
        OdemeTipi    TINYINT       NOT NULL, -- 1: nakit, 2: kart, 3: puan
        Aciklama     NVARCHAR(300) NULL,
        CONSTRAINT UQ_Iade_No UNIQUE (IadeNo),
        CONSTRAINT FK_Iade_Fis FOREIGN KEY (FisId) REFERENCES Fis(Id),
        CONSTRAINT FK_Iade_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id),
        CONSTRAINT CK_Iade_Tutar CHECK (ToplamTutar >= 0),
        CONSTRAINT CK_Iade_OdemeTipi CHECK (OdemeTipi IN (1, 2, 3))
    );

    CREATE INDEX IX_Iade_Fis ON Iade (FisId, Tarih);
END
GO

IF OBJECT_ID('IadeSatir') IS NULL
BEGIN
    CREATE TABLE IadeSatir (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        IadeId          INT           NOT NULL,
        FisSatirId      INT           NOT NULL,
        UrunId          INT           NOT NULL,
        Miktar          DECIMAL(18,4) NOT NULL,
        BirimFiyat      DECIMAL(18,4) NOT NULL,
        IndirimTutari   DECIMAL(18,4) NOT NULL CONSTRAINT DF_IadeSatir_Ind DEFAULT(0),
        KdvOrani        DECIMAL(5,2)  NOT NULL,
        Tutar           DECIMAL(18,4) NOT NULL,
        CONSTRAINT FK_IadeSatir_Iade FOREIGN KEY (IadeId) REFERENCES Iade(Id),
        CONSTRAINT FK_IadeSatir_FisSatir FOREIGN KEY (FisSatirId) REFERENCES FisSatir(Id),
        CONSTRAINT FK_IadeSatir_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
        CONSTRAINT CK_IadeSatir_Miktar CHECK (Miktar > 0),
        CONSTRAINT CK_IadeSatir_Tutar CHECK (Tutar >= 0)
    );

    CREATE INDEX IX_IadeSatir_FisSatir ON IadeSatir (FisSatirId);
END
GO
