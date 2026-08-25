namespace MarketOtomasyon.Models.Entities;

/// <summary>
/// Depolar arasi transferin basligi. Transfer numarasi, taşıyan kullanici
/// ve aciklama burada; tasinan urunler StokTransferSatir'da.
///
/// Ayri baslik tablosu sart: StokHareket tablosunda KullaniciId yok, yani
/// "bu transferi kim yapti" bilgisi hareketlere sigmiyor.
/// </summary>
public sealed class StokTransfer
{
    public int Id { get; set; }
    public string TransferNo { get; set; } = "";
    public int KaynakDepoId { get; set; }
    public int HedefDepoId { get; set; }
    public int KullaniciId { get; set; }
    public DateTime Tarih { get; set; }
    public string? Aciklama { get; set; }
}

public sealed class StokTransferSatir
{
    public int Id { get; set; }
    public int TransferId { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }
}
