using Autodesk.Revit.DB;
using TeraTools.Core.Text;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Tipos de campo do diálogo de opções.</summary>
public enum OptKind
{
    Auto,
    Length,
    Angle,
    Number,
    Integer,
    Percent,
    Text,
    MultiLine,
    Bool,
    Enum,
    Color,
    File,
    SaveFile,
    Folder,
    Formula,
    FamilyType,
    Material,
    Level,
    View,
    ViewTemplate,
    TitleBlock,
    WallType,
    FloorType,
    CeilingType,
    RoofType,
    Category,
    Phase,
    TextNoteType,
    FilledRegionType,
    DimensionType,
    ViewFamilyType,
    WallSweepType,
    LinePattern,
    LineStyle,
}

/// <summary>Listas de elementos do modelo para campos de escolha e resolução por nome.</summary>
public static class Lookup
{
    public static string Label(FamilySymbol s) => $"{s.FamilyName} : {s.Name}";

    public static List<(string Label, string Value)> Choices(Document doc, OptKind kind, string? filter)
    {
        IEnumerable<(string, string)> items = kind switch
        {
            OptKind.FamilyType => Symbols(doc, filter).Select(s => (Label(s), Label(s))),
            OptKind.TitleBlock => Symbols(doc, "OST_TitleBlocks").Select(s => (Label(s), Label(s))),
            OptKind.Material => Collect<Material>(doc).Select(m => (m.Name, m.Name)),
            OptKind.Level => Collect<Level>(doc).OrderBy(l => l.Elevation).Select(l => (l.Name, l.Name)),
            OptKind.View => Collect<View>(doc).Where(v => !v.IsTemplate && v.CanBePrinted && ViewTypeMatches(v, filter)).Select(v => (v.Name, v.Name)),
            OptKind.ViewTemplate => Collect<View>(doc).Where(v => v.IsTemplate && ViewTypeMatches(v, filter)).Select(v => (v.Name, v.Name)),
            OptKind.WallType => Collect<WallType>(doc).Where(w => w.Kind == WallKind.Basic).Select(t => (t.Name, t.Name)),
            OptKind.FloorType => Collect<FloorType>(doc).Where(f => !f.IsFoundationSlab).Select(t => (t.Name, t.Name)),
            OptKind.CeilingType => Collect<CeilingType>(doc).Select(t => (t.Name, t.Name)),
            OptKind.RoofType => Collect<RoofType>(doc).Select(t => (t.Name, t.Name)),
            OptKind.Phase => doc.Phases.Cast<Phase>().Select(p => (p.Name, p.Name)),
            OptKind.TextNoteType => Collect<TextNoteType>(doc).Select(t => (t.Name, t.Name)),
            OptKind.FilledRegionType => Collect<FilledRegionType>(doc).Select(t => (t.Name, t.Name)),
            OptKind.DimensionType => Collect<DimensionType>(doc).Where(d => d.StyleType == DimensionStyleType.Linear).Select(t => (t.Name, t.Name)),
            OptKind.ViewFamilyType => Collect<ViewFamilyType>(doc).Where(v => filter == null || filter.Split(',').Any(f => string.Equals(v.ViewFamily.ToString(), f.Trim(), StringComparison.OrdinalIgnoreCase))).Select(t => (t.Name, t.Name)),
            OptKind.WallSweepType => new FilteredElementCollector(doc).OfClass(typeof(ElementType)).OfCategory(BuiltInCategory.OST_Cornices).Cast<ElementType>().Select(t => (t.Name, t.Name)),
            OptKind.LinePattern => Collect<LinePatternElement>(doc).Select(t => (t.Name, t.Name)),
            OptKind.LineStyle => LineStyles(doc).Select(c => (c.Name, c.Name)),
            OptKind.Category => ModelCategories(doc).Select(c => (c.Name, ((BuiltInCategory)c.Id.Value).ToString())),
            _ => Enumerable.Empty<(string, string)>(),
        };
        return items.DistinctBy(i => i.Item2).OrderBy(i => i.Item1, TextUtil.NaturalComparer).ToList();
    }

    private static bool ViewTypeMatches(View v, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return filter.Split(',').Any(f => string.Equals(v.ViewType.ToString(), f.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<T> Collect<T>(Document doc) where T : Element =>
        new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>();

    public static IEnumerable<FamilySymbol> Symbols(Document doc, string? categories)
    {
        var col = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
        var cats = ParseCategories(categories);
        if (cats.Count > 0)
            col = col.WherePasses(new ElementMulticategoryFilter(cats));
        return col.Cast<FamilySymbol>();
    }

    public static List<BuiltInCategory> ParseCategories(string? categories)
    {
        var list = new List<BuiltInCategory>();
        if (string.IsNullOrWhiteSpace(categories)) return list;
        foreach (var c in categories.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            if (Enum.TryParse<BuiltInCategory>(c.Trim(), out var bic))
                list.Add(bic);
        return list;
    }

    public static IEnumerable<Category> ModelCategories(Document doc)
    {
        foreach (Category c in doc.Settings.Categories)
        {
            if (c.CategoryType != CategoryType.Model || !c.AllowsBoundParameters) continue;
            if (c.Id.Value >= 0) continue; // apenas categorias internas (BuiltInCategory)
            yield return c;
        }
    }

    public static IEnumerable<Category> LineStyles(Document doc)
    {
        var lines = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        if (lines == null) yield break;
        foreach (Category c in lines.SubCategories) yield return c;
    }

    // ---------------- Resolução por nome ----------------

    public static FamilySymbol? Symbol(Document doc, string? key, string? categories = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var parts = key.Split(new[] { " : " }, 2, StringSplitOptions.None);
        var list = Symbols(doc, categories);
        return parts.Length == 2
            ? list.FirstOrDefault(s => s.FamilyName == parts[0].Trim() && s.Name == parts[1].Trim())
            : list.FirstOrDefault(s => s.Name == key.Trim());
    }

    public static Material? Material(Document doc, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Collect<Material>(doc).FirstOrDefault(m => m.Name == name);

    public static Level? Level(Document doc, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Collect<Level>(doc).FirstOrDefault(l => l.Name == name);

    public static T? Named<T>(Document doc, string? name) where T : Element =>
        string.IsNullOrWhiteSpace(name) ? null : Collect<T>(doc).FirstOrDefault(e => e.Name == name);

    public static View? View(Document doc, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Collect<View>(doc).FirstOrDefault(v => v.Name == name);

    public static Phase? Phase(Document doc, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : doc.Phases.Cast<Phase>().FirstOrDefault(p => p.Name == name);

    public static ElementId CategoryId(string? bicName) =>
        Enum.TryParse<BuiltInCategory>(bicName ?? "", out var bic) ? new ElementId(bic) : ElementId.InvalidElementId;

    public static ElementType? ViewFamilyType(Document doc, ViewFamily family, string? preferredName = null)
    {
        var list = Collect<ViewFamilyType>(doc).Where(v => v.ViewFamily == family).ToList();
        return list.FirstOrDefault(v => v.Name == preferredName) ?? list.FirstOrDefault();
    }

    /// <summary>Nível mais próximo abaixo (ou igual) de uma cota.</summary>
    public static Level? LevelBelow(Document doc, double z) =>
        Collect<Level>(doc).Where(l => l.Elevation <= z + 1e-6).OrderByDescending(l => l.Elevation).FirstOrDefault()
        ?? Collect<Level>(doc).OrderBy(l => l.Elevation).FirstOrDefault();

    /// <summary>Gera um nome único de vista acrescentando " (2)", " (3)"...</summary>
    public static string UniqueViewName(Document doc, string name)
    {
        var existing = Collect<View>(doc).Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(name)) return name;
        for (var i = 2; i < 10000; i++)
        {
            var n = $"{name} ({i})";
            if (!existing.Contains(n)) return n;
        }
        return name + " " + Guid.NewGuid().ToString("N")[..6];
    }

    public static string ElementLabel(Element e)
    {
        if (e is FamilyInstance fi) return $"{fi.Symbol.FamilyName} : {fi.Symbol.Name}";
        var typeId = e.GetTypeId();
        var t = typeId != ElementId.InvalidElementId ? e.Document.GetElement(typeId) : null;
        return t != null ? $"{e.Category?.Name} : {t.Name}" : $"{e.Category?.Name} : {e.Name}";
    }
}
