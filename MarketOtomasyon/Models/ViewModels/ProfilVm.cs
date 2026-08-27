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
    /// Acik vardiyanin acilis zamani (UTC). Kasiyer icin anlamli bilgi:
    /// "vardiyam acik mi, ne zamandir aciktim".
    /// </summary>
    public DateTime? AcikVardiyaAcilisUtc { get; set; }

    /// <summary>Ad soyad formunun degeri; hata sonrasi ekran yeniden cizilirken dolu kalir.</summary>
    public string? YeniAdSoyad { get; set; }

    public string? Hata { get; set; }
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
