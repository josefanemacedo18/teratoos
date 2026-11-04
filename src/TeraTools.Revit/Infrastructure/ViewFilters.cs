using Autodesk.Revit.DB;
using TeraTools.Core.Colors;
using TeraTools.Revit.Families;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Filtros de vista e sobreposições gráficas (cores por valor, estados, temas).</summary>
public static class ViewFilters
{
    public static ElementId SharedParamId(Document doc, string name)
    {
        var def = SharedParams.Get(name);
        return SharedParameterElement.Lookup(doc, new Guid(def.Guid))?.Id ?? ElementId.InvalidElementId;
    }

    /// <summary>Cria (ou substitui) um filtro de vista com a regra informada.</summary>
    public static ParameterFilterElement Ensure(Document doc, string name, ICollection<ElementId> categoryIds, ElementFilter filter)
    {
        var existing = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>()
            .FirstOrDefault(f => f.Name == name);
        if (existing != null)
        {
            existing.SetCategories(categoryIds);
            existing.SetElementFilter(filter);
            return existing;
        }
        return ParameterFilterElement.Create(doc, name, categoryIds, filter);
    }

    public static ElementId SolidFill(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill)?.Id ?? ElementId.InvalidElementId;

    public static OverrideGraphicSettings Color(Document doc, RgbColor c, int transparency = 0, bool cut = true, bool lines = false)
    {
        var col = new Color(c.R, c.G, c.B);
        var solid = SolidFill(doc);
        var ogs = new OverrideGraphicSettings();
        if (solid != ElementId.InvalidElementId)
        {
            ogs.SetSurfaceForegroundPatternId(solid);
            ogs.SetSurfaceForegroundPatternColor(col);
            if (cut)
            {
                ogs.SetCutForegroundPatternId(solid);
                ogs.SetCutForegroundPatternColor(col);
            }
        }
        if (lines) ogs.SetProjectionLineColor(col);
        if (transparency > 0) ogs.SetSurfaceTransparency(Math.Clamp(transparency, 0, 100));
        return ogs;
    }

    /// <summary>Aplica o filtro à vista (se ainda não aplicado) com sobreposição e visibilidade.</summary>
    public static void Apply(View v, ElementId filterId, OverrideGraphicSettings? ogs, bool visible = true)
    {
        if (!v.IsFilterApplied(filterId)) v.AddFilter(filterId);
        if (ogs != null) v.SetFilterOverrides(filterId, ogs);
        v.SetFilterVisibility(filterId, visible);
    }

    public static ICollection<ElementId> CategoryIds(params BuiltInCategory[] cats) => cats.Select(c => new ElementId(c)).ToList();
}
