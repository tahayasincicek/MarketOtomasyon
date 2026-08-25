namespace MarketOtomasyon.Models.Entities;

/// <summary>Tek maliyetle depoya giren stok katmani. FEFO sirasinda tuketilir:
/// son kullanma tarihi en yakin parti once cikar.</summary>
public class StokParti
{
    public long Id { get; set; }
    public int UrunId { get; set; }
    public int DepoId { get; set; }
    public long? StokHareketId { get; set; }
    public DateTime GirisTarihi { get; set; }
    public decimal GirisMiktari { get; set; }
    public decimal KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public string? Aciklama { get; set; }

    /// <summary>
    /// Son kullanma tarihi. Raf omru olmayan urunlerde (kirtasiye,
    /// zuccaciye) null; FEFO siralamasinda bunlar sona duser.
    /// Takvim gunudur, saat tasimaz - UTC donusumune girmez.
    /// </summary>
    public DateTime? SonKullanmaTarihi { get; set; }

    /// <summary>
    /// Tedarikci lot/parti numarasi. Bir lot geri cagrildiginda,
    /// StokPartiTuketim.FisSatirId uzerinden hangi fislerde satildigi
    /// bulunabilir.
    /// </summary>
    public string? LotNo { get; set; }

    public string? TedarikciAdi { get; set; }
}
