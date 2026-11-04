using System.Globalization;

namespace TeraTools.Core.Colors;

/// <summary>Cor RGB de 8 bits por canal.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static readonly RgbColor White = new(255, 255, 255);
    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor Gray = new(128, 128, 128);

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public static RgbColor FromHex(string? hex)
    {
        if (!TryParse(hex, out var c))
            throw new FormatException("Cor inválida: " + hex);
        return c;
    }

    public static bool TryParse(string? text, out RgbColor color)
    {
        color = Gray;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 3) s = string.Concat(s.Select(ch => new string(ch, 2)));
        if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            color = new RgbColor((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }
        // "r,g,b" ou "r;g;b"
        var parts = text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts.All(p => byte.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            color = new RgbColor(byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture));
            return true;
        }
        return false;
    }

    /// <summary>Luminância relativa (0..1) segundo WCAG.</summary>
    public double RelativeLuminance
    {
        get
        {
            static double Lin(byte c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Lin(R) + 0.7152 * Lin(G) + 0.0722 * Lin(B);
        }
    }

    /// <summary>Brilho perceptual simples (0..1).</summary>
    public double Brightness => (0.2126 * R + 0.7152 * G + 0.0722 * B) / 255.0;

    public static double ContrastRatio(RgbColor a, RgbColor b)
    {
        var la = a.RelativeLuminance + 0.05;
        var lb = b.RelativeLuminance + 0.05;
        return la > lb ? la / lb : lb / la;
    }

    public double DistanceTo(RgbColor o)
    {
        // Distância "redmean": aproximação perceptual barata.
        var rm = (R + o.R) / 2.0;
        var dr = R - o.R;
        var dg = G - o.G;
        var db = B - o.B;
        return Math.Sqrt((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db);
    }

    public static RgbColor Lerp(RgbColor a, RgbColor b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new RgbColor(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Converte para HSL (H em graus 0..360, S e L 0..1).</summary>
    public (double H, double S, double L) ToHsl()
    {
        var r = R / 255.0;
        var g = G / 255.0;
        var b = B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0, s = 0;
        var d = max - min;
        if (d > 1e-12)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }
        return (h, s, l);
    }

    public static RgbColor FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360 / 360.0;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        double r, g, b;
        if (s < 1e-12)
        {
            r = g = b = l;
        }
        else
        {
            static double Hue(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3);
            g = Hue(p, q, h);
            b = Hue(p, q, h - 1.0 / 3);
        }
        return new RgbColor((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    public RgbColor WithLightness(double l)
    {
        var (h, s, _) = ToHsl();
        return FromHsl(h, s, l);
    }

    public override string ToString() => Hex;
}
