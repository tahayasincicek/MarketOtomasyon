/* =========================================================
   Mudur onayi (manager override)
   ---------------------------------------------------------
   Kasiyer indirim limitini asinca islem eskiden dogrudan
   reddediliyordu. Gercekte olan sey suydu: mudur cagriliyor,
   kasiyerin ACIK oturumunda islemi kendisi yapiyordu. Log da
   islemi kasiyerin yaptigini soyluyordu. Yani tasarim, denetim
   izini bozmaya tesvik ediyordu.

   Artik limit asildiginda kasada mudur onayi istenir ve onay
   ayri alanlarda saklanir:
     KullaniciId          -> islemi yapan kasiyer (mevcut kolon)
     OnaylayanKullaniciId -> onayi veren mudur
     OnaySebebi           -> neden onaylandigi
     Tarih                -> zaten vardi

   Onaylayan Aciklama metnine gomulmuyor: "bu ay hangi mudur kac
   override verdi" sorusu ancak ayri kolonla sorgulanabilir.

   Ikisi de NULL kabul eder; onaysiz islemler ve mevcut kayitlar
   etkilenmez. Tekrar calistirilabilir.
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.IslemLog', 'OnaylayanKullaniciId') IS NULL
BEGIN
    ALTER TABLE dbo.IslemLog ADD OnaylayanKullaniciId INT NULL;
END;
GO

IF COL_LENGTH('dbo.IslemLog', 'OnaySebebi') IS NULL
BEGIN
    ALTER TABLE dbo.IslemLog ADD OnaySebebi NVARCHAR(300) NULL;
END;
GO

IF OBJECT_ID('FK_IslemLog_Onaylayan', 'F') IS NULL
BEGIN
    ALTER TABLE dbo.IslemLog
        ADD CONSTRAINT FK_IslemLog_Onaylayan
            FOREIGN KEY (OnaylayanKullaniciId) REFERENCES dbo.Kullanici(Id);
END;
GO

/* "Hangi mudur ne kadar onay verdi" sorgusu icin. Filtreli index:
   kayitlarin buyuk cogunlugu onaysiz, onlari indexlemek gereksiz.

   DIKKAT: Filtreli index, IslemLog'a yazan HER oturumun
   QUOTED_IDENTIFIER ON olmasini zorunlu kilar. .NET SqlClient bunu
   varsayilan olarak ON gonderir, yani uygulama etkilenmez; ama sqlcmd
   varsayilan OFF baslatir. Bu tabloya elle INSERT eden betiklerin
   basinda SET QUOTED_IDENTIFIER ON bulunmali, yoksa Msg 1934 alinir. */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.IslemLog') AND name = 'IX_IslemLog_Onaylayan'
)
    CREATE INDEX IX_IslemLog_Onaylayan
        ON dbo.IslemLog (OnaylayanKullaniciId, Tarih DESC)
        WHERE OnaylayanKullaniciId IS NOT NULL;
GO

SELECT Kolonlar = COUNT(*)
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.IslemLog')
  AND name IN ('OnaylayanKullaniciId', 'OnaySebebi');
GO
