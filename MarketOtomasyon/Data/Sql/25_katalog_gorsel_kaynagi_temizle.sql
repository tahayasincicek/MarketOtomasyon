/*
    Yerel katalog görsellerinde kullanıcıya gösterilecek harici kaynak
    atfı bulunmaz. Önceki sürümün yazdığı açıklamayı temizler.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE Urun
SET ResimKaynagi = NULL
WHERE Kod LIKE N'URN[0-9][0-9][0-9]'
  AND TRY_CONVERT(INT, RIGHT(Kod, 3)) BETWEEN 31 AND 60
  AND ResimKaynagi IS NOT NULL;

COMMIT TRANSACTION;
GO
