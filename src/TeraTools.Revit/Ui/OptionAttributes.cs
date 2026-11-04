using System.Reflection;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Ui;

/// <summary>
/// Marca uma propriedade de uma classe de opções para aparecer no diálogo.
/// Comprimentos ficam em pés (unidade interna do Revit) e são exibidos nas unidades do projeto.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OptAttribute : Attribute
{
    public OptAttribute(string pt, string en)
    {
        Pt = pt;
        En = en;
    }

    public string Pt { get; }
    public string En { get; }

    /// <summary>Grupo no formato "pt|en".</summary>
    public string Group { get; set; } = "";

    /// <summary>Dica (tooltip) no formato "pt|en".</summary>
    public string Tip { get; set; } = "";

    public OptKind Kind { get; set; } = OptKind.Auto;
    public double Min { get; set; } = double.NaN;
    public double Max { get; set; } = double.NaN;

    /// <summary>"Prop" (bool verdadeiro), "!Prop", "Prop=A,B" ou "Prop!=A".</summary>
    public string VisibleIf { get; set; } = "";

    /// <summary>Filtro: categorias (OST_...) para tipos de família, tipos de vista, ou filtro de arquivo.</summary>
    public string Filter { get; set; } = "";

    public int Decimals { get; set; } = 2;
    public string Suffix { get; set; } = "";

    /// <summary>Campo de escolha obrigatório (sem opção "nenhum").</summary>
    public bool Required { get; set; }

    public string Label => Loc.T(Pt, En);

    public string? TipText => string.IsNullOrEmpty(Tip) ? null : Split(Tip);

    public string? GroupText => string.IsNullOrEmpty(Group) ? null : Split(Group);

    public static string Split(string s)
    {
        var p = s.Split('|');
        return p.Length > 1 && Loc.IsEnglish ? p[1] : p[0];
    }
}

/// <summary>Texto bilíngue para valores de enum definidos no add-in.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class TextAttribute : Attribute
{
    public TextAttribute(string pt, string en)
    {
        Pt = pt;
        En = en;
    }

    public string Pt { get; }
    public string En { get; }
}

/// <summary>Classes de opções podem validar combinações de valores.</summary>
public interface IValidatableOptions
{
    IEnumerable<string> Validate();
}

/// <summary>Rótulos de enums (inclusive os do núcleo, que não conhecem o Revit).</summary>
public static class EnumLabels
{
    private static readonly Dictionary<Type, Dictionary<string, Txt>> Map = new();

    public static void Register<T>(params (T Value, string Pt, string En)[] items) where T : struct, Enum
    {
        var d = new Dictionary<string, Txt>();
        foreach (var (v, pt, en) in items) d[v.ToString()] = new Txt(pt, en);
        Map[typeof(T)] = d;
    }

    public static string Label(Type enumType, object value)
    {
        var name = value.ToString() ?? "";
        if (Map.TryGetValue(enumType, out var d) && d.TryGetValue(name, out var t)) return t.Value;
        var f = enumType.GetField(name);
        var a = f?.GetCustomAttribute<TextAttribute>();
        return a != null ? Loc.T(a.Pt, a.En) : name;
    }
}
