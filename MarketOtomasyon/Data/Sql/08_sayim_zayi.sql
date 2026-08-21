/* =========================================================
   Gun 16 - Sayim ve zayi

   Sayim basligi ile sayim anindaki sistem miktari, fiziksel
   sayilan miktar ve aradaki fark saklanir. Fark sifir degilse
   StokHareket tablosuna KaynakTip = 4 hareketi yazilir.

   Zayi/fire kaydi sebebiyle birlikte saklanir ve ayni
   transaction icinde KaynakTip = 5 stok cikisi olusturulur.
   ========================================================= */

USE MarketOtomasyon;
GO

IF OBJECT_ID('Sayim', 'U') IS NULL
BEGIN
    CREATE TABLE Sayim (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        DepoId       INT           NOT NULL,
        KullaniciId  INT           NOT NULL,
        Tarih        DATETIME2     NOT NULL CONSTRAINT DF_Sayim_Tarih DEFAULT(SYSUTCDATETIME()),
        Aciklama     NVARCHAR(200) NULL,
        CONSTRAINT FK_Sayim_Depo FOREIGN KEY (DepoId) REFERENCES Depo(Id),
        CONSTRAINT FK_Sayim_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id)
    );

    CREATE INDEX IX_Sayim_Tarih ON Sayim (Tarih DESC);
END;
GO

IF OBJECT_ID('SayimSatir', 'U') IS NULL
BEGIN
    CREATE TABLE SayimSatir (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        SayimId         INT           NOT NULL,
        UrunId          INT           NOT NULL,
        SistemMiktari   DECIMAL(18,4) NOT NULL,
        SayilanMiktar   DECIMAL(18,4) NOT NULL,
        Fark            DECIMAL(18,4) NOT NULL,
        CONSTRAINT FK_SayimSatir_Sayim FOREIGN KEY (SayimId) REFERENCES Sayim(Id),
        CONSTRAINT FK_SayimSatir_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
        CONSTRAINT UQ_SayimSatir UNIQUE (SayimId, UrunId),
        CONSTRAINT CK_SayimSatir_Sayilan CHECK (SayilanMiktar >= 0),
        CONSTRAINT CK_SayimSatir_Fark CHECK (Fark = SayilanMiktar - SistemMiktari)
    );
END;
GO

IF OBJECT_ID('Zayi', 'U') IS NULL
BEGIN
    CREATE TABLE Zayi (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        UrunId       INT           NOT NULL,
        DepoId       INT           NOT NULL,
        KullaniciId  INT           NOT NULL,
        Tarih        DATETIME2     NOT NULL CONSTRAINT DF_Zayi_Tarih DEFAULT(SYSUTCDATETIME()),
        Miktar       DECIMAL(18,4) NOT NULL,
        Sebep        NVARCHAR(200) NOT NULL,
        CONSTRAINT FK_Zayi_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
        CONSTRAINT FK_Zayi_Depo FOREIGN KEY (DepoId) REFERENCES Depo(Id),
        CONSTRAINT FK_Zayi_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id),
        CONSTRAINT CK_Zayi_Miktar CHECK (Miktar > 0),
        CONSTRAINT CK_Zayi_Sebep CHECK (LEN(LTRIM(RTRIM(Sebep))) > 0)
    );

    CREATE INDEX IX_Zayi_Tarih ON Zayi (Tarih DESC);
END;
GO
