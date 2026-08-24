using System.ComponentModel.DataAnnotations;

namespace MarketOtomasyon.Models.ViewModels;

public sealed class GirisVm
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    [Display(Name = "Kullanıcı adı")]
    public string KullaniciAdi { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Sifre { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class IslemLogSatirVm
{
    public long Id { get; set; }
    public DateTime Tarih { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string IslemTipi { get; set; } = "";
    public string HedefTipi { get; set; } = "";
    public int? HedefId { get; set; }
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string? Aciklama { get; set; }
}
