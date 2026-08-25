namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Personel listesinin bir satiri.</summary>
public sealed class PersonelSatirVm
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public byte Rol { get; set; }
    public bool Aktif { get; set; }

    /// <summary>
    /// Acik vardiyanin Id'si, yoksa null. Listede gosterilir: mudur kimin
    /// kasada oldugunu gormeden pasiflestirme karari vermemeli.
    /// </summary>
    public int? AcikVardiyaId { get; set; }

    public bool VardiyadaMi => AcikVardiyaId is not null;
}

/// <summary>Yeni personel formu.</summary>
public sealed class PersonelFormVm
{
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string Sifre { get; set; } = "";
    public byte Rol { get; set; } = 1;
}

public sealed class PersonelEkranVm
{
    public IReadOnlyList<PersonelSatirVm> Satirlar { get; set; } = [];
    public PersonelFormVm YeniPersonel { get; set; } = new();

    /// <summary>Kendi satirini ayirt etmek icin; kendine islem yapilamaz.</summary>
    public int OturumdakiKullaniciId { get; set; }

    public string? Hata { get; set; }

    public int AktifMudurSayisi { get; set; }
}
