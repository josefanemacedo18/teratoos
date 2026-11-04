using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Selection;

public enum SearchScope
{
    [Text("Vista ativa", "Active view")] ActiveView,
    [Text("Projeto inteiro", "Whole project")] Project,
    [Text("Seleção atual", "Current selection")] Selection,
}

public enum GroupBy
{
    [Text("Categoria", "Category")] Category,
    [Text("Família", "Family")] Family,
    [Text("Família e tipo", "Family and type")] Type,
    [Text("Material", "Material")] Material,
    [Text("Nível", "Level")] Level,
    [Text("Ambiente", "Room")] Room,
    [Text("Fase de criação", "Phase created")] Phase,
    [Text("Sem filtro de grupo (só parâmetro)", "No grouping (parameter only)")] None,
}

public enum ParamOperator
{
    [Text("(sem condição)", "(no condition)")] None,
    [Text("igual a", "equals")] Equals,
    [Text("diferente de", "not equal to")] NotEquals,
    [Text("contém", "contains")] Contains,
    [Text("não contém", "does not contain")] NotContains,
    [Text("começa com", "starts with")] StartsWith,
    [Text("maior que", "greater than")] Greater,
    [Text("menor que", "less than")] Less,
    [Text("está vazio", "is empty")] Empty,
    [Text("não está vazio", "is not empty")] NotEmpty,
    [Text("expressão regular", "regular expression")] Regex,
}

public enum FilterAction
{
    [Text("Selecionar", "Select")] Select,
    [Text("Isolar temporariamente", "Isolate temporarily")] Isolate,
    [Text("Ocultar temporariamente", "Hide temporarily")] HideTemporary,
    [Text("Ocultar na vista (permanente)", "Hide in view (permanent)")] Hide,
}

public sealed class FilterOptions
{
    [Opt("Onde procurar", "Search in", Group = "Escopo|Scope")]
    public SearchScope Scope { get; set; } = SearchScope.ActiveView;

    [Opt("Filtrar por", "Filter by", Group = "Escopo|Scope", Tip = "Depois de OK, você escolhe quais itens (categorias, tipos, materiais...) manter.|After OK you choose which items (categories, types, materials...) to keep.")]
    public GroupBy By { get; set; } = GroupBy.Category;

    [Opt("Incluir anotações", "Include annotations", Group = "Escopo|Scope")]
    public bool IncludeAnnotations { get; set; }

    [Opt("Parâmetro", "Parameter", Group = "Condição de parâmetro (opcional)|Parameter condition (optional)", Tip = "Nome do parâmetro como aparece nas propriedades.|Parameter name as shown in properties.")]
    public string Parameter { get; set; } = "";

    [Opt("Condição", "Condition", Group = "Condição de parâmetro (opcional)|Parameter condition (optional)")]
    public ParamOperator Operator { get; set; } = ParamOperator.None;

    [Opt("Valor", "Value", Group = "Condição de parâmetro (opcional)|Parameter condition (optional)", VisibleIf = "Operator!=None,Empty,NotEmpty")]
    public string Value { get; set; } = "";

    [Opt("Ação", "Action", Group = "Resultado|Result")]
    public FilterAction Action { get; set; } = FilterAction.Select;
}

internal static class SelectionUtil
{
    public static List<Element> Collect(ToolContext ctx, SearchScope scope, bool annotations)
    {
        IEnumerable<Element> els = scope switch
        {
            SearchScope.Selection => ctx.Selected(),
            SearchScope.Project => new FilteredElementCollector(ctx.Doc).WhereElementIsNotElementType(),
            _ => new FilteredElementCollector(ctx.Doc, ctx.ActiveView.Id).WhereElementIsNotElementType(),
        };
        return els.Where(e => e.Category != null && e is not View && !(e is ElementType)
                              && (annotations || e.Category.CategoryType == CategoryType.Model)
                              && e.Category.Id.Value != (long)BuiltInCategory.OST_Cameras
                              && e.Category.Id.Value != (long)BuiltInCategory.OST_SectionBox)
                  .ToList();
    }

    public static IEnumerable<string> Keys(Element e, GroupBy by, List<Room> rooms)
    {
        var doc = e.Document;
        switch (by)
        {
            case GroupBy.Category:
                yield return e.Category?.Name ?? "-";
                break;
            case GroupBy.Family:
                yield return e is FamilyInstance fi ? $"{fi.Category?.Name}: {fi.Symbol.FamilyName}" : $"{e.Category?.Name}: {(doc.GetElement(e.GetTypeId()) as ElementType)?.FamilyName}";
                break;
            case GroupBy.Type:
                yield return Lookup.ElementLabel(e);
                break;
            case GroupBy.Material:
                ICollection<ElementId> mats;
                try
                {
                    mats = e.GetMaterialIds(false);
                }
                catch
                {
                    mats = new List<ElementId>();
                }
                if (mats.Count == 0) yield return Loc.T("(sem material)", "(no material)");
                foreach (var m in mats) yield return doc.GetElement(m)?.Name ?? "-";
                break;
            case GroupBy.Level:
                yield return RevitGeo.LevelOf(e)?.Name ?? Loc.T("(sem nível)", "(no level)");
                break;
            case GroupBy.Room:
                yield return RoomOf(e, rooms)?.Name ?? Loc.T("(fora de ambientes)", "(outside rooms)");
                break;
            case GroupBy.Phase:
                yield return e.CreatedPhaseId != ElementId.InvalidElementId ? doc.GetElement(e.CreatedPhaseId)?.Name ?? "-" : "-";
                break;
            default:
                yield return Loc.T("Todos", "All");
                break;
        }
    }

    public static Room? RoomOf(Element e, List<Room> rooms)
    {
        if (e is FamilyInstance fi)
        {
            try
            {
                if (fi.Room != null) return fi.Room;
            }
            catch
            {
                // sem fase compatível
            }
        }
        var a = RevitGeo.Anchor(e);
        return a == null ? null : RevitGeo.RoomAt(e.Document, a, rooms);
    }

    public static bool Matches(Element e, FilterOptions o)
    {
        if (o.Operator == ParamOperator.None || string.IsNullOrWhiteSpace(o.Parameter)) return true;
        var p = e.LookupParameter(o.Parameter) ?? (e.Document.GetElement(e.GetTypeId()))?.LookupParameter(o.Parameter);
        var text = RevitGeo.Display(p);
        switch (o.Operator)
        {
            case ParamOperator.Empty: return string.IsNullOrWhiteSpace(text);
            case ParamOperator.NotEmpty: return !string.IsNullOrWhiteSpace(text);
        }
        if (p == null) return false;
        var v = o.Value ?? "";
        switch (o.Operator)
        {
            case ParamOperator.Equals: return string.Equals(text.Trim(), v.Trim(), StringComparison.OrdinalIgnoreCase);
            case ParamOperator.NotEquals: return !string.Equals(text.Trim(), v.Trim(), StringComparison.OrdinalIgnoreCase);
            case ParamOperator.Contains: return text.Contains(v, StringComparison.OrdinalIgnoreCase);
            case ParamOperator.NotContains: return !text.Contains(v, StringComparison.OrdinalIgnoreCase);
            case ParamOperator.StartsWith: return text.StartsWith(v, StringComparison.OrdinalIgnoreCase);
            case ParamOperator.Regex:
                try
                {
                    return Regex.IsMatch(text, v, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                }
                catch
                {
                    return false;
                }
            case ParamOperator.Greater:
            case ParamOperator.Less:
                double a, b;
                if (p.StorageType == StorageType.Double)
                {
                    a = p.AsDouble();
                    try
                    {
                        if (!UnitFormatUtils.TryParse(e.Document.GetUnits(), p.Definition.GetDataType(), v, out b) && !U.TryParseNumber(v, out b)) return false;
                    }
                    catch
                    {
                        if (!U.TryParseNumber(v, out b)) return false;
                    }
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    a = p.AsInteger();
                    if (!U.TryParseNumber(v, out b)) return false;
                }
                else
                {
                    if (!U.TryParseNumber(text, out a) || !U.TryParseNumber(v, out b)) return false;
                }
                return o.Operator == ParamOperator.Greater ? a > b + 1e-9 : a < b - 1e-9;
            default:
                return true;
        }
    }

    public static void Apply(ToolContext ctx, List<ElementId> ids, FilterAction action)
    {
        if (ids.Count == 0)
        {
            Msg.Info(Loc.T("Nenhum elemento atende aos critérios.", "No elements match the criteria."));
            return;
        }
        var view = ctx.ActiveView;
        switch (action)
        {
            case FilterAction.Isolate:
                ctx.Modify(Loc.T("Isolar", "Isolate"), () => view.IsolateElementsTemporary(ids));
                break;
            case FilterAction.HideTemporary:
                ctx.Modify(Loc.T("Ocultar", "Hide"), () => view.HideElementsTemporary(ids));
                break;
            case FilterAction.Hide:
                ctx.Modify(Loc.T("Ocultar", "Hide"), () =>
                {
                    var hideable = ids.Where(id => ctx.Doc.GetElement(id)?.CanBeHidden(view) == true).ToList();
                    if (hideable.Count > 0) view.HideElements(hideable);
                });
                break;
        }
        ctx.UiDoc.Selection.SetElementIds(ids);
        ctx.Report.Count(Loc.T("Elementos", "Elements"), ids.Count);
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.filter", Modules.Selection, "Filtro avançado", "Advanced filter", Order = 10, Large = true,
    DescPt = "Seleciona, isola ou oculta por categoria, família, tipo, material, nível, ambiente ou fase, com condição de parâmetro.",
    DescEn = "Selects, isolates or hides by category, family, type, material, level, room or phase, with a parameter condition.",
    HelpPt = "1. Escolha onde procurar (vista, projeto ou seleção) e o critério de agrupamento.\n2. Opcionalmente, defina uma condição de parâmetro (ex.: Comentários contém \"demolir\").\n3. Na lista seguinte, marque os grupos que deseja (a quantidade aparece ao lado).\n\nComparações 'maior/menor que' entendem as unidades do projeto (ex.: 2,10 m).",
    HelpEn = "1. Choose where to search and how to group.\n2. Optionally set a parameter condition.\n3. In the next list, check the groups you want.\n\n'Greater/less than' comparisons understand project units.",
    Keywords = "filtro selecionar categoria familia tipo material nivel ambiente parametro filter select")]
public sealed class FilterCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<FilterOptions>();
        var els = SelectionUtil.Collect(ctx, o.Scope, o.IncludeAnnotations).Where(e => SelectionUtil.Matches(e, o)).ToList();
        if (els.Count == 0)
        {
            Msg.Info(Loc.T("Nenhum elemento atende à condição.", "No elements match the condition."));
            ctx.Cancel();
        }
        var rooms = o.By == GroupBy.Room ? RevitGeo.Rooms(ctx.Doc).Where(r => r.Area > 0).ToList() : new List<Room>();
        var groups = new Dictionary<string, List<ElementId>>();
        foreach (var e in els)
            foreach (var k in SelectionUtil.Keys(e, o.By, rooms).Distinct())
            {
                if (!groups.TryGetValue(k, out var l)) groups[k] = l = new List<ElementId>();
                l.Add(e.Id);
            }
        List<string> chosen;
        if (o.By == GroupBy.None || groups.Count == 1)
        {
            chosen = groups.Keys.ToList();
        }
        else
        {
            var keys = groups.Keys.OrderBy(k => k, Core.Text.TextUtil.NaturalComparer).ToList();
            chosen = Pick.Many(keys, k => k, ctx.Tool.Name, Loc.T("Marque os itens a manter:", "Check the items to keep:"), ctx.Owner,
                detail: k => Loc.F("{0} elemento(s)", "{0} element(s)", groups[k].Count));
            if (chosen.Count == 0) ctx.Cancel();
        }
        var ids = chosen.SelectMany(k => groups[k]).Distinct().ToList();
        SelectionUtil.Apply(ctx, ids, o.Action);
    }
}

public sealed class SimilarOptions
{
    [Opt("Onde procurar", "Search in")]
    public SearchScope Scope { get; set; } = SearchScope.ActiveView;

    [Opt("Mesmo nível", "Same level")]
    public bool SameLevel { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.similar", Modules.Selection, "Selecionar semelhantes", "Select similar", Order = 20,
    DescPt = "Seleciona todos os elementos do mesmo tipo dos selecionados (na vista ou no projeto).",
    DescEn = "Selects all elements of the same type as the selected ones (in the view or project).",
    Keywords = "semelhantes mesmo tipo similar same type")]
public sealed class SimilarCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var sel = ctx.SelectedOrPick("Selecione elementos de referência", "Select reference elements", e => e.GetTypeId() != ElementId.InvalidElementId);
        var o = ctx.Ask<SimilarOptions>();
        var types = sel.Select(e => e.GetTypeId().Value).ToHashSet();
        var levels = sel.Select(e => RevitGeo.LevelOf(e)?.Id.Value ?? -1).ToHashSet();
        var scope = o.Scope == SearchScope.Selection ? SearchScope.ActiveView : o.Scope;
        var ids = SelectionUtil.Collect(ctx, scope, true)
            .Where(e => types.Contains(e.GetTypeId().Value) && (!o.SameLevel || levels.Contains(RevitGeo.LevelOf(e)?.Id.Value ?? -1)))
            .Select(e => e.Id).ToList();
        SelectionUtil.Apply(ctx, ids, FilterAction.Select);
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.isolate", Modules.Selection, "Isolar", "Isolate", Order = 30, Group = "Visibilidade|Visibility",
    DescPt = "Isola temporariamente os elementos selecionados na vista ativa.",
    DescEn = "Temporarily isolates the selected elements in the active view.",
    Keywords = "isolar isolate visibilidade")]
public sealed class IsolateCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione os elementos a isolar", "Select elements to isolate");
        SelectionUtil.Apply(ctx, els.Select(e => e.Id).ToList(), FilterAction.Isolate);
    }
}

public sealed class HideOptions
{
    [Opt("Ocultar permanentemente na vista", "Hide permanently in view", Tip = "Desligado: ocultação temporária (óculos).|Off: temporary hide (glasses icon).")]
    public bool Permanent { get; set; }

    [Opt("Ocultar a categoria inteira", "Hide the whole category")]
    public bool WholeCategory { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.hide", Modules.Selection, "Ocultar", "Hide", Order = 31, Group = "Visibilidade|Visibility",
    DescPt = "Oculta os elementos (ou categorias) selecionados, temporária ou permanentemente.",
    DescEn = "Hides the selected elements (or categories), temporarily or permanently.",
    Keywords = "ocultar esconder hide")]
public sealed class HideCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione os elementos a ocultar", "Select elements to hide");
        var o = ctx.Ask<HideOptions>();
        var view = ctx.ActiveView;
        if (o.WholeCategory)
        {
            ctx.Modify(ctx.Tool.Name, () =>
            {
                foreach (var cat in els.Select(e => e.Category).Where(c => c != null).DistinctBy(c => c.Id.Value))
                {
                    if (view.CanCategoryBeHidden(cat.Id))
                    {
                        view.SetCategoryHidden(cat.Id, true);
                        ctx.Report.Count(Loc.T("Categorias ocultas", "Categories hidden"));
                    }
                }
            });
            return;
        }
        SelectionUtil.Apply(ctx, els.Select(e => e.Id).ToList(), o.Permanent ? FilterAction.Hide : FilterAction.HideTemporary);
        ctx.UiDoc.Selection.SetElementIds(new List<ElementId>());
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.reveal", Modules.Selection, "Revelar ocultos", "Reveal hidden", Order = 32, Group = "Visibilidade|Visibility",
    DescPt = "Desativa isolamento/ocultação temporária e reexibe elementos ocultos na vista ativa.",
    DescEn = "Disables temporary isolate/hide and unhides hidden elements in the active view.",
    Keywords = "revelar mostrar reexibir reveal unhide show")]
public sealed class RevealCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var view = ctx.ActiveView;
        if (view.IsTemplate) ToolContext.Fail("A vista ativa é um modelo de vista.", "The active view is a view template.");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            try
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }
            catch
            {
                // não havia modo temporário
            }
            var hidden = new FilteredElementCollector(ctx.Doc).WhereElementIsNotElementType()
                .Where(e => e.Category != null && SafeHidden(e, view)).Select(e => e.Id).ToList();
            if (hidden.Count > 0)
            {
                view.UnhideElements(hidden);
                ctx.Report.Count(Loc.T("Elementos reexibidos", "Elements unhidden"), hidden.Count);
            }
            else
            {
                ctx.Report.Info(Loc.T("Não havia elementos ocultos individualmente nesta vista.", "There were no individually hidden elements in this view."));
            }
        });
    }

    private static bool SafeHidden(Element e, View v)
    {
        try
        {
            return e.IsHidden(v);
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ByRoomOptions
{
    [Opt("Somente categorias de interiores", "Interior categories only", Tip = "Mobiliário, marcenaria, luminárias, louças, equipamentos e modelos genéricos.|Furniture, casework, lighting, plumbing, equipment and generic models.")]
    public bool InteriorOnly { get; set; } = true;

    [Opt("Ação", "Action")]
    public FilterAction Action { get; set; } = FilterAction.Select;
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.byroom", Modules.Selection, "Elementos do ambiente", "Elements in room", Order = 40,
    DescPt = "Seleciona (ou isola) os elementos que estão dentro dos ambientes escolhidos.",
    DescEn = "Selects (or isolates) the elements inside the chosen rooms.",
    Keywords = "ambiente sala room elementos dentro filtrar")]
public sealed class ByRoomCommand : ToolCommand
{
    private static readonly BuiltInCategory[] Interior =
    {
        BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems, BuiltInCategory.OST_Casework,
        BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_SpecialityEquipment,
        BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_MechanicalEquipment,
        BuiltInCategory.OST_Planting, BuiltInCategory.OST_Entourage, BuiltInCategory.OST_FoodServiceEquipment,
    };

    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room).OfType<Room>().Where(r => r.Area > 0).ToList();
        if (rooms.Count == 0) ToolContext.Fail("Nenhum ambiente válido (colocado e fechado) selecionado.", "No valid (placed and enclosed) room selected.");
        var o = ctx.Ask<ByRoomOptions>();
        var cats = Interior.Select(c => (long)c).ToHashSet();
        var ids = new FilteredElementCollector(ctx.Doc).WhereElementIsNotElementType()
            .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model && e is not Room && (!o.InteriorOnly || cats.Contains(e.Category.Id.Value)))
            .Where(e => SelectionUtil.RoomOf(e, rooms) is { } r && rooms.Any(x => x.Id == r.Id))
            .Select(e => e.Id).ToList();
        SelectionUtil.Apply(ctx, ids, o.Action);
    }
}

// =================================================================== Análise

public enum ClashTarget
{
    [Text("Entre os próprios elementos selecionados", "Among the selected elements")] SameSet,
    [Text("Selecionados × categorias escolhidas", "Selected × chosen categories")] Categories,
}

public sealed class ClashOptions
{
    [Opt("Verificar", "Check")]
    public ClashTarget Target { get; set; } = ClashTarget.SameSet;

    [Opt("Ignorar elementos unidos (paredes/pisos)", "Ignore joined elements (walls/floors)")]
    public bool IgnoreJoined { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("analysis.clash", Modules.Analysis, "Verificar colisões", "Check clashes", Order = 10, Large = true,
    DescPt = "Encontra interferências geométricas entre os elementos selecionados ou contra categorias escolhidas.",
    DescEn = "Finds geometric interferences among the selected elements or against chosen categories.",
    HelpPt = "Usa a geometria real dos elementos (não só as caixas). No relatório, clique duas vezes em uma colisão para selecionar o par no modelo.",
    HelpEn = "Uses the actual element geometry (not just boxes). In the report, double-click a clash to select the pair in the model.",
    Keywords = "colisao interferencia conflito clash interference")]
public sealed class ClashCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var a = ctx.SelectedOrPick("Selecione os elementos a verificar", "Select the elements to check", e => e.Category?.CategoryType == CategoryType.Model);
        var o = ctx.Ask<ClashOptions>();
        List<ElementId>? candidates = null;
        if (o.Target == ClashTarget.Categories)
        {
            var cats = Lookup.ModelCategories(ctx.Doc).OrderBy(c => c.Name).ToList();
            var chosen = Pick.Many(cats, c => c.Name, ctx.Tool.Name, Loc.T("Verificar contra quais categorias?", "Check against which categories?"), ctx.Owner);
            if (chosen.Count == 0) ctx.Cancel();
            var bics = chosen.Select(c => (BuiltInCategory)c.Id.Value).ToList();
            candidates = new FilteredElementCollector(ctx.Doc).WherePasses(new ElementMulticategoryFilter(bics)).WhereElementIsNotElementType().ToElementIds().ToList();
        }
        var setA = a.Select(e => e.Id).ToHashSet();
        var pairs = new HashSet<(long, long)>();
        var table = new TableData(Loc.T("Colisões", "Clashes"));
        table.AddColumn(Loc.T("Elemento A", "Element A")).AddColumn(Loc.T("Elemento B", "Element B")).AddColumn(Loc.T("Ids", "Ids"));
        foreach (var e in a)
        {
            var b = RevitGeo.Box(e);
            if (b == null) continue;
            var outline = new Outline(b.Min, b.Max);
            var col = candidates != null
                ? new FilteredElementCollector(ctx.Doc, candidates)
                : new FilteredElementCollector(ctx.Doc, setA.ToList());
            IList<Element> hits;
            try
            {
                hits = col.WherePasses(new BoundingBoxIntersectsFilter(outline)).WherePasses(new ElementIntersectsElementFilter(e)).ToElements();
            }
            catch
            {
                continue;
            }
            foreach (var h in hits)
            {
                if (h.Id == e.Id) continue;
                if (o.IgnoreJoined && JoinGeometryUtils.AreElementsJoined(ctx.Doc, e, h)) continue;
                var key = e.Id.Value < h.Id.Value ? (e.Id.Value, h.Id.Value) : (h.Id.Value, e.Id.Value);
                if (!pairs.Add(key)) continue;
                var msg = $"{Lookup.ElementLabel(e)}  ×  {Lookup.ElementLabel(h)}";
                ctx.Report.Warn(msg, new[] { e.Id.Value, h.Id.Value });
                table.AddRowWithIds(new[] { e.Id.Value, h.Id.Value }, Lookup.ElementLabel(e), Lookup.ElementLabel(h), $"{e.Id.Value} / {h.Id.Value}");
            }
        }
        ctx.Report.Count(Loc.T("Colisões", "Clashes"), pairs.Count);
        if (pairs.Count == 0) ctx.Report.Success(Loc.T("Nenhuma colisão encontrada.", "No clashes found."));
        else ctx.Report.Tables.Add(table);
        ctx.Report.ForceShow = true;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("analysis.measure", Modules.Analysis, "Medir seleção", "Measure selection", Order = 20,
    DescPt = "Soma comprimentos, áreas, perímetros e volumes dos elementos selecionados.",
    DescEn = "Sums lengths, areas, perimeters and volumes of the selected elements.",
    Keywords = "medir medicao area volume perimetro comprimento measure")]
public sealed class MeasureCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione os elementos a medir", "Select the elements to measure");
        var t = new TableData(Loc.T("Medições", "Measurements"));
        t.AddColumn(Loc.T("Elemento", "Element")).AddColumn(Loc.T("Comprimento (m)", "Length (m)"), ColumnKind.Number)
         .AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number).AddColumn(Loc.T("Perímetro (m)", "Perimeter (m)"), ColumnKind.Number)
         .AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3)
         .AddColumn(Loc.T("Caixa L × P × A (m)", "Box W × D × H (m)"));
        foreach (var e in els)
        {
            double len = 0, area = 0, per = 0, vol = 0;
            if (e.Location is LocationCurve lc) len = lc.Curve.Length;
            else if (e is CurveElement ce) len = ce.GeometryCurve.Length;
            else len = Get(e, BuiltInParameter.CURVE_ELEM_LENGTH);
            area = e is SpatialElement se ? se.Area : Get(e, BuiltInParameter.HOST_AREA_COMPUTED);
            per = e is SpatialElement se2 ? se2.Perimeter : Get(e, BuiltInParameter.HOST_PERIMETER_COMPUTED);
            vol = e is Room rm ? rm.Volume : Get(e, BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (vol <= 0) vol = RevitGeo.Solids(e).Sum(s => s.Volume);
            var b = RevitGeo.Box(e);
            var box = b == null ? "" : $"{U.ToM(b.Max.X - b.Min.X):0.00} × {U.ToM(b.Max.Y - b.Min.Y):0.00} × {U.ToM(b.Max.Z - b.Min.Z):0.00}";
            t.AddRowWithIds(new[] { e.Id.Value }, Lookup.ElementLabel(e), U.ToM(len), U.SqFtToM2(area), U.ToM(per), U.CuFtToM3(vol), box);
        }
        t.AddTotalsRow(Loc.T("TOTAL", "TOTAL"), 1, 2, 3, 4);
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
        ctx.Report.Info(Loc.F("{0} elementos medidos.", "{0} elements measured.", els.Count));
    }

    private static double Get(Element e, BuiltInParameter bip)
    {
        var p = e.get_Parameter(bip);
        return p is { StorageType: StorageType.Double, HasValue: true } ? p.AsDouble() : 0;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("analysis.distance", Modules.Analysis, "Medir distância", "Measure distance", Order = 30,
    DescPt = "Mede a distância entre dois pontos (total, em planta e em altura).",
    DescEn = "Measures the distance between two points (total, in plan and in height).",
    Keywords = "distancia medir pontos distance measure")]
public sealed class DistanceCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var a = ctx.PickPoint("Primeiro ponto", "First point");
        var b = ctx.PickPoint("Segundo ponto", "Second point");
        var d = b - a;
        var units = ctx.Doc.GetUnits();
        string F(double v) => UnitFormatUtils.Format(units, SpecTypeId.Length, v, false);
        Msg.Info(Loc.F("Distância: {0}\nEm planta: {1}\nΔX: {2}   ΔY: {3}   ΔZ: {4}",
            "Distance: {0}\nIn plan: {1}\nΔX: {2}   ΔY: {3}   ΔZ: {4}",
            F(d.GetLength()), F(new XYZ(d.X, d.Y, 0).GetLength()), F(Math.Abs(d.X)), F(Math.Abs(d.Y)), F(Math.Abs(d.Z))), ctx.Tool.Name);
    }
}
