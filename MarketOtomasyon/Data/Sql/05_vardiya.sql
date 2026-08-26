/* =========================================================
   Vardiya kapanisi ve Z raporu
   ---------------------------------------------------------
   Iade, olustugu ANDAKI vardiyaya baglanir; iade edilen fisin
   vardiyasina degil. Kasadan para o an cikar.
   Eski satirlar icin NULL kalir.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('Iade', 'VardiyaId') IS NULL
BEGIN
    ALTER TABLE Iade ADD VardiyaId INT NULL;
END
GO

IF OBJECT_ID('FK_Iade_Vardiya', 'F') IS NULL
BEGIN
    ALTER TABLE Iade
        ADD CONSTRAINT FK_Iade_Vardiya FOREIGN KEY (VardiyaId) REFERENCES Vardiya(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Iade_Vardiya')
    CREATE INDEX IX_Iade_Vardiya ON Iade (VardiyaId);
GO

-- Z raporunda fisler vardiyaya gore taraniyor.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Fis_Vardiya')
    CREATE INDEX IX_Fis_Vardiya ON Fis (VardiyaId, Durum);
GO