/* =========================================================
   Market Otomasyonu - Cekirdek sema (MSSQL)
   Kapsam: urun, barkod, fiyat, stok, vardiya, fis, odeme
   Yaklasim: Dapper ile ham SQL. Tablolar elle yonetilir.
   Not: stok miktari KOLON OLARAK TUTULMAZ; StokHareket
        toplamindan hesaplanir.
   ========================================================= */

CREATE DATABASE MarketOtomasyon;
GO
USE MarketOtomasyon;
GO

/* ---------- Kategori (agac yapisi) ---------- */
CREATE TABLE Kategori (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    Kod           NVARCHAR(20)  NOT NULL,
    Ad            NVARCHAR(100) NOT NULL,
    UstKategoriId INT           NULL,
    Aktif         BIT           NOT NULL CONSTRAINT DF_Kat_Aktif DEFAULT(1),
    CONSTRAINT UQ_Kategori_Kod UNIQUE (Kod),
    CONSTRAINT FK_Kategori_Ust FOREIGN KEY (UstKategoriId) REFERENCES Kategori(Id)
);

/* ---------- Urun ---------- */
CREATE TABLE Urun (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Kod             NVARCHAR(30)  NOT NULL,
    Ad              NVARCHAR(200) NOT NULL,
    KategoriId      INT           NOT NULL,
    Birim           NVARCHAR(10)  NOT NULL,          -- ADET, KG
    KdvOrani        DECIMAL(5,2)  NOT NULL,          -- 1, 10, 20
    MinStokSeviyesi DECIMAL(18,4) NOT NULL CONSTRAINT DF_Urun_MinStok DEFAULT(0),
    Tartili         BIT           NOT NULL CONSTRAINT DF_Urun_Tartili DEFAULT(0),
    Aktif           BIT           NOT NULL CONSTRAINT DF_Urun_Aktif DEFAULT(1),
    OlusturmaTarihi DATETIME2     NOT NULL CONSTRAINT DF_Urun_Tarih DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT UQ_Urun_Kod UNIQUE (Kod),
    CONSTRAINT FK_Urun_Kategori FOREIGN KEY (KategoriId) REFERENCES Kategori(Id)
);

/* ---------- Urun barkodlari ----------
   Bir urunun birden fazla barkodu olabilir.
   Carpan: koli barkodu okutulunca kac adet sepete eklenecek. */
CREATE TABLE UrunBarkod (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    UrunId  INT           NOT NULL,
    Barkod  NVARCHAR(30)  NOT NULL,
    Carpan  DECIMAL(18,4) NOT NULL CONSTRAINT DF_Barkod_Carpan DEFAULT(1),
    Tip     TINYINT       NOT NULL CONSTRAINT DF_Barkod_Tip DEFAULT(1),  -- 1: tekli, 2: koli, 3: terazi oneki
    CONSTRAINT UQ_UrunBarkod UNIQUE (Barkod),
    CONSTRAINT FK_Barkod_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id)
);

/* ---------- Fiyat gecmisi ----------
   Fiyat degisince eski kayit silinmez; BitisTarihi doldurulur. */
CREATE TABLE UrunFiyat (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    UrunId           INT           NOT NULL,
    Fiyat            DECIMAL(18,4) NOT NULL,
    BaslangicTarihi  DATETIME2     NOT NULL CONSTRAINT DF_Fiyat_Bas DEFAULT(SYSUTCDATETIME()),
    BitisTarihi      DATETIME2     NULL,
    CONSTRAINT FK_Fiyat_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
    CONSTRAINT CK_Fiyat_Pozitif CHECK (Fiyat > 0)
);
CREATE INDEX IX_UrunFiyat_Guncel ON UrunFiyat (UrunId, BitisTarihi);

/* ---------- Depo ---------- */
CREATE TABLE Depo (
    Id    INT IDENTITY(1,1) PRIMARY KEY,
    Kod   NVARCHAR(20)  NOT NULL,
    Ad    NVARCHAR(100) NOT NULL,
    Aktif BIT           NOT NULL CONSTRAINT DF_Depo_Aktif DEFAULT(1),
    CONSTRAINT UQ_Depo_Kod UNIQUE (Kod)
);

/* ---------- Kullanici ---------- */
CREATE TABLE Kullanici (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    KullaniciAdi NVARCHAR(50)  NOT NULL,
    AdSoyad      NVARCHAR(100) NOT NULL,
    SifreHash    NVARCHAR(500) NOT NULL,
    Rol          TINYINT       NOT NULL,   -- 1: kasiyer, 2: mudur
    Aktif        BIT           NOT NULL CONSTRAINT DF_Kul_Aktif DEFAULT(1),
    CONSTRAINT UQ_Kullanici UNIQUE (KullaniciAdi)
);

/* ---------- Vardiya ---------- */
CREATE TABLE Vardiya (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    KullaniciId    INT           NOT NULL,
    AcilisTarihi   DATETIME2     NOT NULL CONSTRAINT DF_Vard_Acilis DEFAULT(SYSUTCDATETIME()),
    AcilisTutari   DECIMAL(18,4) NOT NULL,
    KapanisTarihi  DATETIME2     NULL,
    SayilanTutar   DECIMAL(18,4) NULL,
    BeklenenTutar  DECIMAL(18,4) NULL,
    Fark           DECIMAL(18,4) NULL,
    Durum          TINYINT       NOT NULL CONSTRAINT DF_Vard_Durum DEFAULT(1),  -- 1: acik, 2: kapali
    CONSTRAINT FK_Vardiya_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id)
);

/* ---------- Fis numarasi icin sequence ----------
   MAX(no)+1 kullanma: es zamanli iki satista ayni numarayi uretir. */
CREATE SEQUENCE FisNoSeq AS INT START WITH 1 INCREMENT BY 1;
GO

/* ---------- Fis (satis basligi) ----------
   Durum 1 (Beklemede): askiya alinmis sepet. Stogu etkilemez.
   Durum 2 (Tamamlandi): odeme alinmis, stok dusmus. */
CREATE TABLE Fis (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    FisNo          NVARCHAR(20)  NOT NULL,
    VardiyaId      INT           NOT NULL,
    KullaniciId    INT           NOT NULL,
    MusteriId      INT           NULL,
    Tarih          DATETIME2     NOT NULL CONSTRAINT DF_Fis_Tarih DEFAULT(SYSUTCDATETIME()),
    AraToplam      DECIMAL(18,4) NOT NULL CONSTRAINT DF_Fis_Ara DEFAULT(0),
    ToplamIndirim  DECIMAL(18,4) NOT NULL CONSTRAINT DF_Fis_Ind DEFAULT(0),
    ToplamKdv      DECIMAL(18,4) NOT NULL CONSTRAINT DF_Fis_Kdv DEFAULT(0),
    GenelToplam    DECIMAL(18,4) NOT NULL CONSTRAINT DF_Fis_Genel DEFAULT(0),
    Durum          TINYINT       NOT NULL CONSTRAINT DF_Fis_Durum DEFAULT(1),
                                 -- 1: beklemede, 2: tamamlandi, 9: iptal
    CONSTRAINT UQ_Fis_No UNIQUE (FisNo),
    CONSTRAINT FK_Fis_Vardiya FOREIGN KEY (VardiyaId) REFERENCES Vardiya(Id),
    CONSTRAINT FK_Fis_Kullanici FOREIGN KEY (KullaniciId) REFERENCES Kullanici(Id)
);
CREATE INDEX IX_Fis_Tarih ON Fis (Tarih) INCLUDE (GenelToplam, Durum);

/* ---------- Fis satiri ----------
   BirimFiyat burada SAKLANIR. Urun kartindan okunmaz:
   fiyat degisirse gecmis fisler ve iade tutarlari bozulur. */
CREATE TABLE FisSatir (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    FisId            INT           NOT NULL,
    SatirNo          INT           NOT NULL,
    UrunId           INT           NOT NULL,
    Miktar           DECIMAL(18,4) NOT NULL,
    BirimFiyat       DECIMAL(18,4) NOT NULL,
    IndirimTutari    DECIMAL(18,4) NOT NULL CONSTRAINT DF_FS_Ind DEFAULT(0),
    KdvOrani         DECIMAL(5,2)  NOT NULL,
    SatirToplam      DECIMAL(18,4) NOT NULL,
    IadeEdilenMiktar DECIMAL(18,4) NOT NULL CONSTRAINT DF_FS_Iade DEFAULT(0),
    KampanyaId       INT           NULL,
    CONSTRAINT UQ_FisSatir UNIQUE (FisId, SatirNo),
    CONSTRAINT FK_FisSatir_Fis  FOREIGN KEY (FisId)  REFERENCES Fis(Id),
    CONSTRAINT FK_FisSatir_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
    CONSTRAINT CK_FisSatir_Miktar CHECK (Miktar > 0),
    CONSTRAINT CK_FisSatir_Iade   CHECK (IadeEdilenMiktar >= 0 AND IadeEdilenMiktar <= Miktar)
);

/* ---------- Odeme ----------
   Bire-cok: 40 TL nakit + 60 TL kart ayni fise baglanabilir. */
CREATE TABLE Odeme (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    FisId       INT           NOT NULL,
    Tip         TINYINT       NOT NULL,   -- 1: nakit, 2: kart, 3: puan
    Tutar       DECIMAL(18,4) NOT NULL,
    AlinanTutar DECIMAL(18,4) NULL,       -- nakitte musteriden alinan
    ParaUstu    DECIMAL(18,4) NULL,
    OnayKodu    NVARCHAR(50)  NULL,       -- kartta
    Tarih       DATETIME2     NOT NULL CONSTRAINT DF_Odeme_Tarih DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_Odeme_Fis FOREIGN KEY (FisId) REFERENCES Fis(Id),
    CONSTRAINT CK_Odeme_Tutar CHECK (Tutar > 0)
);

/* ---------- Stok hareketi ----------
   Tum stok degisimlerinin tek merkezi.
   KaynakTip: 1 satis, 2 iade, 3 mal kabul, 4 sayim, 5 zayi, 6 acilis */
CREATE TABLE StokHareket (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    UrunId    INT           NOT NULL,
    DepoId    INT           NOT NULL,
    Tarih     DATETIME2     NOT NULL CONSTRAINT DF_SH_Tarih DEFAULT(SYSUTCDATETIME()),
    Yon       TINYINT       NOT NULL,     -- 1: giris, 2: cikis
    Miktar    DECIMAL(18,4) NOT NULL,     -- her zaman pozitif
    KaynakTip TINYINT       NOT NULL,
    KaynakId  INT           NULL,
    Aciklama  NVARCHAR(200) NULL,
    CONSTRAINT FK_SH_Urun FOREIGN KEY (UrunId) REFERENCES Urun(Id),
    CONSTRAINT FK_SH_Depo FOREIGN KEY (DepoId) REFERENCES Depo(Id),
    CONSTRAINT CK_SH_Miktar CHECK (Miktar > 0)
);
CREATE INDEX IX_StokHareket_Urun ON StokHareket (UrunId, DepoId) INCLUDE (Yon, Miktar);
GO

/* ---------- Stok bakiye view'i ---------- */
CREATE VIEW vw_StokBakiye AS
SELECT
    h.UrunId,
    h.DepoId,
    SUM(CASE WHEN h.Yon = 1 THEN h.Miktar ELSE -h.Miktar END) AS Bakiye
FROM StokHareket h
GROUP BY h.UrunId, h.DepoId;
GO

/* ---------- Guncel fiyat view'i ---------- */
CREATE VIEW vw_GuncelFiyat AS
SELECT f.UrunId, f.Fiyat
FROM UrunFiyat f
WHERE f.BitisTarihi IS NULL;
GO

/* ---------- Barkod cozumleme sorgusu (ornek) ----------
   Dapper'dan cagiracagin tipik sorgu:

   SELECT u.Id, u.Kod, u.Ad, u.Birim, u.KdvOrani, u.Tartili,
          b.Carpan, gf.Fiyat
   FROM UrunBarkod b
   JOIN Urun u        ON u.Id = b.UrunId AND u.Aktif = 1
   LEFT JOIN vw_GuncelFiyat gf ON gf.UrunId = u.Id
   WHERE b.Barkod = @barkod;
   ------------------------------------------------------- */

/* Ornek/test verisi bu dosyada degil: Data/Sql/90_ornek_veri.sql */
