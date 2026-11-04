using System.Globalization;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Units;

/// <summary>
/// Conversões de unidades. O Revit trabalha internamente em PÉS; o plugin guarda
/// comprimentos em pés e converte apenas para exibir/ler valores.
/// </summary>
public static class U
{
    public const double MmPerFoot = 304.8;
    public const double SqMPerSqFoot = 0.09290304;
    public const double CuMPerCuFoot = 0.028316846592;

    /// <summary>Milímetros → pés.</summary>
    public static double Mm(double mm) => mm / MmPerFoot;

    /// <summary>Centímetros → pés.</summary>
    public static double Cm(double cm) => cm * 10.0 / MmPerFoot;

    /// <summary>Metros → pés.</summary>
    public static double M(double m) => m * 1000.0 / MmPerFoot;

    public static double ToMm(double feet) => feet * MmPerFoot;
    public static double ToCm(double feet) => feet * MmPerFoot / 10.0;
    public static double ToM(double feet) => feet * MmPerFoot / 1000.0;

    public static double SqFtToM2(double sqft) => sqft * SqMPerSqFoot;
    public static double M2ToSqFt(double m2) => m2 / SqMPerSqFoot;
    public static double CuFtToM3(double cuft) => cuft * CuMPerCuFoot;
    public static double M3ToCuFt(double m3) => m3 / CuMPerCuFoot;

    public static string FormatMm(double feet, int decimals = 0) =>
        ToMm(feet).ToString("N" + decimals, Loc.Culture) + " mm";

    public static string FormatCm(double feet, int decimals = 1) =>
        ToCm(feet).ToString("N" + decimals, Loc.Culture) + " cm";

    public static string FormatM(double feet, int decimals = 2) =>
        ToM(feet).ToString("N" + decimals, Loc.Culture) + " m";

    public static string FormatArea(double sqft, int decimals = 2) =>
        SqFtToM2(sqft).ToString("N" + decimals, Loc.Culture) + " m²";

    public static string FormatVolume(double cuft, int decimals = 3) =>
        CuFtToM3(cuft).ToString("N" + decimals, Loc.Culture) + " m³";

    public static string FormatNumber(double v, int decimals = 2) => v.ToString("N" + decimals, Loc.Culture);

    public static string FormatMoney(double v) =>
        v.ToString("C2", Loc.IsEnglish ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// Lê um comprimento digitado em texto livre ("60", "60 cm", "0,6 m", "600mm", "2'").
    /// Sem unidade, assume <paramref name="defaultUnit"/>. Retorna pés.
    /// </summary>
    public static bool TryParseLength(string? text, LengthUnit defaultUnit, out double feet)
    {
        feet = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().ToLowerInvariant().Replace(" ", "");
        var unit = defaultUnit;
        string[] suffixes = ["mm", "cm", "m", "ft", "'", "in", "\"", "pés", "pes"];
        foreach (var suf in suffixes)
        {
            if (s.EndsWith(suf, StringComparison.Ordinal) && s.Length > suf.Length)
            {
                unit = suf switch
                {
                    "mm" => LengthUnit.Millimeters,
                    "cm" => LengthUnit.Centimeters,
                    "m" => LengthUnit.Meters,
                    "in" or "\"" => LengthUnit.Inches,
                    _ => LengthUnit.Feet,
                };
                s = s[..^suf.Length];
                break;
            }
        }
        if (!TryParseNumber(s, out var v)) return false;
        feet = unit switch
        {
            LengthUnit.Millimeters => Mm(v),
            LengthUnit.Centimeters => Cm(v),
            LengthUnit.Meters => M(v),
            LengthUnit.Inches => v / 12.0,
            _ => v,
        };
        return true;
    }

    /// <summary>Lê número aceitando vírgula ou ponto como separador decimal.</summary>
    public static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // O último separador é o decimal; o outro é separador de milhar.
            if (lastComma > lastDot) s = s.Replace(".", "").Replace(',', '.');
            else s = s.Replace(",", "");
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static string ToDisplay(double feet, LengthUnit unit, int decimals)
    {
        var v = unit switch
        {
            LengthUnit.Millimeters => ToMm(feet),
            LengthUnit.Centimeters => ToCm(feet),
            LengthUnit.Meters => ToM(feet),
            LengthUnit.Inches => feet * 12,
            _ => feet,
        };
        return v.ToString("0." + new string('#', Math.Max(0, decimals)), Loc.Culture);
    }

    public static string Suffix(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeters => "mm",
        LengthUnit.Centimeters => "cm",
        LengthUnit.Meters => "m",
        LengthUnit.Inches => "in",
        _ => "ft",
    };
}

public enum LengthUnit
{
    Millimeters,
    Centimeters,
    Meters,
    Feet,
    Inches,
}
