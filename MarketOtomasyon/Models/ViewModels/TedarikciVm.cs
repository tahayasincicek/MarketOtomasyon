namespace MarketOtomasyon.Models.ViewModels;

public sealed class TedarikciSatirVm
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Unvan { get; set; } = "";
    public string? VergiNo { get; set; }
    public string? Telefon { get; set; }
    public bool Aktif { get; set; }
}

public sealed class TedarikciListeVm
{
    public IReadOnlyList<TedarikciSatirVm> Satirlar { get; set; } = [];
    public string? Arama { get; set; }
    public bool SadeceAktif { get; set; } = true;
    public string? Hata { get; set; }
}

/// <summary>Tedarikci ekleme/duzenleme formu. Dogrulama TedarikciFormVmValidator icinde.</summary>
public sealed class TedarikciFormVm
{
    /// <summary>Yeni kayitta 0.</summary>
    public int Id { get; set; }

    public string Kod { get; set; } = "";
    public string Unvan { get; set; } = "";
    public string? VergiNo { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public bool Aktif { get; set; } = true;
}
