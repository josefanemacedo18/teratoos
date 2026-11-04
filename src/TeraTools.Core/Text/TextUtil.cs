using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TeraTools.Core.Text;

public static class TextUtil
{
    /// <summary>Remove acentos ("Área" → "Area").</summary>
    public static string RemoveDiacritics(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Texto seguro para nomes de arquivo.</summary>
    public static string SafeFileName(string? s, string fallback = "arquivo")
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var sb = new StringBuilder();
        foreach (var ch in s.Trim())
            sb.Append(invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch);
        var r = sb.ToString().Trim('.', ' ');
        return r.Length == 0 ? fallback : r;
    }

    /// <summary>Identificador sem acentos/espaços (ex.: para nomes de tipos gerados).</summary>
    public static string Slug(string? s)
    {
        var r = RemoveDiacritics(s ?? "");
        r = Regex.Replace(r, "[^A-Za-z0-9]+", "_").Trim('_');
        return r.Length == 0 ? "item" : r;
    }

    /// <summary>Comparação "natural": "Sala 2" &lt; "Sala 10".</summary>
    public static int CompareNatural(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var si = i;
                var sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = a[si..i].TrimStart('0');
                var nb = b[sj..j].TrimStart('0');
                if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                var c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                var c = string.Compare(a[i].ToString(), b[j].ToString(), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
                if (c != 0) return c;
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    public static readonly IComparer<string> NaturalComparer = Comparer<string>.Create(CompareNatural);

    /// <summary>Converte índice (0, 1, 2...) em letras (A, B, ... Z, AA, AB...).</summary>
    public static string ToLetters(int index)
    {
        if (index < 0) return "";
        var sb = new StringBuilder();
        index++;
        while (index > 0)
        {
            index--;
            sb.Insert(0, (char)('A' + index % 26));
            index /= 26;
        }
        return sb.ToString();
    }

    /// <summary>Converte inteiro em algarismos romanos (1..3999).</summary>
    public static string ToRoman(int n)
    {
        if (n <= 0 || n > 3999) return n.ToString(CultureInfo.InvariantCulture);
        (int V, string S)[] map = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new StringBuilder();
        foreach (var (v, s) in map)
            while (n >= v) { sb.Append(s); n -= v; }
        return sb.ToString();
    }

    /// <summary>Distância de edição (Levenshtein), usada para detectar nomes parecidos/duplicados.</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    /// <summary>Normalização para comparar nomes (sem acento, minúsculo, espaços simples).</summary>
    public static string NormalizeForCompare(string? s) =>
        Regex.Replace(RemoveDiacritics(s ?? "").ToLowerInvariant(), "\\s+", " ").Trim();
}
