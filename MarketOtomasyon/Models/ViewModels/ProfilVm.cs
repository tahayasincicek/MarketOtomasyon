namespace MarketOtomasyon.Models.ViewModels;

/// <summary>Kullanicinin kendi hesap ekrani.</summary>
public sealed class ProfilVm
{
    public int KullaniciId { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;

    /// <summary>Ekranda gosterilecek rol adi ("Müdür" / "Kasiyer").</summary>
    public string RolAdi { get; set; } = string.Empty;

    /// <summary>
    /// Rolun sayisal karsiligi. Oturum tazelenirken rol claim'i bundan
    /// uretilir; ekrandaki metinden geri cozmek kirilgan olurdu.
    /// </summary>
    public byte RolKodu { get; set; }

    /// <summary>
    /// Acik vardiyanin acilis zamani (UTC). Kasiyer icin anlamli bilgi:
    /// "vardiyam acik mi, ne zamandir aciktim".
    /// </summary>
    public DateTime? AcikVardiyaAcilisUtc { get; set; }

    /// <summary>Form degerleri; hata sonrasi ekran yeniden cizilirken dolu kalir.</summary>
    public string? YeniAdSoyad { get; set; }
    public string? YeniKullaniciAdi { get; set; }

    /// <summary>Hatalar bolum bazinda ayri: mesaj kendi formunun yaninda cikmali.</summary>
    public string? Hata { get; set; }
    public string? KullaniciAdiHatasi { get; set; }
    public string? SifreHatasi { get; set; }
}

/// <summary>
/// Sifre degistirme formu. Alanlar ViewModel'de tutuluyor ama ekrana
/// GERI YAZILMIYOR: sifre iceren bir alanin HTML'e basilmasi, tarayici
/// gecmisinde ve sayfa kaynaginda iz birakir.
/// </summary>
public sealed class SifreDegistirVm
{
    public string? MevcutSifre { get; set; }
    public string? YeniSifre { get; set; }
    public string? YeniSifreTekrar { get; set; }
}
