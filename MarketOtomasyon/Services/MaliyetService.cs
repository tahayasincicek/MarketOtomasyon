using System.Data;
using MarketOtomasyon.Data.Repositories;
using MarketOtomasyon.Models.Entities;
using MarketOtomasyon.Models.ViewModels;

namespace MarketOtomasyon.Services;

/// <summary>Parti açma ve transaction içi FIFO tüketim işlemleri.</summary>
public sealed class MaliyetService
{
    private readonly MaliyetRepository _maliyetRepository;

    public MaliyetService(MaliyetRepository maliyetRepository)
        => _maliyetRepository = maliyetRepository;

    public async Task<long> PartiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        decimal miktar,
        decimal birimMaliyet,
        string? aciklama,
        DateTime? sonKullanmaTarihi = null,
        string? lotNo = null,
        int? tedarikciId = null,
        int? alisFaturasiSatirId = null,
        CancellationToken ct = default)
    {
        if (miktar <= 0)
            throw new ArgumentOutOfRangeException(nameof(miktar));
        if (birimMaliyet < 0)
            throw new ArgumentOutOfRangeException(nameof(birimMaliyet));

        return await _maliyetRepository.PartiEkleAsync(conn, tx, new StokParti
        {
            UrunId = urunId,
            DepoId = depoId,
            StokHareketId = stokHareketId,
            GirisMiktari = miktar,
            KalanMiktar = miktar,
            BirimMaliyet = birimMaliyet,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Mal kabul" : aciklama.Trim(),

            /* Bos metin null'a cevriliyor: aksi halde tabloda "" ile NULL
               karisik durur ve "lotu olmayan partiler" sorgusu iki kosul
               yazmayi gerektirir. */
            SonKullanmaTarihi = sonKullanmaTarihi?.Date,
            LotNo = string.IsNullOrWhiteSpace(lotNo) ? null : lotNo.Trim(),
            TedarikciId = tedarikciId,
            AlisFaturasiSatirId = alisFaturasiSatirId
        }, ct);
    }

    /// <param name="gecerlilikGunu">
    /// Doluysa suresi bu gunden once dolan partiler hic goruntulenmez;
    /// satis bunu gecer. Zayi, transfer ve sayim duzeltmesi NULL birakir
    /// cunku onlarin suresi gecmis partiye erisebilmesi gerekir.
    /// </param>
    public async Task<FifoTuketimSonucu> FifoTuketAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        int? fisSatirId,
        decimal miktar,
        DateTime? gecerlilikGunu = null,
        CancellationToken ct = default)
    {
        var partiler = await _maliyetRepository.AcikPartileriGetirAsync(
            conn, tx, urunId, depoId, gecerlilikGunu, ct: ct);
        var sonuc = FifoMaliyetHesaplayici.Tuket(partiler, miktar);
        if (!sonuc.Basarili) return sonuc;

        foreach (var tuketim in sonuc.Tuketimler)
            await _maliyetRepository.TuketimYazAsync(
                conn, tx, stokHareketId, fisSatirId, tuketim, ct);

        return sonuc;
    }

    /// <summary>
    /// Suresi gecmis ve dusulmemis bakiye. Satis reddedildiginde
    /// kasiyere "stok yok" yerine gercek sebebi soyleyebilmek icin.
    /// </summary>
    public async Task<decimal> SuresiGecmisBakiyeAsync(
        IDbConnection conn, IDbTransaction tx, int urunId, int depoId, DateTime bugun,
        CancellationToken ct = default)
        => await _maliyetRepository.SuresiGecmisBakiyeAsync(conn, tx, urunId, depoId, bugun, ct);

    /// <summary>
    /// Coklu urun icin ayni bilgi; kasa sepetindeki uyari rozetini
    /// besler. Tek sorgu, transaction disi.
    /// </summary>
    public async Task<Dictionary<int, decimal>> SuresiGecmisBakiyeleriAsync(
        int depoId, IReadOnlyCollection<int> urunIdler, DateTime bugun,
        CancellationToken ct = default)
        => await _maliyetRepository.SuresiGecmisBakiyeleriAsync(depoId, urunIdler, bugun, ct);

    /// <summary>
    /// BELIRLI bir partiyi tuketir; FEFO sirasina bakmaz.
    ///
    /// Son kullanma ekranindaki "zayi'ye al" bunu kullanir. FifoTuketAsync
    /// kullanilamaz: o, urun+depo icin sirali ilk partiyi secer. Kullanici
    /// ekranda 15 Mart lotunu isaretlemisken sistemin 20 Mart lotunu
    /// dusurmesi, sayilan mal ile kayit arasinda sessiz bir fark birakirdi.
    ///
    /// Miktar kontrolu SQL tarafinda: TuketimYaz, KalanMiktar yetmezse
    /// THROW eder, yani es zamanli iki dusum yarisinda ikincisi kesilir.
    /// </summary>
    public async Task PartiTuketAsync(
        IDbConnection conn,
        IDbTransaction tx,
        long stokPartiId,
        decimal miktar,
        decimal birimMaliyet,
        long stokHareketId,
        CancellationToken ct = default)
        => await _maliyetRepository.TuketimYazAsync(
            conn, tx, stokHareketId, fisSatirId: null,
            new FifoTuketimVm
            {
                StokPartiId = stokPartiId,
                Miktar = miktar,
                BirimMaliyet = birimMaliyet
            },
            ct);

    public async Task<long> DuzeltmePartisiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        decimal miktar,
        string aciklama,
        CancellationToken ct = default)
    {
        var ortalamaMaliyet = await _maliyetRepository.OrtalamaAcikMaliyetAsync(
            conn, tx, urunId, depoId, ct);

        return await PartiAcAsync(
            conn, tx, urunId, depoId, stokHareketId, miktar, ortalamaMaliyet, aciklama, ct: ct);
    }

    /// <remarks>
    /// Iade partisinde SonKullanmaTarihi bilerek NULL birakilir.
    ///
    /// Teorik olarak orijinal partiden okunabilirdi (StokPartiTuketim ->
    /// StokParti) ama musteri hangi partiden aldigini soylemez ve iade
    /// edilen urunun raf omrunun neresinde oldugu belirsizdir. Uydurulmus
    /// bir tarih FEFO siralamasini yanlis yonlendirir; NULL birakmak
    /// "bilmiyorum" demenin durust yolu ve o parti sirada sona duser.
    /// </remarks>
    public async Task<long> IadePartisiAcAsync(
        IDbConnection conn,
        IDbTransaction tx,
        int urunId,
        int depoId,
        long stokHareketId,
        int fisSatirId,
        decimal miktar,
        decimal varsayilanBirimMaliyet,
        string aciklama,
        CancellationToken ct = default)
    {
        var satisMaliyeti = await _maliyetRepository.FisSatirBirimMaliyetiAsync(
            conn, tx, fisSatirId, ct);
        var birimMaliyet = satisMaliyeti ?? Math.Max(0, varsayilanBirimMaliyet);

        return await PartiAcAsync(
            conn, tx, urunId, depoId, stokHareketId, miktar, birimMaliyet, aciklama, ct: ct);
    }
}
