using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;

namespace TeraTools.Core.Colors;

public enum Harmony
{
    Monochromatic,
    Analogous,
    Complementary,
    SplitComplementary,
    Triadic,
    Tetradic,
    WarmNeutrals,
    CoolNeutrals,
    Gradient,
}

/// <summary>Geração de paletas de cores para temas, materiais e filtros de vista.</summary>
public static class PaletteGenerator
{
    public static List<RgbColor> Generate(RgbColor baseColor, Harmony harmony, int count, RgbColor? secondColor = null)
    {
        count = Math.Clamp(count, 1, 64);
        var (h, s, l) = baseColor.ToHsl();
        var list = new List<RgbColor>(count);
        switch (harmony)
        {
            case Harmony.Monochromatic:
                foreach (var t in MathUtil.Linspace(0.25, 0.85, count))
                    list.Add(RgbColor.FromHsl(h, s, t));
                break;
            case Harmony.Analogous:
                foreach (var t in MathUtil.Linspace(-40, 40, count))
                    list.Add(RgbColor.FromHsl(h + t, s, l));
                break;
            case Harmony.Gradient:
                var other = secondColor ?? RgbColor.FromHsl(h + 180, s, l);
                foreach (var t in MathUtil.Linspace(0, 1, count))
                    list.Add(RgbColor.Lerp(baseColor, other, t));
                break;
            case Harmony.WarmNeutrals:
                foreach (var t in MathUtil.Linspace(0.30, 0.92, count))
                    list.Add(RgbColor.FromHsl(32, 0.18, t));
                break;
            case Harmony.CoolNeutrals:
                foreach (var t in MathUtil.Linspace(0.30, 0.92, count))
                    list.Add(RgbColor.FromHsl(210, 0.10, t));
                break;
            default:
                var offsets = harmony switch
                {
                    Harmony.Complementary => new[] { 0.0, 180 },
                    Harmony.SplitComplementary => new[] { 0.0, 150, 210 },
                    Harmony.Triadic => new[] { 0.0, 120, 240 },
                    _ => new[] { 0.0, 90, 180, 270 },
                };
                for (var i = 0; i < count; i++)
                {
                    var hue = h + offsets[i % offsets.Length];
                    var round = i / offsets.Length;
                    var light = Math.Clamp(l + (round % 2 == 0 ? 1 : -1) * 0.12 * ((round + 1) / 2), 0.15, 0.9);
                    list.Add(RgbColor.FromHsl(hue, s, light));
                }
                break;
        }
        return list;
    }

    /// <summary>Paleta categórica com cores bem distintas (para filtros por valor de parâmetro).</summary>
    public static List<RgbColor> Distinct(int count, double saturation = 0.55, double lightness = 0.55)
    {
        var list = new List<RgbColor>(count);
        const double golden = 137.50776405;
        for (var i = 0; i < count; i++)
        {
            var l = lightness + (i % 3 - 1) * 0.08;
            list.Add(RgbColor.FromHsl(20 + i * golden, saturation, l));
        }
        return list;
    }

    public static string HarmonyName(Harmony h) => h switch
    {
        Harmony.Monochromatic => Loc.T("Monocromática", "Monochromatic"),
        Harmony.Analogous => Loc.T("Análoga", "Analogous"),
        Harmony.Complementary => Loc.T("Complementar", "Complementary"),
        Harmony.SplitComplementary => Loc.T("Complementar dividida", "Split complementary"),
        Harmony.Triadic => Loc.T("Tríade", "Triadic"),
        Harmony.Tetradic => Loc.T("Tétrade", "Tetradic"),
        Harmony.WarmNeutrals => Loc.T("Neutros quentes", "Warm neutrals"),
        Harmony.CoolNeutrals => Loc.T("Neutros frios", "Cool neutrals"),
        _ => Loc.T("Degradê", "Gradient"),
    };
}

/// <summary>Entrada de tema: valor/categoria → cor e aparência gráfica.</summary>
public sealed class ThemeEntry
{
    public string Key { get; set; } = "";
    public string ColorHex { get; set; } = "#808080";
    public string? MaterialName { get; set; }
    public int Transparency { get; set; }
    public bool Halftone { get; set; }
    public bool SolidFill { get; set; } = true;

    public RgbColor Color => RgbColor.TryParse(ColorHex, out var c) ? c : RgbColor.Gray;
}

/// <summary>Tema visual reutilizável (cores por categoria/valor, para vistas de apresentação).</summary>
public sealed class ColorTheme
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Parameter { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<ThemeEntry> Entries { get; set; } = new();
    public string BackgroundHex { get; set; } = "#FFFFFF";
    public bool HalftoneOthers { get; set; }

    public ThemeEntry? Find(string key) =>
        Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}
