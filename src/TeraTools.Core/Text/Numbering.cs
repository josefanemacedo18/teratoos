using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TeraTools.Core.Text;

public enum NumberingStyle
{
    /// <summary>1, 2, 3...</summary>
    Numbers,
    /// <summary>A, B, C...</summary>
    Letters,
    /// <summary>a, b, c...</summary>
    LowerLetters,
    /// <summary>I, II, III...</summary>
    Roman,
}

/// <summary>
/// Sequência de numeração configurável (prefixo, início, passo, dígitos, sufixo).
/// Ex.: Prefix="AMB-", Start=1, Digits=3 → AMB-001, AMB-002...
/// </summary>
public sealed class NumberingSequence
{
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public int Start { get; set; } = 1;
    public int Step { get; set; } = 1;
    public int Digits { get; set; } = 1;
    public NumberingStyle Style { get; set; } = NumberingStyle.Numbers;

    public string Format(int index)
    {
        var n = Start + index * Step;
        string core = Style switch
        {
            NumberingStyle.Letters => TextUtil.ToLetters(n - 1),
            NumberingStyle.LowerLetters => TextUtil.ToLetters(n - 1).ToLowerInvariant(),
            NumberingStyle.Roman => TextUtil.ToRoman(n),
            _ => n.ToString(new string('0', Math.Max(1, Digits)), CultureInfo.InvariantCulture),
        };
        return Prefix + core + Suffix;
    }

    public IEnumerable<string> Take(int count)
    {
        for (var i = 0; i < count; i++) yield return Format(i);
    }
}

/// <summary>
/// Modelos de nome com marcadores: "{Nivel}-{Numero:000} {Nome}".
/// Marcadores desconhecidos ficam como estão (para o usuário perceber o erro).
/// </summary>
public static class NameTemplate
{
    private static readonly Regex Token = new(@"\{(?<name>[^{}:]+)(:(?<fmt>[^{}]+))?\}", RegexOptions.Compiled);

    public static string Apply(string template, IReadOnlyDictionary<string, object?> values)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var dict = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        return Token.Replace(template, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            if (!dict.TryGetValue(name, out var v) &&
                !dict.TryGetValue(TextUtil.RemoveDiacritics(name), out v))
                return m.Value;
            var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;
            return v switch
            {
                null => "",
                IFormattable f when fmt != null => f.ToString(fmt, CultureInfo.InvariantCulture),
                _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
            };
        });
    }

    /// <summary>Marcadores usados no modelo.</summary>
    public static IEnumerable<string> Tokens(string template) =>
        Token.Matches(template ?? "").Select(m => m.Groups["name"].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Operações de renomeação em lote (localizar/substituir, maiúsculas, prefixo...).</summary>
public sealed class RenameRule
{
    public string Find { get; set; } = "";
    public string Replace { get; set; } = "";
    public bool UseRegex { get; set; }
    public bool MatchCase { get; set; }
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public TextCase Case { get; set; } = TextCase.Keep;
    public bool TrimSpaces { get; set; } = true;

    public string Apply(string input)
    {
        var s = input ?? "";
        if (!string.IsNullOrEmpty(Find))
        {
            if (UseRegex)
                s = Regex.Replace(s, Find, Replace ?? "", MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
            else
                s = s.Replace(Find, Replace ?? "", MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }
        s = Case switch
        {
            TextCase.Upper => s.ToUpper(CultureInfo.CurrentCulture),
            TextCase.Lower => s.ToLower(CultureInfo.CurrentCulture),
            TextCase.Title => ToTitle(s),
            TextCase.Sentence => s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s[1..].ToLower(CultureInfo.CurrentCulture),
            _ => s,
        };
        s = Prefix + s + Suffix;
        if (TrimSpaces) s = Regex.Replace(s, "\\s{2,}", " ").Trim();
        return s;
    }

    private static string ToTitle(string s)
    {
        // Mantém preposições curtas em minúsculo (estilo português: "Sala de Estar").
        var small = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "da", "do", "das", "dos", "e", "a", "o", "em", "of", "and", "the" };
        var words = s.ToLower(CultureInfo.CurrentCulture).Split(' ');
        var sb = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (i > 0) sb.Append(' ');
            if (w.Length == 0) continue;
            if (i > 0 && small.Contains(w)) sb.Append(w);
            else sb.Append(char.ToUpper(w[0], CultureInfo.CurrentCulture)).Append(w[1..]);
        }
        return sb.ToString();
    }
}

public enum TextCase
{
    Keep,
    Upper,
    Lower,
    Title,
    Sentence,
}
