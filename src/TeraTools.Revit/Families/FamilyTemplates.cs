using Autodesk.Revit.ApplicationServices;
using TeraTools.Core.Text;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Families;

public enum TemplateKind
{
    GenericModel,
    GenericModelFaceBased,
    GenericModelLineBased,
    GenericModelAdaptive,
}

/// <summary>
/// Localiza templates de família (.rft) instalados com o Revit, em inglês ou português,
/// pontuando os nomes de arquivo (os nomes exatos variam por idioma e versão).
/// </summary>
public static class FamilyTemplates
{
    private static readonly Dictionary<string, string?> Cache = new();

    public static string? Find(Application app, TemplateKind kind, string? overrideFolder)
    {
        var key = kind + "|" + overrideFolder;
        if (Cache.TryGetValue(key, out var cached) && (cached == null || File.Exists(cached))) return cached;

        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(overrideFolder) && Directory.Exists(overrideFolder)) roots.Add(overrideFolder);
        try
        {
            var p = app.FamilyTemplatePath;
            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            {
                roots.Add(p);
                var parent = Directory.GetParent(p)?.FullName;
                if (parent != null) roots.Add(parent);
            }
        }
        catch
        {
            // ignora
        }
        var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        roots.Add(Path.Combine(pd, "Autodesk", "RVT " + Paths.RevitVersion, "Family Templates"));

        string? best = null;
        var bestScore = int.MinValue;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.rft", SearchOption.AllDirectories).Take(4000);
            }
            catch
            {
                continue;
            }
            foreach (var f in files)
            {
                if (!visited.Add(f)) continue;
                var s = Score(Path.GetFileNameWithoutExtension(f), kind);
                if (root == overrideFolder) s += 5;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = f;
                }
            }
        }
        var result = bestScore >= 10 ? best : null;
        Cache[key] = result;
        Log.Info($"Template {kind}: {result ?? "não encontrado"}");
        return result;
    }

    internal static int Score(string fileName, TemplateKind kind)
    {
        var n = TextUtil.RemoveDiacritics(fileName).ToLowerInvariant();
        if (!(n.Contains("generic model") || n.Contains("generico") || n.Contains("modelo generico"))) return int.MinValue;
        var score = 10;
        if (n.Contains("metric") || n.Contains("metrico")) score += 3;
        var isFace = n.Contains("face");
        var isLine = n.Contains("line") || n.Contains("linha");
        var isAdaptive = n.Contains("adapt");
        var hostedOther = new[] { "wall", "parede", "ceiling", "forro", "floor", "piso", "roof", "telhado", "pattern", "padr", "level", "nivel", "two", "dois" }.Any(n.Contains);
        switch (kind)
        {
            case TemplateKind.GenericModel:
                if (isFace || isLine || isAdaptive || hostedOther) return int.MinValue;
                break;
            case TemplateKind.GenericModelFaceBased:
                if (!isFace || isLine || isAdaptive) return int.MinValue;
                break;
            case TemplateKind.GenericModelLineBased:
                if (!isLine || isFace || isAdaptive) return int.MinValue;
                break;
            case TemplateKind.GenericModelAdaptive:
                if (!isAdaptive || n.Contains("pattern") || n.Contains("padr")) return int.MinValue;
                break;
        }
        return score - n.Length / 20;
    }
}
