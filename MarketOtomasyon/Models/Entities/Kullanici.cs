namespace MarketOtomasyon.Models.Entities;

public sealed class Kullanici
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string SifreHash { get; set; } = "";
    public byte Rol { get; set; }
    public bool Aktif { get; set; }
}
