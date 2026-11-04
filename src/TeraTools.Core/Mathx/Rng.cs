namespace TeraTools.Core.Mathx;

/// <summary>
/// Gerador pseudoaleatório determinístico (SplitMix64). A mesma semente sempre gera
/// o mesmo resultado — essencial para "variação aleatória controlada" reproduzível.
/// </summary>
public sealed class Rng
{
    private ulong _state;

    public Rng(int seed) => _state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Número em [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Número em [min, max).</summary>
    public double Range(double min, double max) => min + (max - min) * NextDouble();

    public int Next(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(NextDouble() * maxExclusive);

    /// <summary>Distribuição normal (Box–Muller).</summary>
    public double Gaussian(double mean = 0, double stdDev = 1)
    {
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        return mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Valor pseudoaleatório estável em [0,1) para uma célula (i, j) — não depende da ordem.</summary>
    public static double Hash(int i, int j, int seed)
    {
        unchecked
        {
            var h = (ulong)(uint)i * 0x8DA6B343UL ^ (ulong)(uint)j * 0xD8163841UL ^ (ulong)(uint)seed * 0xCB1AB31FUL;
            h ^= h >> 33;
            h *= 0xFF51AFD7ED558CCDUL;
            h ^= h >> 33;
            h *= 0xC4CEB9FE1A85EC53UL;
            h ^= h >> 33;
            return (h >> 11) * (1.0 / (1UL << 53));
        }
    }

    /// <summary>Ruído de valor 2D suave (interpolado) em [0,1), útil para variações orgânicas.</summary>
    public static double ValueNoise(double x, double y, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = MathUtil.SmoothStep(x - x0);
        var fy = MathUtil.SmoothStep(y - y0);
        var a = Hash(x0, y0, seed);
        var b = Hash(x0 + 1, y0, seed);
        var c = Hash(x0, y0 + 1, seed);
        var d = Hash(x0 + 1, y0 + 1, seed);
        return MathUtil.Lerp(MathUtil.Lerp(a, b, fx), MathUtil.Lerp(c, d, fx), fy);
    }
}
