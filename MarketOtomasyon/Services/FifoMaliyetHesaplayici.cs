using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>Veritabanından bağımsız FIFO miktar ve maliyet dağıtıcısı.</summary>
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
