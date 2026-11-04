using System.Globalization;

namespace TeraTools.Core.Localization;

public enum Language
{
    Portuguese,
    English,
}

/// <summary>
/// Localização simples e segura: cada texto é escrito nos dois idiomas no próprio ponto de uso,
/// então nunca existe "chave faltando". O idioma é definido na inicialização do add-in.
/// </summary>
public static class Loc
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static Language Current { get; set; } = Language.Portuguese;

    public static bool IsEnglish => Current == Language.English;

    /// <summary>Cultura usada para formatar números em mensagens e relatórios.</summary>
    public static CultureInfo Culture => IsEnglish ? EnUs : PtBr;

    public static string T(string pt, string en) => IsEnglish ? en : pt;

    public static string F(string pt, string en, params object?[] args) =>
        string.Format(Culture, T(pt, en), args);

    /// <summary>Converte o código de idioma do Revit/Windows ("pt-BR", "en-US"...) para o idioma do plugin.</summary>
    public static Language FromCultureName(string? name) =>
        name != null && name.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? Language.Portuguese : Language.English;
}

/// <summary>Texto bilíngue (pt-BR / en-US).</summary>
public readonly record struct Txt(string Pt, string En)
{
    public string Value => Loc.T(Pt, En);

    public override string ToString() => Value;

    public static implicit operator string(Txt t) => t.Value;

    public bool Matches(string query) =>
        Pt.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        En.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Text.TextUtil.RemoveDiacritics(Pt).Contains(Text.TextUtil.RemoveDiacritics(query), StringComparison.OrdinalIgnoreCase);
}
