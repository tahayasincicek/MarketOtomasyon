using System.Net;

namespace MarketOtomasyon.Web;

/// <summary>
/// Uygulama bir ters vekil (nginx, IIS ARR, Cloudflare) arkasinda
/// calisiyorsa gereken ayarlar.
///
/// Vekil TLS'i kendi sonlandirir ve uygulamaya duz HTTP ile baglanir.
/// Bu durumda Request.IsHttps her zaman false doner; UseHttpsRedirection
/// istegi HTTPS'e yonlendirir, vekil yine HTTP olarak iletir ve
/// yonlendirme dongusu olusur. Ayrica HSTS basligi hic eklenmez, cunku
/// o da istegi HTTP saniyor.
///
/// Cozum, vekilin ekledigi X-Forwarded-Proto ve X-Forwarded-For
/// basliklarini okumak. Ancak bu basliklara kosulsuz guvenilmez: dogrudan
/// erisebilen biri X-Forwarded-Proto: https gondererek uygulamayi
/// baglantinin sifreli oldugu konusunda yanlislar. Bu yuzden ozellik
/// varsayilan olarak KAPALI ve acildiginda hangi kaynaklara guvenilecegi
/// belirtilir.
/// </summary>
public sealed class TersProxyAyarlari
{
    public const string Bolum = "TersProxy";

    /// <summary>
    /// Vekil basliklari islensin mi. Vekil kullanilmiyorsa false
    /// kalmali; aksi halde istemcinin gonderdigi basliklar dikkate
    /// alinir.
    /// </summary>
    public bool Etkin { get; set; }

    /// <summary>
    /// Basliklarina guvenilecek vekil adresleri (orn. "10.0.0.5").
    /// Vekil uygulamayla ayni makinedeyse gerekmez; loopback zaten
    /// guvenilir kabul edilir.
    /// </summary>
    public string[] GuvenilenProxyler { get; set; } = [];

    /// <summary>
    /// Guvenilecek aglar, CIDR biciminde (orn. "10.0.0.0/8").
    /// Konteyner ortamlarinda vekilin adresi her baslangicta
    /// degisebildiginden tek tek adres yerine ag vermek pratiktir.
    /// </summary>
    public string[] GuvenilenAglar { get; set; } = [];

    /// <summary>
    /// Tum kaynaklardan gelen vekil basliklarina guvenilsin mi?
    /// Yalnizca uygulama portuna disaridan dogrudan erisimin guvenlik
    /// duvari ile kesin olarak engellendigi yonetilen ortamlarda acilir.
    /// Bos guven listesi tek basina bunu ACMAZ; yanlis ayar fail-closed
    /// davranmali.
    /// </summary>
    public bool TumProxylereGuven { get; set; }

    public bool KaynakBelirtilmemis
        => GuvenilenProxyler.Length == 0 && GuvenilenAglar.Length == 0;

    /// <summary>
    /// Guvenlik yapilandirmasini dogrular. Gecersiz bir degeri atlayip
    /// devam etmek tehlikelidir: tek adres de hataliysa guven listesi
    /// bosalir ve middleware tum kaynaklara guvenebilir.
    /// </summary>
    public IReadOnlyList<string> DogrulamaHatalari()
    {
        if (!Etkin) return [];

        var hatalar = new List<string>();
        var (_, gecersizProxyler) = ProxyleriCoz();
        var (_, gecersizAglar) = AglariCoz();

        hatalar.AddRange(gecersizProxyler.Select(x => $"Geçersiz proxy adresi: {x}"));
        hatalar.AddRange(gecersizAglar.Select(x => $"Geçersiz proxy ağı: {x}"));

        if (KaynakBelirtilmemis && !TumProxylereGuven)
        {
            hatalar.Add(
                "Ters proxy etkin ancak güvenilen proxy/ağ belirtilmemiş. " +
                "GuvenilenProxyler veya GuvenilenAglar ayarlayın; yalnızca izole " +
                "bir ağdaysanız TumProxylereGuven seçeneğini açın.");
        }

        if (!KaynakBelirtilmemis && TumProxylereGuven)
            hatalar.Add("TumProxylereGuven ile güvenilen adres listeleri birlikte kullanılamaz.");

        return hatalar;
    }

    /// <summary>
    /// Metin olarak verilen adresleri ayristirir. Gecersiz olanlar
    /// sessizce atlanmaz; cagiran taraf uyari loglayabilsin diye ayri
    /// listede doner.
    /// </summary>
    public (List<IPAddress> Gecerli, List<string> Gecersiz) ProxyleriCoz()
    {
        var gecerli = new List<IPAddress>();
        var gecersiz = new List<string>();

        foreach (var metin in GuvenilenProxyler)
        {
            if (IPAddress.TryParse(metin?.Trim(), out var adres))
                gecerli.Add(adres);
            else
                gecersiz.Add(metin ?? "(bos)");
        }

        return (gecerli, gecersiz);
    }

    /// <summary>
    /// CIDR biciminde verilen aglari ayristirir: "10.0.0.0/8".
    /// </summary>
    public (List<(IPAddress Adres, int Uzunluk)> Gecerli, List<string> Gecersiz) AglariCoz()
    {
        var gecerli = new List<(IPAddress, int)>();
        var gecersiz = new List<string>();

        foreach (var metin in GuvenilenAglar)
        {
            var parcalar = (metin ?? string.Empty).Split('/', StringSplitOptions.TrimEntries);

            if (parcalar.Length == 2
                && IPAddress.TryParse(parcalar[0], out var adres)
                && int.TryParse(parcalar[1], out var uzunluk)
                && uzunluk >= 0
                && uzunluk <= (adres.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32))
            {
                gecerli.Add((adres, uzunluk));
            }
            else
            {
                gecersiz.Add(metin ?? "(bos)");
            }
        }

        return (gecerli, gecersiz);
    }
}
