namespace TeraTools.Core.Mathx;

public static class MathUtil
{
    public const double Deg = Math.PI / 180.0;

    public static double ToRadians(double degrees) => degrees * Deg;
    public static double ToDegrees(double radians) => radians / Deg;

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double InverseLerp(double a, double b, double v) =>
        Math.Abs(b - a) < 1e-15 ? 0 : (v - a) / (b - a);

    public static double Remap(double v, double inMin, double inMax, double outMin, double outMax) =>
        Lerp(outMin, outMax, InverseLerp(inMin, inMax, v));

    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    public static double SmoothStep(double t)
    {
        t = Clamp01(t);
        return t * t * (3 - 2 * t);
    }

    public static bool AlmostEqual(double a, double b, double tol = 1e-9) => Math.Abs(a - b) <= tol;

    /// <summary>Arredonda para o múltiplo mais próximo de <paramref name="step"/>.</summary>
    public static double RoundTo(double v, double step) => step <= 0 ? v : Math.Round(v / step) * step;

    public static double FloorTo(double v, double step) => step <= 0 ? v : Math.Floor(v / step + 1e-9) * step;

    public static double CeilTo(double v, double step) => step <= 0 ? v : Math.Ceiling(v / step - 1e-9) * step;

    /// <summary>Normaliza ângulo em graus para (-180, 180].</summary>
    public static double NormalizeDegrees(double a)
    {
        a %= 360;
        if (a <= -180) a += 360;
        if (a > 180) a -= 360;
        return a;
    }

    /// <summary>Distribui <paramref name="count"/> valores uniformemente entre a e b (inclusive).</summary>
    public static IEnumerable<double> Linspace(double a, double b, int count)
    {
        if (count <= 0) yield break;
        if (count == 1) { yield return (a + b) / 2; yield break; }
        for (var i = 0; i < count; i++)
            yield return a + (b - a) * i / (count - 1);
    }

    /// <summary>
    /// Quantos módulos de tamanho <paramref name="module"/> (com junta) cabem em <paramref name="length"/>.
    /// </summary>
    public static int FitCount(double length, double module, double gap = 0)
    {
        if (module <= 0) return 0;
        return Math.Max(0, (int)Math.Floor((length + gap) / (module + gap) + 1e-9));
    }
}
