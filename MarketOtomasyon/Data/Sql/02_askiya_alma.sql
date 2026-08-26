/* =========================================================
   Askiya alma / geri cagirma
   ---------------------------------------------------------
   Beklemedeki fis (Durum 1) kasadaki acik sepettir. Musteri
   kartini unutup arabaya donunce kasiyerin o sepeti bir kenara
   koyup sonraki musteriye gecebilmesi gerekir.

   Bunun icin "beklemede" durumu ikiye ayrilir:
     Askida = 0 -> kasada acik olan sepet (ayni anda en fazla bir tane)
     Askida = 1 -> bir kenara alinmis, listeden geri cagrilabilir

   Durum alanina yeni bir deger eklemek yerine ayri bir kolon
   kullanildi: askiya alinan fis hala "beklemede"dir, stogu
   etkilemez; degisen tek sey kasada acik olup olmadigidir.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Fis') AND name = 'Askida')
BEGIN
    ALTER TABLE Fis
        ADD Askida BIT NOT NULL CONSTRAINT DF_Fis_Askida DEFAULT(0);
END
GO

/* Acik sepet aramasi bu iki kolonu birlikte kullanir. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fis_AcikSepet' AND object_id = OBJECT_ID('Fis'))
BEGIN
    CREATE INDEX IX_Fis_AcikSepet ON Fis (VardiyaId, Durum, Askida) INCLUDE (FisNo, GenelToplam);
END
GO
