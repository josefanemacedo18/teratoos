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
[Tool("selection.filter", Modules.Modeling, "Filtro avançado", "Advanced filter", Order = 10, Large = true,
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

public sealed class ByRoomOptions
{
    [Opt("Somente categorias de interiores", "Interior categories only", Tip = "Mobiliário, marcenaria, luminárias, louças, equipamentos e modelos genéricos.|Furniture, casework, lighting, plumbing, equipment and generic models.")]
    public bool InteriorOnly { get; set; } = true;

    [Opt("Ação", "Action")]
    public FilterAction Action { get; set; } = FilterAction.Select;
}

[Transaction(TransactionMode.Manual)]
[Tool("selection.byroom", Modules.Modeling, "Elementos do ambiente", "Elements in room", Order = 40,
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

