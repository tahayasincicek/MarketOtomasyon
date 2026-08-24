/* =========================================================
   Kasa - Hizli urun tuslari
   Barkod ve fiyat kopyalanmaz; yalnizca urun ve ekrandaki sira tutulur.
   Script tekrar calistirilabilir, mevcut secimleri silmez.
   ========================================================= */

USE MarketOtomasyon;
GO

IF OBJECT_ID(N'dbo.HizliUrun', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HizliUrun (
        Id     INT IDENTITY(1,1) PRIMARY KEY,
        UrunId INT      NOT NULL,
        Sira   SMALLINT NOT NULL CONSTRAINT DF_HizliUrun_Sira DEFAULT(0),
        Aktif  BIT      NOT NULL CONSTRAINT DF_HizliUrun_Aktif DEFAULT(1),

        CONSTRAINT UQ_HizliUrun_Urun UNIQUE (UrunId),
        CONSTRAINT FK_HizliUrun_Urun FOREIGN KEY (UrunId) REFERENCES dbo.Urun(Id),
        CONSTRAINT CK_HizliUrun_Sira CHECK (Sira >= 0)
    );

    CREATE INDEX IX_HizliUrun_Liste
        ON dbo.HizliUrun (Aktif, Sira)
        INCLUDE (UrunId);
END;
GO

/* Gelistirme verisindeki temel urunler. Urun yoksa sessizce atlanir. */
INSERT INTO dbo.HizliUrun (UrunId, Sira, Aktif)
SELECT u.Id, v.Sira, 1
FROM (VALUES
    (N'URN004', 10), -- Ekmek
    (N'URN001', 20), -- Sut
    (N'URN017', 30), -- Su 5 L
    (N'URN020', 40), -- Ayran
    (N'URN021', 50), -- Maden suyu
    (N'URN018', 60), -- Kola
    (N'URN010', 70), -- Yumurta
    (N'URN022', 80)  -- Cikolata
) v (Kod, Sira)
JOIN dbo.Urun u ON u.Kod = v.Kod
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.HizliUrun hu WHERE hu.UrunId = u.Id
);
GO

SELECT hu.Sira, u.Kod, u.Ad, hu.Aktif
FROM dbo.HizliUrun hu
JOIN dbo.Urun u ON u.Id = hu.UrunId
ORDER BY hu.Sira, hu.Id;
GO
