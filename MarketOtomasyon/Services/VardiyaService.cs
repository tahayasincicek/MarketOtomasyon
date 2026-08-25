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

    /// <summary>
    /// Kasada makul olarak bulunabilecek tavan. Amaci tasmayi onlemek degil,
    /// parmak hatasini yakalamak: 500 yerine 50000 yazilirsa gun sonunda
    /// kasa aciği o kadar buyuk cikar ve hata ancak kapanista fark edilir.
    /// </summary>
    private const decimal TutarTavani = 100_000m;

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
            return "Açılış tutarı negatif olamaz.";

        if (acilisTutari > TutarTavani)
            return $"Açılış tutarı {TutarTavani:N0} TL'yi aşamaz. Girdiğiniz tutarı kontrol edin.";

        if (await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct) is not null)
            return "Zaten açık bir vardiyanız var. Önce onu kapatın.";

        await _vardiyaRepository.AcAsync(kullaniciId, acilisTutari, ct);
        return null;
    }

    /// <summary>Basarili olursa kapanmis vardiyanin Z raporunu dondurur.</summary>
    public async Task<(ZRaporVm? Rapor, string? Hata)> KapatAsync(
        int kullaniciId, decimal sayilanTutar, CancellationToken ct = default)
    {
        if (sayilanTutar < 0)
            return (null, "Sayılan tutar negatif olamaz.");

        if (sayilanTutar > TutarTavani)
            return (null, $"Sayılan tutar {TutarTavani:N0} TL'yi aşamaz. Kasayı yeniden sayın.");

        var acik = await _vardiyaRepository.AcikVardiyaGetirAsync(kullaniciId, ct);
        if (acik is null)
            return (null, "Açık vardiya bulunamadı.");

        var rapor = await _vardiyaRepository.ZRaporAsync(acik.Id, ct);
        if (rapor is null)
            return (null, "Vardiya özeti okunamadı.");

        rapor.SayilanTutar = sayilanTutar;
        var beklenen = rapor.BeklenenTutar;
        var fark = sayilanTutar - beklenen;

        var etkilenen = await _vardiyaRepository.KapatAsync(acik.Id, sayilanTutar, beklenen, fark, ct);
        if (etkilenen != 1)
            return (null, "Vardiya başka bir işlemde kapatılmış.");

        return (await _vardiyaRepository.ZRaporAsync(acik.Id, ct), null);
    }

    public Task<ZRaporVm?> RaporAsync(int vardiyaId, CancellationToken ct = default)
        => _vardiyaRepository.ZRaporAsync(vardiyaId, ct);
}
