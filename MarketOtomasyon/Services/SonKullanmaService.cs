using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>Son kullanma tarihi takip ekraninin veri kaynagi.</summary>
public sealed class SonKullanmaService
{
    private readonly SonKullanmaRepository _sonKullanmaRepository;
    private readonly DepoRepository _depoRepository;

    public SonKullanmaService(
        SonKullanmaRepository sonKullanmaRepository, DepoRepository depoRepository)
    {
        _sonKullanmaRepository = sonKullanmaRepository;
        _depoRepository = depoRepository;
    }

    public async Task<SonKullanmaEkranVm> EkranAsync(
        int gunSayisi, int? depoId, CancellationToken ct = default)
    {
        gunSayisi = SonKullanmaKurallari.GunSayisiniKirp(gunSayisi);

        return new SonKullanmaEkranVm
        {
            GunSayisi = gunSayisi,
            DepoId = depoId,
            Depolar = await _depoRepository.AktifleriGetirAsync(ct),
            Satirlar = await _sonKullanmaRepository.ListeleAsync(
                gunSayisi, depoId, DateTime.Today, ct)
        };
    }
}
