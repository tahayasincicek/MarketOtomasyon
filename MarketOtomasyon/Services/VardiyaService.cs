using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>
/// Vardiya acma/kapatma ve Z raporu.
/// Beklenen tutar tek yerde (ZRaporVm.BeklenenTutar) hesaplanir; kapanista
/// o deger Vardiya satirina donduruluyor ki rapor sonradan degismesin.
/// </summary>
public class VardiyaService
{
    private const int SonKapananAdet = 10;

    private readonly VardiyaRepository _vardiyaRepository;

    public VardiyaService(VardiyaRepository vardiyaRepository) => _vardiyaRepository = vardiyaRepository;

    public async Task<VardiyaEkranVm> EkranAsync(int kullaniciId, CancellationToken ct = default)
    {
        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        return new VardiyaEkranVm
        {
            Acik = acik is null ? null : await _vardiyaRepository.ZRaporAsync(acik.Id, ct),
            SonKapananlar = await _vardiyaRepository.SonKapananlarAsync(SonKapananAdet, ct)
        };
    }

    public async Task<string?> AcAsync(int kullaniciId, decimal acilisTutari, CancellationToken ct = default)
    {
        if (acilisTutari < 0)
            return "Acilis tutari negatif olamaz.";

        if (await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct) is not null)
            return "Zaten acik bir vardiyaniz var. Once onu kapatin.";

        await _vardiyaRepository.AcAsync(kullaniciId, acilisTutari, ct);
        return null;
    }

    /// <summary>Basarili olursa kapanmis vardiyanin Z raporunu dondurur.</summary>
    public async Task<(ZRaporVm? Rapor, string? Hata)> KapatAsync(
        int kullaniciId, decimal sayilanTutar, CancellationToken ct = default)
    {
        if (sayilanTutar < 0)
            return (null, "Sayilan tutar negatif olamaz.");

        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        if (acik is null)
            return (null, "Acik vardiya bulunamadi.");

        var rapor = await _vardiyaRepository.ZRaporAsync(acik.Id, ct);
        if (rapor is null)
            return (null, "Vardiya ozeti okunamadi.");

        rapor.SayilanTutar = sayilanTutar;
        var beklenen = rapor.BeklenenTutar;
        var fark = sayilanTutar - beklenen;

        var etkilenen = await _vardiyaRepository.KapatAsync(acik.Id, sayilanTutar, beklenen, fark, ct);
        if (etkilenen != 1)
            return (null, "Vardiya baska bir islemde kapatilmis.");

        return (await _vardiyaRepository.ZRaporAsync(acik.Id, ct), null);
    }

    public Task<ZRaporVm?> RaporAsync(int vardiyaId, CancellationToken ct = default)
        => _vardiyaRepository.ZRaporAsync(vardiyaId, ct);
}