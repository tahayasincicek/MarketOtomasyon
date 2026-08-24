using Microsoft.Extensions.Options;
using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

public class IadeService
{
    private const byte DurumTamamlandi = 2;
    private const byte OdemeTipNakit = 1;
    private const byte YonGiris = 1;
    private const byte KaynakIade = 2;

    private readonly IDbConnectionFactory _factory;
    private readonly IadeRepository _iadeRepository;
    private readonly StokRepository _stokRepository;
    private readonly DepoRepository _depoRepository;
    private readonly MaliyetService _maliyetService;
    private readonly IadeAyarlari _iadeAyarlari;
    private readonly SatisAyarlari _satisAyarlari;

    public IadeService(
        IDbConnectionFactory factory,
        IadeRepository iadeRepository,
        StokRepository stokRepository,
        DepoRepository depoRepository,
        MaliyetService maliyetService,
        IOptions<IadeAyarlari> iadeAyarlari,
        IOptions<SatisAyarlari> satisAyarlari)
    {
        _factory = factory;
        _iadeRepository = iadeRepository;
        _stokRepository = stokRepository;
        _depoRepository = depoRepository;
        _maliyetService = maliyetService;
        _iadeAyarlari = iadeAyarlari.Value;
        _satisAyarlari = satisAyarlari.Value;
    }

    /// <summary>Fis numarasiyla satis ve satir bazli kalan iade miktarlarini getirir.</summary>
    public async Task<IadeAramaVm> AraAsync(string? fisNo, CancellationToken ct = default)
    {
        var vm = new IadeAramaVm { FisNo = fisNo?.Trim() };
        if (string.IsNullOrWhiteSpace(vm.FisNo)) return vm;

        var fis = await _iadeRepository.FisGetirAsync(vm.FisNo, ct);
        if (fis is null)
        {
            vm.Hata = $"'{vm.FisNo}' numaralı fiş bulunamadı.";
            return vm;
        }

        FisBilgileriniTamamla(fis);
        vm.Fis = fis;
        vm.Form = new IadeFormVm
        {
            FisNo = fis.FisNo,
            Satirlar = fis.Satirlar
                .Select(s => new IadeTalepSatirVm { FisSatirId = s.FisSatirId })
                .ToList()
        };

        var dogrulamaHatasi = FisDogrulamaHatasi(fis, DateTime.UtcNow);
        if (fis.Durum == DurumTamamlandi && fis.TumUrunlerIadeEdildi)
            vm.Bilgi = "Fişteki tüm ürünlerin iadesi tamamlandı. İade edilebilecek ürün kalmadı.";
        else
            vm.Hata = dogrulamaHatasi;

        return vm;
    }

    /// <summary>
    /// Para iadesi, FisSatir.IadeEdilenMiktar ve stok girisini ayni transaction'da yazar.
    /// </summary>
    public async Task<IadeSonucVm> IadeEtAsync(
    IadeFormVm form, int kullaniciId, int vardiyaId, CancellationToken ct = default)
    {
        var fisNo = form.FisNo?.Trim();
        if (string.IsNullOrWhiteSpace(fisNo))
            return IadeSonucVm.Basarisiz("Fiş numarası zorunludur.");

        var talepler = form.Satirlar
            .Where(s => s.Miktar > 0)
            .GroupBy(s => s.FisSatirId)
            .Select(g => new IadeTalepSatirVm { FisSatirId = g.Key, Miktar = g.Sum(x => x.Miktar) })
            .ToList();

        if (talepler.Count == 0)
            return IadeSonucVm.Basarisiz("İade edilecek en az bir satır ve miktar girin.");

        var depoId = await _depoRepository.IdGetirAsync(_satisAyarlari.DepoKodu, ct);
        if (depoId is null)
            return IadeSonucVm.Basarisiz($"İade stoğunun gireceği depo bulunamadı: {_satisAyarlari.DepoKodu}");

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var fis = await _iadeRepository.IadeIcinFisGetirAsync(conn, tx, fisNo, ct);
        if (fis is null)
        {
            tx.Rollback();
            return IadeSonucVm.Basarisiz("Fiş bulunamadı.");
        }

        FisBilgileriniTamamla(fis);
        var fisHatasi = FisDogrulamaHatasi(fis, DateTime.UtcNow);
        if (fisHatasi is not null)
        {
            tx.Rollback();
            return IadeSonucVm.Basarisiz(fisHatasi);
        }

        var hesaplananlar = new List<(IadeFisSatirVm Satir, decimal Miktar, decimal Tutar)>();

        foreach (var talep in talepler)
        {
            var satir = fis.Satirlar.FirstOrDefault(s => s.FisSatirId == talep.FisSatirId);
            if (satir is null)
            {
                tx.Rollback();
                return IadeSonucVm.Basarisiz("Seçilen satır bu fişe ait değil.");
            }

            var (gecerli, hata) = IadeKurallari.MiktarDogrula(
                satir.Miktar, satir.IadeEdilenMiktar, talep.Miktar);
            if (!gecerli)
            {
                tx.Rollback();
                return IadeSonucVm.Basarisiz($"{satir.Ad}: {hata}");
            }

            var tutar = IadeKurallari.TutarHesapla(
                satir.Miktar,
                satir.SatirToplam,
                satir.IadeEdilenMiktar,
                satir.DahaOnceIadeTutari,
                talep.Miktar);

            hesaplananlar.Add((satir, talep.Miktar, tutar));
        }

        var toplamTutar = hesaplananlar.Sum(x => x.Tutar);
        var (iadeId, iadeNo) = await _iadeRepository.EkleAsync(conn, tx, new Iade
        {
            FisId = fis.FisId,
            KullaniciId = kullaniciId,
            VardiyaId = vardiyaId,
            ToplamTutar = toplamTutar,
            OdemeTipi = OdemeTipNakit,
            Aciklama = string.IsNullOrWhiteSpace(form.Aciklama) ? null : form.Aciklama.Trim()
        }, ct);

        foreach (var (satir, miktar, tutar) in hesaplananlar)
        {
            var guncellenen = await _iadeRepository.IadeMiktariArtirAsync(
                conn, tx, satir.FisSatirId, miktar, ct);
            if (guncellenen != 1)
            {
                tx.Rollback();
                return IadeSonucVm.Basarisiz(
                    $"{satir.Ad} için iade miktarı başka bir işlemde değişmiş. " +
                    "Fişi yeniden yükleyip kalan miktarı kontrol edin.");
            }

            var brutIade = decimal.Round(satir.BirimFiyat * miktar, 2, MidpointRounding.AwayFromZero);
            await _iadeRepository.SatirEkleAsync(conn, tx, new IadeSatir
            {
                IadeId = iadeId,
                FisSatirId = satir.FisSatirId,
                UrunId = satir.UrunId,
                Miktar = miktar,
                BirimFiyat = satir.BirimFiyat,
                IndirimTutari = Math.Max(0, brutIade - tutar),
                KdvOrani = satir.KdvOrani,
                Tutar = tutar
            }, ct);

            var hareketId = await _stokRepository.HareketEkleAsync(conn, tx, new StokHareket
            {
                UrunId = satir.UrunId,
                DepoId = depoId.Value,
                Yon = YonGiris,
                Miktar = miktar,
                KaynakTip = KaynakIade,
                KaynakId = iadeId,
                Aciklama = $"İade {iadeNo} / Fiş {fis.FisNo}"
            }, ct);

            var varsayilanMaliyet = satir.BirimFiyat / (1 + satir.KdvOrani / 100m);
            await _maliyetService.IadePartisiAcAsync(
                conn,
                tx,
                satir.UrunId,
                depoId.Value,
                hareketId,
                satir.FisSatirId,
                miktar,
                varsayilanMaliyet,
                $"İade {iadeNo}",
                ct);
        }

        tx.Commit();

        return new IadeSonucVm
        {
            Basarili = true,
            IadeId = iadeId,
            IadeNo = iadeNo,
            ToplamTutar = toplamTutar
        };
    }

    private void FisBilgileriniTamamla(IadeFisVm fis)
    {
        // SQL Server datetime2 saat dilimi bilgisi tasimaz; sema SYSUTCDATETIME
        // kullandigi icin degeri UTC olarak isaretleyip hem sureyi hem ekrani dogru hesapla.
        fis.Tarih = DateTime.SpecifyKind(fis.Tarih, DateTimeKind.Utc);
        fis.IadeSonTarihi = fis.Tarih.AddDays(_iadeAyarlari.SureGun);
    }

    private static string? FisDogrulamaHatasi(IadeFisVm fis, DateTime simdiUtc)
    {
        if (fis.Durum != DurumTamamlandi)
            return "Yalnızca tamamlanmış satış fişleri iade edilebilir.";

        if (simdiUtc > fis.IadeSonTarihi)
            return $"İade süresi {fis.IadeSonTarihi.ToLocalTime():dd.MM.yyyy} tarihinde dolmuş.";

        if (fis.Satirlar.Count == 0)
            return "Fişte iade edilebilecek satır yok.";

        if (fis.Satirlar.All(s => s.KalanMiktar <= 0))
            return "Fişteki tüm ürünler daha önce iade edilmiş.";

        return null;
    }
}
