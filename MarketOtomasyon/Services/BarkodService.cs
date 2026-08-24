using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Okutulan barkodu satisa hazir bir sepet satirina cevirir.
/// Miktarin nereden geldigi barkodun tipine gore degisir.
/// </summary>
public class BarkodService
{
    private readonly IBarkodRepository _barkodRepository;

    public BarkodService(IBarkodRepository barkodRepository) => _barkodRepository = barkodRepository;

    public async Task<BarkodSonucVm> CozAsync(string? barkod, CancellationToken ct = default)
    {
        barkod = barkod?.Trim();
        if (string.IsNullOrEmpty(barkod))
            return BarkodSonucVm.Basarisiz("Barkod boş olamaz.");

        // Terazi barkodu once denenir: 13 hane oldugu icin normal EAN gibi gorunur,
        // ama tamami veritabaninda kayitli degildir; yalnizca ilk 7 hanesi aranir.
        if (BarkodCozumleyici.TeraziBarkoduMu(barkod))
            return await TeraziCozAsync(barkod, ct);

        // 13 haneli normal barkodlarda kontrol hanesi dogrulanir: okuyucunun
        // yanlis okumasi ya da elle yanlis girilmesi burada yakalanir.
        if (barkod.Length == 13 && barkod.All(char.IsAsciiDigit) && !BarkodCozumleyici.Ean13Gecerli(barkod))
            return BarkodSonucVm.Basarisiz("Barkod kontrol hanesi tutmuyor, tekrar okutun.");

        var urun = await _barkodRepository.BarkodCozAsync(barkod, ct);
        if (urun is null)
            return BarkodSonucVm.Basarisiz($"'{barkod}' barkodlu ürün bulunamadı.");

        if (urun.Fiyat is null)
            return BarkodSonucVm.Basarisiz($"{urun.Ad} için fiyat tanımlı değil.");

        // Koli barkodunda carpan kadar adet sepete girer, tekli barkodda 1.
        var miktar = urun.BarkodTip == 2 ? urun.Carpan : 1m;

        return Sonuc(urun, miktar, urun.BarkodTip == 2 ? "koli" : "tekli");
    }

    private async Task<BarkodSonucVm> TeraziCozAsync(string barkod, CancellationToken ct)
    {
        if (!BarkodCozumleyici.Ean13Gecerli(barkod))
            return BarkodSonucVm.Basarisiz("Terazi barkodunun kontrol hanesi tutmuyor.");

        var (anahtar, miktarKg) = BarkodCozumleyici.TeraziAyristir(barkod);

        var urun = await _barkodRepository.BarkodCozAsync(anahtar, ct);
        if (urun is null)
            return BarkodSonucVm.Basarisiz($"Terazi kodu '{anahtar}' hiçbir ürüne tanımlı değil.");

        if (urun.Fiyat is null)
            return BarkodSonucVm.Basarisiz($"{urun.Ad} için fiyat tanımlı değil.");

        if (miktarKg <= 0)
            return BarkodSonucVm.Basarisiz("Terazi barkodundaki gramaj sıfır.");

        return Sonuc(urun, miktarKg, "terazi");
    }

    private static BarkodSonucVm Sonuc(BarkodCozumVm urun, decimal miktar, string tip) => new()
    {
        Basarili = true,
        UrunId = urun.UrunId,
        Kod = urun.Kod,
        Ad = urun.Ad,
        Birim = urun.Birim,
        KdvOrani = urun.KdvOrani,
        Miktar = miktar,
        BirimFiyat = urun.Fiyat!.Value,
        SatirToplam = decimal.Round(miktar * urun.Fiyat.Value, 2, MidpointRounding.AwayFromZero),
        BarkodTipi = tip
    };
}
