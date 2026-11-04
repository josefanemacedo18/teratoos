using Autodesk.Revit.DB;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Criação de tabelas (schedules) com campos por BuiltInParameter (independe do idioma do Revit),
/// por nome de parâmetro (compartilhado/projeto) ou contagem.
/// Campos: "BIP:ROOM_AREA", "TT_Largura", "#count".
/// </summary>
public static class Schedules
{
    public sealed record Spec(string Field, string? Heading = null, bool Hidden = false, bool Totals = false);

    public static ViewSchedule Create(Document doc, BuiltInCategory category, string name, IEnumerable<Spec> fields,
        IEnumerable<(string Field, ScheduleFilterType Type, string Value)>? filters = null,
        IEnumerable<string>? sortBy = null, bool itemized = true, OperationReport? report = null)
    {
        var vs = ViewSchedule.CreateSchedule(doc, new ElementId(category));
        return Configure(doc, vs, name, fields, filters, sortBy, itemized, report);
    }

    /// <summary>Configura uma tabela já criada (ex.: lista de folhas).</summary>
    public static ViewSchedule Configure(Document doc, ViewSchedule vs, string name, IEnumerable<Spec> fields,
        IEnumerable<(string Field, ScheduleFilterType Type, string Value)>? filters = null,
        IEnumerable<string>? sortBy = null, bool itemized = true, OperationReport? report = null)
    {
        try
        {
            vs.Name = Lookup.UniqueViewName(doc, name);
        }
        catch
        {
            // mantém nome automático
        }
        var def = vs.Definition;
        var available = def.GetSchedulableFields();
        var added = new Dictionary<string, ScheduleField>(StringComparer.OrdinalIgnoreCase);

        ScheduleField? AddField(string spec)
        {
            if (added.TryGetValue(spec, out var existing)) return existing;
            var sf = Find(doc, available, spec);
            if (sf == null)
            {
                report?.Info(Loc.F("Campo \"{0}\" não disponível na tabela {1}.", "Field \"{0}\" not available in schedule {1}.", spec, name));
                return null;
            }
            var f = def.AddField(sf);
            added[spec] = f;
            return f;
        }

        foreach (var s in fields)
        {
            var f = AddField(s.Field);
            if (f == null) continue;
            if (!string.IsNullOrWhiteSpace(s.Heading)) f.ColumnHeading = s.Heading;
            if (s.Hidden) f.IsHidden = true;
            if (s.Totals)
            {
                try
                {
                    f.DisplayType = ScheduleFieldDisplayType.Totals;
                }
                catch
                {
                    // campo não suporta totais
                }
            }
        }
        if (filters != null)
        {
            foreach (var (field, type, value) in filters)
            {
                var f = AddField(field);
                if (f == null) continue;
                if (!fields.Any(x => string.Equals(x.Field, field, StringComparison.OrdinalIgnoreCase))) f.IsHidden = true;
                try
                {
                    def.AddFilter(new ScheduleFilter(f.FieldId, type, value));
                }
                catch (Exception ex)
                {
                    report?.Info(Loc.F("Filtro em \"{0}\" não aplicado: {1}", "Filter on \"{0}\" not applied: {1}", field, ex.Message));
                }
            }
        }
        if (sortBy != null)
        {
            foreach (var s in sortBy)
            {
                var f = AddField(s);
                if (f != null) def.AddSortGroupField(new ScheduleSortGroupField(f.FieldId));
            }
        }
        def.IsItemized = itemized;
        def.ShowGrandTotal = true;
        def.ShowGrandTotalCount = true;
        return vs;
    }

    private static SchedulableField? Find(Document doc, IList<SchedulableField> available, string spec)
    {
        if (spec == "#count") return available.FirstOrDefault(f => f.FieldType == ScheduleFieldType.Count);
        if (spec.StartsWith("BIP:", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<BuiltInParameter>(spec[4..], out var bip))
        {
            var id = new ElementId(bip);
            return available.FirstOrDefault(f => f.ParameterId == id && f.FieldType == ScheduleFieldType.Instance)
                   ?? available.FirstOrDefault(f => f.ParameterId == id);
        }
        return available.FirstOrDefault(f => string.Equals(f.GetName(doc), spec, StringComparison.OrdinalIgnoreCase));
    }
}
