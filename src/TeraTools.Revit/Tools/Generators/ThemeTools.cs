using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Colors;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Settings;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public sealed class PaletteOptions
{
    [Opt("Cor base", "Base color", Kind = OptKind.Color)]
    public string BaseColor { get; set; } = "#B5652B";

    [Opt("Harmonia", "Harmony")]
    public Harmony Harmony { get; set; } = Harmony.Analogous;

    [Opt("Segunda cor (degradê)", "Second color (gradient)", Kind = OptKind.Color, VisibleIf = "Harmony=Gradient")]
    public string SecondColor { get; set; } = "#2F5560";

    [Opt("Quantidade de cores", "Number of colors", Min = 2, Max = 24)]
    public int Count { get; set; } = 6;

    [Opt("Criar materiais", "Create materials")]
    public bool CreateMaterials { get; set; } = true;

    [Opt("Prefixo dos materiais", "Material prefix", VisibleIf = "CreateMaterials")]
    public string Prefix { get; set; } = "TT Paleta";

    [Opt("Brilho do material (0–128)", "Material shininess (0–128)", Min = 0, Max = 128, VisibleIf = "CreateMaterials")]
    public int Shininess { get; set; } = 20;

    public List<RgbColor> Colors() =>
        PaletteGenerator.Generate(RgbColor.TryParse(BaseColor, out var b) ? b : RgbColor.Gray, Harmony, Count,
            RgbColor.TryParse(SecondColor, out var s) ? s : null);
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.palette", Modules.Generators, "Paleta de cores", "Color palette", Order = 60, Group = "Temas|Themes",
    DescPt = "Cria paletas harmônicas (análoga, complementar, tríade, neutros, degradê) e materiais com essas cores.",
    DescEn = "Creates harmonious palettes (analogous, complementary, triadic, neutrals, gradient) and materials in those colors.",
    Keywords = "paleta cores harmonia materiais palette colors harmony")]
public sealed class PaletteCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<PaletteOptions>(preview: Preview, okText: Loc.T("Criar", "Create"));
        var colors = o.Colors();
        var t = new TableData(Loc.T("Paleta", "Palette"));
        t.AddColumn(Loc.T("Nome", "Name")).AddColumn("Hex").AddColumn("RGB");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                var name = $"{o.Prefix} {i + 1:00} {c.Hex}";
                if (o.CreateMaterials)
                {
                    var m = MaterialsService.Ensure(ctx.Doc, name, c, 0, o.Shininess);
                    ctx.Report.AddResult(m.Id.Value);
                    ctx.Report.Created();
                }
                t.AddRow(name, c.Hex, $"{c.R}, {c.G}, {c.B}");
            }
        });
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
    }

    private static PreviewModel Preview(PaletteOptions o)
    {
        var pm = new PreviewModel();
        var colors = o.Colors();
        for (var i = 0; i < colors.Count; i++)
            pm.Polygon(Polygon2.Rectangle(new Vec2(i * 1.1, 0), 1, 1.6), colors[i].Hex, "#5F6B7A", 0.5);
        pm.Info = string.Join("  ", colors.Select(c => c.Hex));
        return pm;
    }
}

public enum ColorScope
{
    [Text("Elementos visíveis na vista ativa", "Elements visible in the active view")] View,
    [Text("Projeto inteiro", "Whole project")] Project,
}

public enum ColorTarget
{
    [Text("Vista ativa", "Active view")] ActiveView,
    [Text("Cópia da vista ativa (apresentação)", "Copy of the active view (presentation)")] Duplicate,
}

public sealed class ColorByOptions
{
    [Opt("Valores a partir de", "Values from")]
    public ColorScope Scope { get; set; } = ColorScope.View;

    [Opt("Aplicar em", "Apply to")]
    public ColorTarget Target { get; set; } = ColorTarget.ActiveView;

    [Opt("Paleta", "Palette")]
    public Harmony Harmony { get; set; } = Harmony.Analogous;

    [Opt("Usar cores bem distintas (categórico)", "Use clearly distinct colors (categorical)")]
    public bool Distinct { get; set; } = true;

    [Opt("Cor base", "Base color", Kind = OptKind.Color, VisibleIf = "!Distinct")]
    public string BaseColor { get; set; } = "#B5652B";

    [Opt("Transparência (0–100)", "Transparency (0–100)", Min = 0, Max = 100)]
    public int Transparency { get; set; }

    [Opt("Colorir também os cortes", "Also color cut patterns")]
    public bool ColorCut { get; set; } = true;

    [Opt("Reaproveitar cores do tema salvo", "Reuse colors from saved theme", Tip = "Valores já coloridos antes mantêm a mesma cor.|Values colored before keep the same color.")]
    public bool ReuseTheme { get; set; } = true;

    [Opt("Criar legenda (vista de desenho)", "Create legend (drafting view)")]
    public bool CreateLegend { get; set; } = true;

    [Opt("Tipo de texto da legenda", "Legend text type", Kind = OptKind.TextNoteType, VisibleIf = "CreateLegend")]
    public string TextType { get; set; } = "";
}

/// <summary>Temas de cores salvos no projeto (por parâmetro).</summary>
public sealed class ThemeLibrary
{
    public Dictionary<string, ColorTheme> Themes { get; set; } = new();
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.colorby", Modules.Generators, "Colorir por parâmetro", "Color by parameter", Order = 61, Group = "Temas|Themes",
    DescPt = "Cria filtros de vista por categoria e valor de parâmetro, com cores, transparência e legenda; o tema fica salvo no projeto.",
    DescEn = "Creates view filters by category and parameter value, with colors, transparency and legend; the theme is saved in the project.",
    HelpPt = "Ex.: colorir mobiliário por fornecedor, ambientes por departamento, pisos por material ou paredes por fase. Depois de escolher as categorias, a lista mostra apenas parâmetros filtráveis em comum. Cada valor vira um filtro \"TT Cor ...\" editável normalmente em Visibilidade/Gráficos.",
    HelpEn = "E.g. color furniture by supplier, rooms by department, floors by material. Each value becomes an editable \"TT Cor ...\" filter in Visibility/Graphics.",
    Keywords = "colorir filtro vista cor parametro tema legenda color filter view theme legend")]
public sealed class ColorByCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var view = ctx.ActiveView;
        if (view.ViewType is ViewType.Schedule or ViewType.DrawingSheet or ViewType.ProjectBrowser)
            ToolContext.Fail("Abra uma vista de modelo (planta, corte, 3D).", "Open a model view (plan, section, 3D).");
        var o = ctx.Ask<ColorByOptions>(okText: Loc.T("Continuar", "Continue"));
        var cats = Lookup.ModelCategories(ctx.Doc).OrderBy(c => c.Name).ToList();
        var chosen = Pick.Many(cats, c => c.Name, ctx.Tool.Name, Loc.T("Quais categorias colorir?", "Which categories to color?"), ctx.Owner);
        if (chosen.Count == 0) ctx.Cancel();
        ICollection<ElementId> catIds = chosen.Select(c => c.Id).ToList();
        ParameterFilterUtilities.RemoveUnfilterableCategories(catIds);
        if (catIds.Count == 0) ToolContext.Fail("As categorias escolhidas não aceitam filtros.", "The chosen categories do not accept filters.");
        var paramIds = ParameterFilterUtilities.GetFilterableParametersInCommon(ctx.Doc, catIds).ToList();
        var paramItems = paramIds.Select(id => (Id: id, Name: ParamName(ctx.Doc, id))).Where(x => !string.IsNullOrWhiteSpace(x.Name)).OrderBy(x => x.Name).ToList();
        var param = Pick.One(paramItems.Select(p => (object)p).ToList(), x => (((ElementId, string))x).Item2, ctx.Tool.Name, Loc.T("Colorir pelo parâmetro:", "Color by parameter:"), ctx.Owner);
        if (param == null) ctx.Cancel();
        var (paramId, paramName) = ((ElementId, string))param!;

        var collector = o.Scope == ColorScope.View
            ? new FilteredElementCollector(ctx.Doc, view.Id)
            : new FilteredElementCollector(ctx.Doc);
        var elements = collector.WherePasses(new ElementMulticategoryFilter(catIds)).WhereElementIsNotElementType().ToList();
        var values = new Dictionary<string, (StorageType Type, object Value, List<long> Ids)>();
        foreach (var e in elements)
        {
            var p = FindParam(e, paramId);
            if (p == null || !p.HasValue) continue;
            string key;
            object raw;
            switch (p.StorageType)
            {
                case StorageType.String:
                    raw = p.AsString() ?? "";
                    key = (string)raw;
                    break;
                case StorageType.Integer:
                    raw = p.AsInteger();
                    key = p.AsValueString() ?? raw.ToString()!;
                    break;
                case StorageType.Double:
                    raw = p.AsDouble();
                    key = p.AsValueString() ?? raw.ToString()!;
                    break;
                case StorageType.ElementId:
                    raw = p.AsElementId();
                    key = ctx.Doc.GetElement((ElementId)raw)?.Name ?? p.AsValueString() ?? "";
                    break;
                default:
                    continue;
            }
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!values.TryGetValue(key, out var v)) values[key] = v = (p.StorageType, raw, new List<long>());
            v.Ids.Add(e.Id.Value);
        }
        if (values.Count == 0) ToolContext.Fail("Nenhum valor encontrado para esse parâmetro.", "No values found for that parameter.");
        if (values.Count > 60 && !Msg.Confirm(Loc.F("Há {0} valores diferentes. Criar um filtro para cada?", "There are {0} different values. Create a filter for each?", values.Count)))
            ctx.Cancel();

        var keys = values.Keys.OrderBy(k => k, Core.Text.TextUtil.NaturalComparer).ToList();
        var palette = o.Distinct
            ? PaletteGenerator.Distinct(keys.Count)
            : PaletteGenerator.Generate(RgbColor.TryParse(o.BaseColor, out var b) ? b : RgbColor.Gray, o.Harmony, keys.Count);
        var themeName = $"{paramName} [{string.Join(", ", chosen.Select(c => c.Name).Take(3))}]";
        var lib = ProjectStorage.GetSection<ThemeLibrary>(ctx.Doc, ProjectSettings.SectionThemes);
        lib.Themes.TryGetValue(themeName, out var oldTheme);
        var theme = new ColorTheme { Name = themeName, Parameter = paramName, Categories = chosen.Select(c => c.Name).ToList() };
        for (var i = 0; i < keys.Count; i++)
        {
            var hex = o.ReuseTheme && oldTheme?.Find(keys[i]) is { } old ? old.ColorHex : palette[i].Hex;
            theme.Entries.Add(new ThemeEntry { Key = keys[i], ColorHex = hex, Transparency = o.Transparency });
        }

        ctx.Modify(ctx.Tool.Name, () =>
        {
            var target = view;
            if (o.Target == ColorTarget.Duplicate && view.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
            {
                var nid = view.Duplicate(ViewDuplicateOption.WithDetailing);
                target = (View)ctx.Doc.GetElement(nid);
                target.Name = Lookup.UniqueViewName(ctx.Doc, $"{view.Name} — {paramName}");
            }
            if (target.ViewTemplateId != ElementId.InvalidElementId)
                ctx.Report.Warn(Loc.T("A vista usa um modelo de vista: os filtros podem ficar controlados pelo modelo.", "The view uses a view template: filters may be controlled by the template."));
            foreach (var entry in theme.Entries)
            {
                var (type, raw, ids) = values[entry.Key];
                FilterRule rule = type switch
                {
                    StorageType.Integer => ParameterFilterRuleFactory.CreateEqualsRule(paramId, (int)raw),
                    StorageType.Double => ParameterFilterRuleFactory.CreateEqualsRule(paramId, (double)raw, 1e-6),
                    StorageType.ElementId => ParameterFilterRuleFactory.CreateEqualsRule(paramId, (ElementId)raw),
                    _ => ParameterFilterRuleFactory.CreateEqualsRule(paramId, (string)raw),
                };
                var name = Core.Text.TextUtil.SafeFileName($"TT Cor {paramName} = {entry.Key}", "TT Cor");
                if (name.Length > 120) name = name[..120];
                var filter = ViewFilters.Ensure(ctx.Doc, name, catIds, new ElementParameterFilter(rule));
                ViewFilters.Apply(target, filter.Id, ViewFilters.Color(ctx.Doc, entry.Color, o.Transparency, o.ColorCut));
                ctx.Report.Count(Loc.T("Filtros", "Filters"));
            }
            lib.Themes[themeName] = theme;
            ProjectStorage.SetSection(ctx.Doc, ProjectSettings.SectionThemes, lib);
            if (o.CreateLegend) Legend.Create(ctx, theme, o.TextType, values.ToDictionary(v => v.Key, v => v.Value.Ids.Count));
        });
        var t = new TableData(Loc.T("Tema de cores", "Color theme"));
        t.AddColumn(Loc.T("Valor", "Value")).AddColumn(Loc.T("Cor", "Color")).AddColumn(Loc.T("Elementos", "Elements"), ColumnKind.Integer);
        foreach (var e in theme.Entries) t.AddRowWithIds(values[e.Key].Ids, e.Key, e.ColorHex, values[e.Key].Ids.Count);
        ctx.Report.Tables.Add(t);
    }

    internal static string ParamName(Document doc, ElementId id)
    {
        if (id.Value < 0)
        {
            try
            {
                return LabelUtils.GetLabelFor((BuiltInParameter)id.Value);
            }
            catch
            {
                return "";
            }
        }
        return doc.GetElement(id)?.Name ?? "";
    }

    internal static Parameter? FindParam(Element e, ElementId paramId)
    {
        if (paramId.Value < 0)
        {
            var p = e.get_Parameter((BuiltInParameter)paramId.Value);
            if (p != null) return p;
        }
        else
        {
            foreach (Parameter p in e.Parameters)
                if (p.Id == paramId) return p;
        }
        var type = e.Document.GetElement(e.GetTypeId());
        if (type == null) return null;
        if (paramId.Value < 0) return type.get_Parameter((BuiltInParameter)paramId.Value);
        foreach (Parameter p in type.Parameters)
            if (p.Id == paramId) return p;
        return null;
    }
}

/// <summary>Legendas de cores em vistas de desenho (amostras + texto).</summary>
internal static class Legend
{
    public static View? Create(ToolContext ctx, ColorTheme theme, string? textType, IReadOnlyDictionary<string, int>? counts = null)
    {
        var doc = ctx.Doc;
        var vft = Lookup.ViewFamilyType(doc, ViewFamily.Drafting);
        var baseRegion = Lookup.Collect<FilledRegionType>(doc).FirstOrDefault();
        if (vft == null || baseRegion == null)
        {
            ctx.Report.Info(Loc.T("Legenda não criada (tipo de vista de desenho ou região preenchida indisponível).", "Legend not created (drafting view or filled region type unavailable)."));
            return null;
        }
        var view = ViewDrafting.Create(doc, vft.Id);
        view.Name = Lookup.UniqueViewName(doc, Loc.F("TT Legenda — {0}", "TT Legend — {0}", theme.Name));
        var scale = view.Scale;
        var textTypeId = Lookup.Named<TextNoteType>(doc, textType)?.Id ?? doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        var solid = ViewFilters.SolidFill(doc);
        var sw = U.Mm(10) * scale;
        var sh = U.Mm(6) * scale;
        var step = U.Mm(9) * scale;
        TextNote.Create(doc, view.Id, new XYZ(0, step, 0), U.Mm(120) * scale, theme.Name, textTypeId);
        var y = 0.0;
        foreach (var e in theme.Entries)
        {
            var typeName = "TT Cor " + e.ColorHex;
            var frt = Lookup.Named<FilledRegionType>(doc, typeName);
            if (frt == null)
            {
                frt = (FilledRegionType)baseRegion.Duplicate(typeName);
                if (solid != ElementId.InvalidElementId) frt.ForegroundPatternId = solid;
                frt.ForegroundPatternColor = new Color(e.Color.R, e.Color.G, e.Color.B);
            }
            var loop = RevitGeo.PolygonToLoop(Polygon2.Rectangle(new Vec2(0, y - sh), sw, sh), 0);
            FilledRegion.Create(doc, frt.Id, view.Id, new List<CurveLoop> { loop });
            var label = counts != null && counts.TryGetValue(e.Key, out var n) ? $"{e.Key}  ({n})" : e.Key;
            TextNote.Create(doc, view.Id, new XYZ(sw * 1.4, y, 0), U.Mm(100) * scale, label, textTypeId);
            y -= step;
        }
        ctx.Report.Count(Loc.T("Legendas", "Legends"));
        ctx.Report.AddResult(view.Id.Value);
        return view;
    }
}

public enum PresentationStyle
{
    [Text("Linhas ocultas", "Hidden line")] HLR,
    [Text("Sombreado", "Shaded")] Shading,
    [Text("Sombreado com arestas", "Shaded with edges")] ShadingWithEdges,
    [Text("Cores consistentes", "Consistent colors")] FlatColors,
    [Text("Realista", "Realistic")] Realistic,
    [Text("Realista com arestas", "Realistic with edges")] RealisticWithEdges,
}

public enum ViewDetail
{
    [Text("Baixo", "Coarse")] Coarse,
    [Text("Médio", "Medium")] Medium,
    [Text("Alto", "Fine")] Fine,
}

