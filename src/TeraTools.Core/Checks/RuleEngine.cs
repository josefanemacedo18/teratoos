using System.Globalization;
using System.Text.RegularExpressions;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Text;

namespace TeraTools.Core.Checks;

public enum RuleKind
{
    /// <summary>Parâmetro obrigatório (não pode estar vazio).</summary>
    Required,
    /// <summary>Valor numérico dentro de uma faixa.</summary>
    Range,
    /// <summary>Texto deve seguir um padrão (expressão regular).</summary>
    Pattern,
    /// <summary>Valor deve estar em uma lista permitida.</summary>
    AllowedValues,
    /// <summary>Valor não pode se repetir entre elementos.</summary>
    Unique,
    /// <summary>Nome do tipo/família deve seguir um padrão.</summary>
    NamePattern,
}

/// <summary>Regra de verificação personalizável (gravada em JSON no projeto ou na biblioteca).</summary>
public sealed class CheckRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public Severity Severity { get; set; } = Severity.Warning;

    /// <summary>Categorias alvo (nomes BuiltInCategory, ex.: "OST_Rooms"). Vazio = todas.</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>Filtro opcional por nome de família/tipo (expressão regular).</summary>
    public string? FamilyFilter { get; set; }

    public RuleKind Kind { get; set; } = RuleKind.Required;
    public string Parameter { get; set; } = "";
    public bool TypeParameter { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Pattern { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public bool CaseSensitive { get; set; }
    public string? Message { get; set; }
}

public sealed class RuleSet
{
    public string Name { get; set; } = "";
    public List<CheckRule> Rules { get; set; } = new();

    /// <summary>Conjunto inicial de regras úteis para projetos de interiores (editável).</summary>
    public static RuleSet Default() => new()
    {
        Name = Loc.T("Regras padrão Téra", "Téra default rules"),
        Rules =
        {
            new CheckRule { Name = Loc.T("Ambiente com nome", "Room has a name"), Categories = { "OST_Rooms" }, Kind = RuleKind.Required, Parameter = "ROOM_NAME", Severity = Severity.Error },
            new CheckRule { Name = Loc.T("Ambiente com número", "Room has a number"), Categories = { "OST_Rooms" }, Kind = RuleKind.Required, Parameter = "ROOM_NUMBER", Severity = Severity.Error },
            new CheckRule { Name = Loc.T("Número de ambiente único", "Unique room number"), Categories = { "OST_Rooms" }, Kind = RuleKind.Unique, Parameter = "ROOM_NUMBER", Severity = Severity.Error },
            new CheckRule { Name = Loc.T("Porta com marca", "Door has a mark"), Categories = { "OST_Doors" }, Kind = RuleKind.Required, Parameter = "ALL_MODEL_MARK" },
            new CheckRule { Name = Loc.T("Marca de porta única", "Unique door mark"), Categories = { "OST_Doors" }, Kind = RuleKind.Unique, Parameter = "ALL_MODEL_MARK" },
            new CheckRule { Name = Loc.T("Mobiliário com código", "Furniture has a code"), Categories = { "OST_Furniture", "OST_Casework" }, Kind = RuleKind.Required, Parameter = "TT_Codigo", Severity = Severity.Info },
            new CheckRule { Name = Loc.T("Luminária com código", "Lighting fixture has a code"), Categories = { "OST_LightingFixtures" }, Kind = RuleKind.Required, Parameter = "TT_Codigo", Severity = Severity.Info },
            new CheckRule { Name = Loc.T("Altura de porta (2,00 a 2,40 m)", "Door height (2.00 to 2.40 m)"), Categories = { "OST_Doors" }, Kind = RuleKind.Range, Parameter = "DOOR_HEIGHT", TypeParameter = true, Min = 2000, Max = 2400, Severity = Severity.Warning, Description = Loc.T("Valores em milímetros.", "Values in millimeters.") },
        },
    };
}

/// <summary>Registro neutro de um elemento (extraído do Revit) para avaliação das regras.</summary>
public sealed class ElementRecord
{
    public long Id { get; set; }
    public string Category { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Family { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Valores de parâmetros de instância como texto (chave: nome ou BuiltInParameter).</summary>
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Valores numéricos em unidades de exibição (mm para comprimentos, m² para áreas).</summary>
    public Dictionary<string, double?> Numbers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string?> TypeValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double?> TypeNumbers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Indica se o parâmetro existe no elemento (para diferenciar "vazio" de "inexistente").</summary>
    public HashSet<string> Present { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CheckIssue
{
    public CheckRule Rule { get; set; } = new();
    public long ElementId { get; set; }
    public string Message { get; set; } = "";
    public Severity Severity { get; set; }
}

public static class RuleEngine
{
    public static List<CheckIssue> Run(RuleSet set, IReadOnlyList<ElementRecord> records)
    {
        var issues = new List<CheckIssue>();
        foreach (var rule in set.Rules.Where(r => r.Enabled))
        {
            var targets = records.Where(r => Applies(rule, r)).ToList();
            if (rule.Kind == RuleKind.Unique)
            {
                foreach (var g in targets
                             .Select(r => (r, v: Get(rule, r)))
                             .Where(x => !string.IsNullOrWhiteSpace(x.v))
                             .GroupBy(x => rule.CaseSensitive ? x.v!.Trim() : x.v!.Trim().ToUpperInvariant())
                             .Where(g => g.Count() > 1))
                {
                    foreach (var (r, v) in g)
                        issues.Add(Issue(rule, r, Loc.F("Valor \"{0}\" repetido em {1} elementos.", "Value \"{0}\" repeated in {1} elements.", v, g.Count())));
                }
                continue;
            }
            foreach (var r in targets)
            {
                var msg = Evaluate(rule, r);
                if (msg != null) issues.Add(Issue(rule, r, msg));
            }
        }
        return issues;
    }

    private static CheckIssue Issue(CheckRule rule, ElementRecord r, string detail) => new()
    {
        Rule = rule,
        ElementId = r.Id,
        Severity = rule.Severity,
        Message = $"{(string.IsNullOrWhiteSpace(rule.Message) ? rule.Name : rule.Message)} — {Describe(r)}: {detail}",
    };

    public static string Describe(ElementRecord r)
    {
        var name = !string.IsNullOrWhiteSpace(r.Name) ? r.Name : $"{r.Family} : {r.Type}".Trim(' ', ':');
        return $"{r.CategoryName} \"{name}\" [{r.Id}]";
    }

    public static bool Applies(CheckRule rule, ElementRecord r)
    {
        if (rule.Categories.Count > 0 && !rule.Categories.Any(c =>
                string.Equals(c, r.Category, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, r.CategoryName, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrWhiteSpace(rule.FamilyFilter))
        {
            try
            {
                if (!Regex.IsMatch($"{r.Family} : {r.Type}", rule.FamilyFilter, RegexOptions.IgnoreCase)) return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        return true;
    }

    private static string? Get(CheckRule rule, ElementRecord r)
    {
        var d = rule.TypeParameter ? r.TypeValues : r.Values;
        return d.TryGetValue(rule.Parameter, out var v) ? v : null;
    }

    private static double? GetNumber(CheckRule rule, ElementRecord r)
    {
        var d = rule.TypeParameter ? r.TypeNumbers : r.Numbers;
        if (d.TryGetValue(rule.Parameter, out var v) && v.HasValue) return v;
        var s = Get(rule, r);
        return Units.U.TryParseNumber(s, out var n) ? n : null;
    }

    private static string? Evaluate(CheckRule rule, ElementRecord r)
    {
        var value = Get(rule, r);
        switch (rule.Kind)
        {
            case RuleKind.Required:
                if (!r.Present.Contains(rule.Parameter) && !(rule.TypeParameter ? r.TypeValues : r.Values).ContainsKey(rule.Parameter))
                    return Loc.F("parâmetro \"{0}\" não existe.", "parameter \"{0}\" does not exist.", rule.Parameter);
                return string.IsNullOrWhiteSpace(value) ? Loc.F("\"{0}\" está vazio.", "\"{0}\" is empty.", rule.Parameter) : null;
            case RuleKind.Range:
                var n = GetNumber(rule, r);
                if (!n.HasValue) return Loc.F("\"{0}\" sem valor numérico.", "\"{0}\" has no numeric value.", rule.Parameter);
                if (rule.Min.HasValue && n.Value < rule.Min.Value - 1e-9)
                    return Loc.F("{0} = {1} abaixo do mínimo {2}.", "{0} = {1} below minimum {2}.", rule.Parameter, Fmt(n.Value), Fmt(rule.Min.Value));
                if (rule.Max.HasValue && n.Value > rule.Max.Value + 1e-9)
                    return Loc.F("{0} = {1} acima do máximo {2}.", "{0} = {1} above maximum {2}.", rule.Parameter, Fmt(n.Value), Fmt(rule.Max.Value));
                return null;
            case RuleKind.Pattern:
                if (string.IsNullOrWhiteSpace(rule.Pattern)) return null;
                if (string.IsNullOrEmpty(value)) return Loc.F("\"{0}\" está vazio.", "\"{0}\" is empty.", rule.Parameter);
                return SafeMatch(value, rule) ? null : Loc.F("\"{0}\" não segue o padrão {1}.", "\"{0}\" does not match pattern {1}.", value, rule.Pattern);
            case RuleKind.NamePattern:
                if (string.IsNullOrWhiteSpace(rule.Pattern)) return null;
                var name = !string.IsNullOrWhiteSpace(r.Name) ? r.Name : r.Type;
                return SafeMatch(name, rule) ? null : Loc.F("nome \"{0}\" fora do padrão {1}.", "name \"{0}\" does not match {1}.", name, rule.Pattern);
            case RuleKind.AllowedValues:
                if (rule.AllowedValues.Count == 0) return null;
                var ok = rule.AllowedValues.Any(a => string.Equals(a.Trim(), value?.Trim(), rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
                return ok ? null : Loc.F("valor \"{0}\" não permitido.", "value \"{0}\" not allowed.", value ?? "");
            default:
                return null;
        }
    }

    private static bool SafeMatch(string value, CheckRule rule)
    {
        try
        {
            return Regex.IsMatch(value, rule.Pattern!, rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    private static string Fmt(double v) => v.ToString("0.##", Loc.Culture);

    /// <summary>Encontra nomes muito parecidos (possíveis duplicados), ex.: "Porta 80" e "Porta  80 ".</summary>
    public static List<(string A, string B, int Distance)> SimilarNames(IEnumerable<string> names, int maxDistance = 1)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var norm = list.Select(TextUtil.NormalizeForCompare).ToList();
        var result = new List<(string, string, int)>();
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (Math.Abs(norm[i].Length - norm[j].Length) > maxDistance) continue;
                var d = norm[i] == norm[j] ? 0 : TextUtil.Levenshtein(norm[i], norm[j]);
                if (d <= maxDistance && !HasDifferentNumbers(norm[i], norm[j]))
                    result.Add((list[i], list[j], d));
            }
        }
        return result;
    }

    // "Sala 1" e "Sala 2" diferem em 1 caractere mas não são duplicados.
    private static bool HasDifferentNumbers(string a, string b)
    {
        var na = Regex.Matches(a, "\\d+").Select(m => m.Value).ToList();
        var nb = Regex.Matches(b, "\\d+").Select(m => m.Value).ToList();
        return !na.SequenceEqual(nb);
    }

    internal static string Invariant(double v) => v.ToString(CultureInfo.InvariantCulture);
}
