namespace MarketOtomasyon.Services;

/// <summary>Sayim farkini stok hareketinin yon ve miktarina cevirir.</summary>
public static class SayimKurallari
{
    public record Duzeltme(decimal Fark, byte? Yon, decimal Miktar)
    {
        public bool HareketGerekli => Yon.HasValue;
    }

    public static Duzeltme DuzeltmeHesapla(decimal sistemMiktari, decimal sayilanMiktar)
    {
        if (sayilanMiktar < 0)
            throw new ArgumentOutOfRangeException(nameof(sayilanMiktar),
                "Sayılan miktar sıfırdan küçük olamaz.");

        var fark = sayilanMiktar - sistemMiktari;
        if (fark > 0) return new Duzeltme(fark, 1, fark);
        if (fark < 0) return new Duzeltme(fark, 2, Math.Abs(fark));
        return new Duzeltme(0, null, 0);
    }
}
