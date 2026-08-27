namespace MarketOtomasyon.Web;

public sealed class GirisGuvenligiAyarlari
{
    public const string Bolum = "GirisGuvenligi";

    public int IzinSayisi { get; set; } = 5;
    public int PencereSaniye { get; set; } = 60;
    public int DilimSayisi { get; set; } = 6;

    public IReadOnlyList<string> DogrulamaHatalari()
    {
        var hatalar = new List<string>();

        if (IzinSayisi < 1)
            hatalar.Add("GirisGuvenligi:IzinSayisi en az 1 olmalıdır.");
        if (PencereSaniye < 1)
            hatalar.Add("GirisGuvenligi:PencereSaniye en az 1 olmalıdır.");
        if (DilimSayisi < 1 || DilimSayisi > PencereSaniye)
            hatalar.Add("GirisGuvenligi:DilimSayisi 1 ile pencere süresi arasında olmalıdır.");

        return hatalar;
    }
}
