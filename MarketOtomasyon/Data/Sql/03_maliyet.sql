/* =========================================================
   Gun 17 - Parti maliyeti

   Sevk sirasi 14_skt_lot.sql ile FEFO ya gecti: raftan once son
   kullanma tarihi en yakin parti cikar. Bu dosyadaki yapi degismedi,
   yalnizca MaliyetRepository icindeki ORDER BY degisti.

   StokParti her maliyetli girişi ayrı katman olarak saklar.
   StokPartiTuketim bir satış satırının hangi partilerden,
   hangi maliyetle karşılandığını değişmez biçimde kaydeder.
   ========================================================= */

USE MarketOtomasyon;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('StokParti', 'U') IS NULL
BEGIN
    CREATE TABLE StokParti (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        UrunId          INT           NOT NULL,
        DepoId          INT           NOT NULL,
        StokHareketId   BIGINT        NULL,
        GirisTarihi     DATETIME2     NOT NULL CONSTRAINT DF_StokParti_Tarih DEFAULT(SYSUTCDATETIME()),
        GirisMiktari    DECIMAL(18,4) NOT NULL,
        KalanMiktar     DECIMAL(18,4) NOT NULL,
        BirimMaliyet    DECIMAL(18,4) NOT NULL,
        Aciklama        NVARCHAR(200) NULL,
        CONSTRAINT FK_StokParti_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
        CONSTRAINT FK_StokParti_Depo FOREIGN KEY (DepoId) REFERENCES Depo(Id),
        CONSTRAINT FK_StokParti_Hareket FOREIGN KEY (StokHareketId) REFERENCES StokHareket(Id),
        CONSTRAINT CK_StokParti_Giris CHECK (GirisMiktari > 0),
        CONSTRAINT CK_StokParti_Kalan CHECK (KalanMiktar >= 0 AND KalanMiktar <= GirisMiktari),
        CONSTRAINT CK_StokParti_Maliyet CHECK (BirimMaliyet >= 0)
    );

END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StokParti') AND name = 'IX_StokParti_FIFO')
    CREATE INDEX IX_StokParti_FIFO
        ON StokParti (UrunId, DepoId, GirisTarihi, Id)
        INCLUDE (KalanMiktar, BirimMaliyet);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StokParti') AND name = 'UX_StokParti_Hareket')
    CREATE UNIQUE INDEX UX_StokParti_Hareket
        ON StokParti (StokHareketId)
        WHERE StokHareketId IS NOT NULL;
GO

IF OBJECT_ID('StokPartiTuketim', 'U') IS NULL
BEGIN
    CREATE TABLE StokPartiTuketim (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        StokPartiId     BIGINT        NOT NULL,
        StokHareketId   BIGINT        NOT NULL,
        FisSatirId      INT           NULL,
        Miktar          DECIMAL(18,4) NOT NULL,
        BirimMaliyet    DECIMAL(18,4) NOT NULL,
        ToplamMaliyet   AS CONVERT(DECIMAL(18,4), Miktar * BirimMaliyet) PERSISTED,
        Tarih           DATETIME2     NOT NULL CONSTRAINT DF_StokPartiTuketim_Tarih DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_PartiTuketim_Parti FOREIGN KEY (StokPartiId) REFERENCES StokParti(Id),
        CONSTRAINT FK_PartiTuketim_Hareket FOREIGN KEY (StokHareketId) REFERENCES StokHareket(Id),
        CONSTRAINT FK_PartiTuketim_FisSatir FOREIGN KEY (FisSatirId) REFERENCES FisSatir(Id),
        CONSTRAINT CK_PartiTuketim_Miktar CHECK (Miktar > 0),
        CONSTRAINT CK_PartiTuketim_Maliyet CHECK (BirimMaliyet >= 0),
        CONSTRAINT UQ_PartiTuketim UNIQUE (StokPartiId, StokHareketId)
    );

END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StokPartiTuketim') AND name = 'IX_PartiTuketim_FisSatir')
    CREATE INDEX IX_PartiTuketim_FisSatir
        ON StokPartiTuketim (FisSatirId)
        INCLUDE (Miktar, BirimMaliyet, ToplamMaliyet);
GO

/* FIFO devreye alınmadan önce oluşmuş pozitif bakiyeler tek bir devir
   partisine dönüştürülür. Kesin alış maliyeti bilinmediği için güncel
   KDV hariç satış fiyatı başlangıç tahmini olarak kullanılır. */
INSERT INTO StokParti
    (UrunId, DepoId, GirisTarihi, GirisMiktari, KalanMiktar, BirimMaliyet, Aciklama)
SELECT b.UrunId,
       b.DepoId,
       SYSUTCDATETIME(),
       b.Bakiye,
       b.Bakiye,
       CONVERT(DECIMAL(18,4), COALESCE(gf.Fiyat / NULLIF(1 + u.KdvOrani / 100.0, 0), 0)),
       N'FIFO açılış devir partisi'
FROM vw_StokBakiye b
JOIN Urun u ON u.Id = b.UrunId
LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = b.UrunId
WHERE b.Bakiye > 0
  AND NOT EXISTS (
      SELECT 1
      FROM StokParti p
      WHERE p.UrunId = b.UrunId AND p.DepoId = b.DepoId
  );
GO

SELECT COUNT(*) AS PartiSayisi, SUM(KalanMiktar) AS ToplamPartiBakiyesi
FROM StokParti;
GO
