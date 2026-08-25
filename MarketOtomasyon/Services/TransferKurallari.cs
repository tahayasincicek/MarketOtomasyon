using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Depolar arasi transferin kurallari. Veritabani bilmez, saf hesaptir.
///
/// BAKIYE KONTROLU BURADA YOK ve bilerek yok: bakiye veritabanindan
/// okunur, ustelik kontrol ile tuketim arasinda baska bir satis araya
/// girebilir. Dogru yer MaliyetService.FifoTuketAsync - partileri
/// UPDLOCK ile okur ve yetersizse zaten hata doner.
/// </summary>
public static class TransferKurallari
{
    public static (bool Gecerli, string? Hata) DepolarGecerliMi(int kaynakDepoId, int hedefDepoId)
    {
        if (kaynakDepoId <= 0) return (false, "Kaynak depo seçilmedi.");
        if (hedefDepoId <= 0) return (false, "Hedef depo seçilmedi.");

        if (kaynakDepoId == hedefDepoId)
            return (false, "Kaynak ve hedef depo aynı olamaz.");

        return (true, null);
    }

    public static (bool Gecerli, string? Hata) SatirlarGecerliMi(
        IReadOnlyList<TransferSatirVm>? satirlar)
    {
        if (satirlar is null || satirlar.Count == 0)
            return (false, "Transfer edilecek en az bir ürün ekleyin.");

        foreach (var satir in satirlar)
        {
            if (satir.UrunId <= 0)
                return (false, "Geçersiz ürün.");

            if (satir.Miktar <= 0)
                return (false, $"{satir.UrunAd}: miktar sıfırdan büyük olmalıdır.");
        }

        // UQ_TransferSatir (TransferId, UrunId) ayni urunu ikinci kez kabul
        // etmez; ekran tekrari birlestirmeli, buraya gelmemeli.
        var tekrarEden = satirlar
            .GroupBy(s => s.UrunId)
            .FirstOrDefault(g => g.Count() > 1);

        if (tekrarEden is not null)
            return (false, $"{tekrarEden.First().UrunAd} listede birden fazla kez var. " +
                           "Aynı ürünü tek satırda toplayın.");

        return (true, null);
    }
}
