/* =========================================================
   Urun fotografi
   ---------------------------------------------------------
   Resmin kendisi degil, wwwroot altindaki yolu saklanir.
   Boylece kasa ekrani her satista veritabanindan binary
   veri cekmez; dosyayi dogrudan tarayici onbellegi tasir.

   ResimKaynagi: resmin nereden geldigi. Open Food Facts
   fotograflari CC-BY-SA lisansli, atif yukumlulugu var.
   ========================================================= */

USE MarketOtomasyon;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('Urun', 'ResimYolu') IS NULL
    ALTER TABLE Urun ADD ResimYolu NVARCHAR(260) NULL;
GO

IF COL_LENGTH('Urun', 'ResimKaynagi') IS NULL
    ALTER TABLE Urun ADD ResimKaynagi NVARCHAR(200) NULL;
GO

IF COL_LENGTH('Urun', 'ResimTarihi') IS NULL
    ALTER TABLE Urun ADD ResimTarihi DATETIME2 NULL;
GO
