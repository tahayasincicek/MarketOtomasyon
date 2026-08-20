using MarketOtomasyon.Models.Entities;

namespace MarketOtomasyon.Models.ViewModels;

/// <summary>
/// Kampanya tanimlama formu. Kullaniciya ham kosul/sonuc satirlari
/// gosterilmez; dort hazir tip arasindan secim yaptirilir ve form
/// bunu kosul/sonuc kayitlarina cevirir.
/// </summary>
public class KampanyaFormVm
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;

    /// <summary>1: yuzde, 2: tutar, 3: N al M ode, 4: tutar baraji.</summary>
    public byte KampanyaTipi { get; set; } = TipYuzdeIndirim;

    /// <summary>1: urun, 2: kategori. Tutar barajinda kullanilmaz.</summary>
    public byte Kapsam { get; set; } = KapsamUrun;

    public int? UrunId { get; set; }
    public int? KategoriId { get; set; }

    public decimal? Yuzde { get; set; }
    public decimal? Tutar { get; set; }

    /// <summary>"N al M ode"deki N.</summary>
    public decimal? AlinacakMiktar { get; set; }

    /// <summary>"N al M ode"deki M.</summary>
    public decimal? OdenecekMiktar { get; set; }

    /// <summary>Tutar barajinda sepetin gecmesi gereken tutar.</summary>
    public decimal? MinSepetTutari { get; set; }

    public int Oncelik { get; set; } = 100;
    public DateTime BaslangicTarihi { get; set; } = DateTime.Today;
    public DateTime? BitisTarihi { get; set; }
    public bool Aktif { get; set; } = true;
    public bool DigerleriyleBirlesir { get; set; }

    public IReadOnlyList<Urun> Urunler { get; set; } = [];
    public IReadOnlyList<Kategori> Kategoriler { get; set; } = [];

    public const byte TipYuzdeIndirim = 1;
    public const byte TipTutarIndirimi = 2;
    public const byte TipNAlMOde = 3;
    public const byte TipTutarBaraji = 4;

    public const byte KapsamUrun = 1;
    public const byte KapsamKategori = 2;

    /// <summary>Form alanlarini kosul/sonuc kayitlarina cevirir.</summary>
    public KampanyaTanimVm TanimaCevir()
    {
        var tanim = new KampanyaTanimVm
        {
            Id = Id,
            Kod = Kod.Trim(),
            Ad = Ad.Trim(),
            Oncelik = Oncelik,
            BaslangicTarihi = BaslangicTarihi,
            BitisTarihi = BitisTarihi,
            Aktif = Aktif,
            DigerleriyleBirlesir = DigerleriyleBirlesir
        };

        if (KampanyaTipi == TipTutarBaraji)
        {
            tanim.Kosullar.Add(new KampanyaKosulVm
            {
                Tip = KosulTipi.SepetTutari,
                MinTutar = MinSepetTutari
            });
        }
        else
        {
            tanim.Kosullar.Add(new KampanyaKosulVm
            {
                Tip = Kapsam == KapsamKategori ? KosulTipi.Kategori : KosulTipi.Urun,
                UrunId = Kapsam == KapsamUrun ? UrunId : null,
                KategoriId = Kapsam == KapsamKategori ? KategoriId : null,
                MinMiktar = KampanyaTipi == TipNAlMOde ? AlinacakMiktar : null
            });
        }

        tanim.Sonuclar.Add(KampanyaTipi switch
        {
            TipNAlMOde => new KampanyaSonucVm { Tip = SonucTipi.NAlMOde, OdenecekMiktar = OdenecekMiktar },
            TipTutarIndirimi => new KampanyaSonucVm { Tip = SonucTipi.TutarIndirimi, Tutar = Tutar },
            _ => new KampanyaSonucVm { Tip = SonucTipi.YuzdeIndirim, Yuzde = Yuzde }
        });

        return tanim;
    }

    /// <summary>Kayitli kampanyayi forma doldurur (duzenleme ekrani).</summary>
    public static KampanyaFormVm TanimdanOlustur(KampanyaTanimVm tanim)
    {
        var kosul = tanim.Kosullar.FirstOrDefault();
        var sonuc = tanim.Sonuclar.FirstOrDefault();

        var form = new KampanyaFormVm
        {
            Id = tanim.Id,
            Kod = tanim.Kod,
            Ad = tanim.Ad,
            Oncelik = tanim.Oncelik,
            BaslangicTarihi = tanim.BaslangicTarihi,
            BitisTarihi = tanim.BitisTarihi,
            Aktif = tanim.Aktif,
            DigerleriyleBirlesir = tanim.DigerleriyleBirlesir,
            UrunId = kosul?.UrunId,
            KategoriId = kosul?.KategoriId,
            AlinacakMiktar = kosul?.MinMiktar,
            MinSepetTutari = kosul?.MinTutar,
            Yuzde = sonuc?.Yuzde,
            Tutar = sonuc?.Tutar,
            OdenecekMiktar = sonuc?.OdenecekMiktar
        };

        form.Kapsam = kosul?.Tip == KosulTipi.Kategori ? KapsamKategori : KapsamUrun;

        form.KampanyaTipi = kosul?.Tip == KosulTipi.SepetTutari
            ? TipTutarBaraji
            : sonuc?.Tip switch
            {
                SonucTipi.NAlMOde => TipNAlMOde,
                SonucTipi.TutarIndirimi => TipTutarIndirimi,
                _ => TipYuzdeIndirim
            };

        return form;
    }

    public string TipAdi => KampanyaTipi switch
    {
        TipYuzdeIndirim => "Yüzde indirim",
        TipTutarIndirimi => "Tutar indirimi",
        TipNAlMOde => "N al M öde",
        TipTutarBaraji => "Tutar barajı",
        _ => ""
    };
}

/// <summary>Kampanya listesindeki satir.</summary>
public class KampanyaListeSatirVm
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string TipAdi { get; set; } = string.Empty;
    public string KapsamAdi { get; set; } = string.Empty;
    public int Oncelik { get; set; }
    public DateTime BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public bool Aktif { get; set; }
    public bool SuAnGecerli { get; set; }
}
