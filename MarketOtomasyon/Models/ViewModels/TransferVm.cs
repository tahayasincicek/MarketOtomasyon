using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Transfer formundaki tek satir.</summary>
public sealed class TransferSatirVm
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Birim { get; set; } = "";
    public decimal Miktar { get; set; }

    /// <summary>Kaynak depodaki mevcut bakiye; ekranda uyari icin.</summary>
    public decimal KaynakBakiye { get; set; }
}

public sealed class TransferEkranVm
{
    public int KaynakDepoId { get; set; }
    public int HedefDepoId { get; set; }
    public string? Aciklama { get; set; }

    public List<TransferSatirVm> Satirlar { get; set; } = [];

    public IReadOnlyList<Depo> Depolar { get; set; } = [];
    public IReadOnlyList<TransferGecmisSatirVm> SonTransferler { get; set; } = [];

    public string? Hata { get; set; }
}

/// <summary>Son transferler listesi.</summary>
public sealed class TransferGecmisSatirVm
{
    public int Id { get; set; }
    public string TransferNo { get; set; } = "";
    public DateTime Tarih { get; set; }
    public string KaynakDepo { get; set; } = "";
    public string HedefDepo { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string? Aciklama { get; set; }
    public int SatirSayisi { get; set; }
    public decimal ToplamMiktar { get; set; }
}
