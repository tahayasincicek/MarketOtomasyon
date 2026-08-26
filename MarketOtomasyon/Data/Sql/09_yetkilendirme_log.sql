/* =========================================================
   Gun 19 - Cookie kimlik dogrulama ve hassas islem loglari
   Tekrar calistirilabilir.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.IslemLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IslemLog (
        Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
        KullaniciId  INT            NOT NULL,
        IslemTipi    NVARCHAR(50)   NOT NULL,
        HedefTipi    NVARCHAR(50)   NOT NULL,
        HedefId      INT            NULL,
        EskiDeger    NVARCHAR(500)  NULL,
        YeniDeger    NVARCHAR(500)  NULL,
        Aciklama     NVARCHAR(1000) NULL,
        Tarih        DATETIME2      NOT NULL
            CONSTRAINT DF_IslemLog_Tarih DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_IslemLog_Kullanici
            FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanici(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.IslemLog')
      AND name = 'IX_IslemLog_Tarih'
)
    CREATE INDEX IX_IslemLog_Tarih ON dbo.IslemLog (Tarih DESC, Id DESC);
GO

/* Ornek veri dosyasindaki gelistirme hesaplarini gercek PasswordHasher
   hash'lerine tasir. Yalnizca DEGISTIR degeri bulunan hesaplara dokunur. */
UPDATE dbo.Kullanici
SET SifreHash = 'AQAAAAIAAYagAAAAEAkhhbHkP3xI6zYOvVwZKA+B9ZUXuXScNssgFjCQHryJISn9J08QWttdTfagJrOCjw=='
WHERE KullaniciAdi = 'kasiyer1' AND SifreHash = 'DEGISTIR';

UPDATE dbo.Kullanici
SET SifreHash = 'AQAAAAIAAYagAAAAEFtV7ctmmIE5rS9S0zETWk8ySmqMWFetnfwrtek1Drq+jm3D0p96Zx0BZgOMXe62sQ=='
WHERE KullaniciAdi = 'mudur' AND SifreHash = 'DEGISTIR';
GO
