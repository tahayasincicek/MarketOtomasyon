using MarketOtomasyon.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

public sealed class TedarikciService
{
    private readonly IDbConnectionFactory _factory;
    private readonly TedarikciRepository _tedarikciRepository;

    public TedarikciService(IDbConnectionFactory factory, TedarikciRepository tedarikciRepository)
    {
        _factory = factory;
        _tedarikciRepository = tedarikciRepository;
    }

    public async Task<IReadOnlyList<TedarikciSatirVm>> ListeleAsync(
        string? arama, bool sadeceAktif, CancellationToken ct = default)
        => await _tedarikciRepository.ListeleAsync(arama, sadeceAktif, ct);

    public async Task<Tedarikci?> GetirAsync(int id, CancellationToken ct = default)
        => await _tedarikciRepository.GetirAsync(id, ct);

    public async Task<string?> KaydetAsync(TedarikciFormVm form, CancellationToken ct = default)
    {
        var kod = form.Kod?.Trim() ?? "";
        var unvan = form.Unvan?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(kod)) return "Kod zorunludur.";
        if (string.IsNullOrWhiteSpace(unvan)) return "Unvan zorunludur.";

        var (vergiGecerli, vergiHatasi) = TedarikciKurallari.VergiNoGecerliMi(form.VergiNo);
        if (!vergiGecerli) return vergiHatasi;

        var hariciId = form.Id > 0 ? form.Id : (int?)null;
        if (await _tedarikciRepository.KodVarMiAsync(kod, hariciId, ct))
            return $"'{kod}' kodu zaten kayıtlı.";

        var tedarikci = new Tedarikci
        {
            Id = form.Id,
            Kod = kod,
            Unvan = unvan,
            VergiNo = string.IsNullOrWhiteSpace(form.VergiNo) ? null : form.VergiNo.Trim(),
            VergiDairesi = string.IsNullOrWhiteSpace(form.VergiDairesi) ? null : form.VergiDairesi.Trim(),
            Telefon = string.IsNullOrWhiteSpace(form.Telefon) ? null : form.Telefon.Trim(),
            Eposta = string.IsNullOrWhiteSpace(form.Eposta) ? null : form.Eposta.Trim(),
            Adres = string.IsNullOrWhiteSpace(form.Adres) ? null : form.Adres.Trim(),
            Aktif = form.Aktif
        };

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        if (form.Id > 0)
            await _tedarikciRepository.GuncelleAsync(conn, tx, tedarikci, ct);
        else
            await _tedarikciRepository.EkleAsync(conn, tx, tedarikci, ct);

        tx.Commit();
        return null;
    }
}
