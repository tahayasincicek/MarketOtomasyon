using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Veritabanindan bagimsiz miktar ve maliyet dagiticisi.
///
/// SIRALAMA YAPMAZ: kendisine verilen listeyi bastan sona tuketir. Sira
/// MaliyetRepository.SqlAcikPartiler icindeki ORDER BY tarafindan
/// belirlenir ve artik FEFO'dur (son kullanma tarihi en yakin once).
/// Sinif adi FIFO diyor cunku once oyle yazildi; davranisi siradan
/// bagimsiz oldugu icin FEFO gecisinde degistirilmedi.
/// </summary>
public static class FifoMaliyetHesaplayici
{
    public static FifoTuketimSonucu Tuket(
        IReadOnlyList<StokPartiKalanVm> enEskidenYeniyePartiler,
        decimal istenenMiktar)
    {
        if (istenenMiktar <= 0)
            return FifoTuketimSonucu.Basarisiz("FIFO tüketim miktarı sıfırdan büyük olmalıdır.");

        var kalanIhtiyac = istenenMiktar;
        var tuketimler = new List<FifoTuketimVm>();

        foreach (var parti in enEskidenYeniyePartiler)
        {
            if (parti.KalanMiktar <= 0) continue;

            var miktar = Math.Min(kalanIhtiyac, parti.KalanMiktar);
            tuketimler.Add(new FifoTuketimVm
            {
                StokPartiId = parti.StokPartiId,
                Miktar = miktar,
                BirimMaliyet = parti.BirimMaliyet
            });

            kalanIhtiyac -= miktar;
            if (kalanIhtiyac == 0) break;
        }

        if (kalanIhtiyac > 0)
            return FifoTuketimSonucu.Basarisiz(
                $"FIFO parti bakiyesi yetersiz. Eksik miktar: {kalanIhtiyac:0.####}.");

        return new FifoTuketimSonucu { Tuketimler = tuketimler };
    }
}
