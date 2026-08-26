/* =========================================================
   Depolar arasi transfer
   ---------------------------------------------------------
   Depo tablosu ve depo bazli stok bastan beri vardi ama bir
   urunu Arka Depo'dan Market Rafi'na tasiyan islem yoktu.
   Sonuc: ikinci depo fiilen olu kaldi. Satis deposu sabit
   (appsettings.json -> Satis.DepoKodu = MRK) oldugu icin
   Arka Depo'ya giren mal hicbir zaman satilamiyordu.

   Transfer YALNIZCA iki stok hareketi degildir. StokParti
   depo bazlidir; partiler tasinmazsa hedef depoda bakiye
   gorunur ama parti bulunmaz ve satis aninda FIFO tuketimi
   "parti bakiyesi yetersiz" ile kirilir. TransferService bu
   yuzden kaynak partileri FEFO ile tuketip hedef depoda ayni
   maliyet, son kullanma tarihi ve lot ile yeni partiler acar.

   Baslik/satir ayrimi Fis-FisSatir ve Iade-IadeSatir ile ayni
   desende: transfer numarasi, kullanici ve aciklama baslikta,
   tasinan urunler satirlarda. StokHareket tablosunda KullaniciId
   olmadigi icin bu bilgi baska turlu tutulamazdi.

   Tekrar calistirilabilir.
   ========================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* Numara icin sequence: MAX(TransferNo)+1 es zamanli iki transferde
   ayni numarayi uretir. FisNoSeq ve IadeNoSeq de ayni sebeple. */
IF OBJECT_ID('TransferNoSeq') IS NULL
    CREATE SEQUENCE TransferNoSeq AS INT START WITH 1 INCREMENT BY 1;
GO

IF OBJECT_ID('StokTransfer') IS NULL
BEGIN
    CREATE TABLE StokTransfer (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        TransferNo    NVARCHAR(20)  NOT NULL,
        KaynakDepoId  INT           NOT NULL,
        HedefDepoId   INT           NOT NULL,
        KullaniciId   INT           NOT NULL,
        Tarih         DATETIME2     NOT NULL
            CONSTRAINT DF_Transfer_Tarih DEFAULT(SYSUTCDATETIME()),
        Aciklama      NVARCHAR(300) NULL,

        CONSTRAINT UQ_Transfer_No UNIQUE (TransferNo),
        CONSTRAINT FK_Transfer_Kaynak    FOREIGN KEY (KaynakDepoId) REFERENCES Depo(Id),
        CONSTRAINT FK_Transfer_Hedef     FOREIGN KEY (HedefDepoId)  REFERENCES Depo(Id),
        CONSTRAINT FK_Transfer_Kullanici FOREIGN KEY (KullaniciId)  REFERENCES Kullanici(Id),

        /* Kural sinifinda da denetleniyor; burasi son savunma hatti.
           Elle yazilmis bir INSERT o kontrolu atlayabilir. */
        CONSTRAINT CK_Transfer_FarkliDepo CHECK (KaynakDepoId <> HedefDepoId)
    );
END
GO

IF OBJECT_ID('StokTransferSatir') IS NULL
BEGIN
    CREATE TABLE StokTransferSatir (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        TransferId INT           NOT NULL,
        UrunId     INT           NOT NULL,
        Miktar     DECIMAL(18,4) NOT NULL,

        CONSTRAINT FK_TransferSatir_Transfer FOREIGN KEY (TransferId) REFERENCES StokTransfer(Id),
        CONSTRAINT FK_TransferSatir_Urun     FOREIGN KEY (UrunId)     REFERENCES Urun(Id),
        CONSTRAINT CK_TransferSatir_Miktar   CHECK (Miktar > 0),

        /* Ayni transferde ayni urun iki satirda yer almasin; ekran
           tekrar eklenen urunun miktarini birlestirir. */
        CONSTRAINT UQ_TransferSatir UNIQUE (TransferId, UrunId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transfer_Tarih')
    CREATE INDEX IX_Transfer_Tarih ON StokTransfer (Tarih DESC, Id DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferSatir_Transfer')
    CREATE INDEX IX_TransferSatir_Transfer ON StokTransferSatir (TransferId);
GO

SELECT Tablolar = (SELECT COUNT(*) FROM sys.tables
                   WHERE name IN ('StokTransfer','StokTransferSatir')),
       Sequence = CASE WHEN OBJECT_ID('TransferNoSeq') IS NULL THEN 0 ELSE 1 END;
GO
