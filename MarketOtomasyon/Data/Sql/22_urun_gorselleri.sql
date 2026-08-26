/*
    Profesyonel urun katalog gorsellerini Urun kayitlarina baglar.
    Dosyalar wwwroot/urun-gorsel/URN001.webp - URN030.webp altindadir.
*/

USE MarketOtomasyon;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE Urun
SET ResimYolu = N'/urun-gorsel/' + Kod + N'.webp',
    ResimKaynagi = NULL,
    ResimTarihi = SYSUTCDATETIME()
WHERE Kod LIKE N'URN[0-9][0-9][0-9]'
  AND TRY_CONVERT(INT, RIGHT(Kod, 3)) BETWEEN 1 AND 30;

IF @@ROWCOUNT <> 30
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51000, 'URN001-URN030 arasindaki 30 urunun tamami bulunamadi.', 1;
END;

COMMIT TRANSACTION;

