using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Checks;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Floors;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

/// <summary>Regras editáveis (salváveis como preset). Valores padrão são apenas referência.</summary>
public sealed class ErgoOptions : IValidatableOptions
{
    [Opt("Nome do conjunto de regras", "Rule set name", Group = "Referência|Reference")]
    public string RuleName { get; set; } = "Referência (editável)";

    [Opt("Nota de referência (vai para o relatório)", "Reference note (goes to the report)", Group = "Referência|Reference", Kind = OptKind.MultiLine,
        Tip = "Indique a norma e a versão que você adotou e quem validou.|State the standard and version you adopted and who validated it.")]
    public List<string> Note { get; set; } = new()
    {
        "Valores de referência editáveis: confirme com a norma vigente aplicável ao projeto (ex.: ABNT NBR 9050) e com o profissional responsável.",
    };

    [Opt("Verificar passagens entre móveis e paredes", "Check passages between items and walls", Group = "Circulação|Circulation")]
    public bool Circulation { get; set; } = true;

    [Opt("Largura mínima de passagem", "Minimum passage width", Kind = OptKind.Length, Min = 0, Group = "Circulação|Circulation", VisibleIf = "Circulation")]
    public double MinCirculation { get; set; } = U.M(0.90);

    [Opt("Ignorar vãos menores que (móveis encostados)", "Ignore gaps smaller than (touching items)", Kind = OptKind.Length, Min = 0, Group = "Circulação|Circulation", VisibleIf = "Circulation")]
    public double IgnoreGaps { get; set; } = U.M(0.05);

    [Opt("Verificar área de manobra (círculo livre)", "Check turning space (free circle)", Group = "Manobra|Turning")]
    public bool Turning { get; set; } = true;

    [Opt("Diâmetro de manobra", "Turning diameter", Kind = OptKind.Length, Min = 0.1, Group = "Manobra|Turning", VisibleIf = "Turning")]
    public double TurningDiameter { get; set; } = U.M(1.50);

    [Opt("Ambientes que exigem manobra (palavras no nome; vazio = todos)", "Rooms requiring turning space (words in name; empty = all)", Kind = OptKind.MultiLine, Group = "Manobra|Turning", VisibleIf = "Turning")]
    public List<string> TurningRooms { get; set; } = new() { "banh", "wc", "bwc", "lavabo", "sanit", "bath", "toilet" };

    [Opt("Verificar áreas de aproximação", "Check approach areas", Group = "Aproximação|Approach")]
    public bool Approach { get; set; } = true;

    [Opt("Largura da área de aproximação", "Approach area width", Kind = OptKind.Length, Min = 0.1, Group = "Aproximação|Approach", VisibleIf = "Approach")]
    public double ApproachWidth { get; set; } = U.M(0.80);

    [Opt("Profundidade da área de aproximação", "Approach area depth", Kind = OptKind.Length, Min = 0.1, Group = "Aproximação|Approach", VisibleIf = "Approach")]
    public double ApproachDepth { get; set; } = U.M(1.20);

    [Opt("Categorias que exigem aproximação", "Categories requiring approach", Kind = OptKind.MultiLine, Group = "Aproximação|Approach", VisibleIf = "Approach",
        Tip = "Nomes internos, ex.: OST_PlumbingFixtures, OST_Casework.|Internal names, e.g. OST_PlumbingFixtures, OST_Casework.")]
    public List<string> ApproachCategories { get; set; } = new() { "OST_PlumbingFixtures" };

    [Opt("Verificar abertura de portas", "Check door swings", Group = "Portas|Doors")]
    public bool DoorSwings { get; set; } = true;

    [Opt("Verificar alturas de bancadas", "Check counter heights", Group = "Alturas|Heights")]
    public bool Heights { get; set; } = true;

    [Opt("Altura mínima", "Minimum height", Kind = OptKind.Length, Group = "Alturas|Heights", VisibleIf = "Heights")]
    public double CounterMin { get; set; } = U.M(0.75);

    [Opt("Altura máxima", "Maximum height", Kind = OptKind.Length, Group = "Alturas|Heights", VisibleIf = "Heights")]
    public double CounterMax { get; set; } = U.M(0.85);

    [Opt("Palavras que identificam bancadas", "Words identifying counters", Kind = OptKind.MultiLine, Group = "Alturas|Heights", VisibleIf = "Heights")]
    public List<string> CounterKeywords { get; set; } = new() { "bancada", "balcão", "balcao", "counter", "lavatório", "lavatorio", "cuba" };

    [Opt("Desenhar marcações na vista ativa (planta)", "Draw markers in the active view (plan)", Group = "Saída|Output")]
    public bool Draw { get; set; } = true;

    [Opt("Tipo de região das marcações", "Marker region type", Kind = OptKind.FilledRegionType, Group = "Saída|Output", VisibleIf = "Draw")]
    public string RegionType { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (CounterMax < CounterMin) yield return Loc.T("A altura máxima deve ser maior que a mínima.", "Maximum height must be greater than minimum.");
    }

    public ErgonomicRules ToRules() => new()
    {
        Name = RuleName,
        ReferenceNote = string.Join(" ", Note),
        MinCirculation = MinCirculation,
        IgnoreGapsBelow = IgnoreGaps,
        TurningDiameter = TurningDiameter,
        ApproachWidth = ApproachWidth,
        ApproachDepth = ApproachDepth,
        ApproachCategories = ApproachCategories.Select(c => c.Trim()).Where(c => c.Length > 0).ToList(),
        CounterMinHeight = CounterMin,
        CounterMaxHeight = CounterMax,
        CounterKeywords = CounterKeywords.Select(TextUtil.NormalizeForCompare).Where(c => c.Length > 0).ToList(),
        CheckCirculation = Circulation,
        CheckTurningSpace = Turning,
        CheckApproach = Approach,
        CheckDoorSwings = DoorSwings,
        CheckHeights = Heights,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("ergo.check", Modules.Interiors, "Ergonomia e acessibilidade", "Ergonomics and accessibility", Order = 30, Large = true, Group = "Verificações|Checks",
    DescPt = "Verifica passagens, área de manobra, áreas de aproximação, abertura de portas e alturas de bancadas com regras editáveis — sem declarar conformidade.",
    DescEn = "Checks passages, turning space, approach areas, door swings and counter heights with editable rules — without declaring compliance.",
    HelpPt = "As regras são suas: ajuste os valores conforme a norma e a versão adotadas no projeto e salve como preset (pessoal ou do projeto). O relatório lista os pontos de atenção encontrados pelo modelo geométrico; ele NÃO substitui a verificação do profissional e NÃO declara conformidade.\n\nLimitações: as pegadas dos móveis são envoltórias convexas em planta; portas sem arco de abertura na planta têm a área estimada (quarto de círculo); vínculos (links) não são analisados.",
    HelpEn = "The rules are yours: adjust values to the standard and version adopted in the project and save as a preset. The report lists attention points found by the geometric model; it does NOT replace professional review and does NOT declare compliance.\n\nLimitations: furniture footprints are convex hulls in plan; doors without a swing arc in plan get an estimated (quarter-circle) area; links are not analyzed.",
    Keywords = "ergonomia acessibilidade nbr 9050 circulacao manobra aproximacao ergonomics accessibility clearance turning")]
public sealed class ErgonomicsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = RoomUtil.SelectedOrInView(ctx).Where(r => r.Area > 0).ToList();
        if (rooms.Count == 0) ToolContext.Fail("Nenhum ambiente para verificar.", "No room to check.");
        var o = ctx.Ask<ErgoOptions>();
        var rules = o.ToRules();
        var doc = ctx.Doc;
        var view = ctx.ActiveView;
        var drawIn = o.Draw && RevitGeo.IsPlanLike(view) ? view : null;
        if (o.Draw && drawIn == null) ctx.Report.Info(Loc.T("Abra uma planta para desenhar as marcações.", "Open a plan view to draw markers."));

        var turningWords = o.TurningRooms.Select(TextUtil.NormalizeForCompare).Where(w => w.Length > 0).ToList();
        var approachCats = rules.ApproachCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allIssues = new List<(Room Room, ErgonomicIssue Issue)>();
        var circles = new List<(Polygon2 Circle, bool Ok)>();
        var estimatedDoors = 0;
        var doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();

        foreach (var room in rooms)
        {
            var region = RevitGeo.RoomRegion(room);
            if (region == null) continue;
            var label = RoomUtil.Label(room);
            var items = InteriorsUtil.InstancesInRoom(doc, room, InteriorsUtil.FurnitureCats)
                .Select(InteriorsUtil.FootprintOf).Where(f => f.Outline.Count >= 3 && Math.Abs(f.Outline.Area) > U.M2ToSqFt(0.01)).ToList();
            var issues = new List<ErgonomicIssue>();

            if (rules.CheckCirculation) issues.AddRange(ErgonomicChecker.CheckCirculation(label, region, items, rules));

            if (rules.CheckTurningSpace && (turningWords.Count == 0 || turningWords.Any(w => TextUtil.NormalizeForCompare(room.Name).Contains(w))))
            {
                var t = ErgonomicChecker.FindTurningSpace(region, items, rules.TurningDiameter, U.M(0.05));
                var circle = Polygon2.Circle(t.Found ? t.Center : region.Outer.Centroid, rules.TurningDiameter / 2, 32);
                if (t.Found)
                {
                    circles.Add((circle, true));
                    ctx.Report.Success(Loc.F("{0}: círculo livre de {1} encontrado.", "{0}: free {1} circle found.", label, U.FormatCm(rules.TurningDiameter)), room.Id.Value);
                }
                else
                {
                    issues.Add(new ErgonomicIssue
                    {
                        Kind = Loc.T("Manobra", "Turning"),
                        Message = Loc.F("{0}: não há círculo livre de {1} (referência).", "{0}: no free {1} circle (reference).", label, U.FormatCm(rules.TurningDiameter)),
                        ElementIds = { room.Id.Value },
                        Location = region.Outer.Centroid,
                    });
                }
            }

            if (rules.CheckApproach)
            {
                var targets = items.Where(i => approachCats.Contains(i.Category)).ToList();
                if (targets.Count > 0) issues.AddRange(ErgonomicChecker.CheckApproach(label, region, items, targets, rules));
            }

            if (rules.CheckDoorSwings)
            {
                var plan = InteriorsUtil.PlanFor(doc, view, room.Level);
                var swings = new List<(long, string, Polygon2)>();
                foreach (var d in doors.Where(d => BorderCommand.SafeRoom(d, true)?.Id == room.Id || BorderCommand.SafeRoom(d, false)?.Id == room.Id))
                {
                    foreach (var s in InteriorsUtil.DoorSwings(d, plan, room, out var est))
                    {
                        if (region.IntersectionArea(s) < Math.Abs(s.Area) * 0.3) continue;
                        swings.Add((d.Id.Value, InteriorsUtil.Name(d), s));
                        if (est) estimatedDoors++;
                    }
                }
                issues.AddRange(ErgonomicChecker.CheckDoorSwings(swings, items));
            }

            if (rules.CheckHeights)
            {
                var floorZ = (room.Level?.Elevation ?? 0) + (room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET)?.AsDouble() ?? 0);
                var counters = items.Where(i => rules.CounterKeywords.Any(k => TextUtil.NormalizeForCompare(i.Name).Contains(k))).ToList();
                issues.AddRange(ErgonomicChecker.CheckHeights(counters, floorZ, rules));
            }

            foreach (var i in issues) allIssues.Add((room, i));
        }

        // Relatório
        ctx.Report.Info(Loc.F("Regras: {0}. {1}", "Rules: {0}. {1}", rules.Name, rules.ReferenceNote));
        foreach (var (_, i) in allIssues) ctx.Report.Add(i.Severity, i.Message, i.ElementIds, i.Kind);
        if (estimatedDoors > 0)
            ctx.Report.Info(Loc.F("{0} abertura(s) de porta estimada(s) (família sem arco em planta).", "{0} door swing(s) estimated (family without plan arc).", estimatedDoors));
        var t2 = new TableData(Loc.T("Pontos de atenção", "Attention points"));
        t2.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Verificação", "Check")).AddColumn(Loc.T("Descrição", "Description"));
        foreach (var (room, i) in allIssues) t2.AddRowWithIds(i.ElementIds, RoomUtil.Label(room), i.Kind, i.Message);
        ctx.Report.Tables.Add(t2);
        ctx.Report.Info(Loc.T("Este relatório aponta itens para revisão e NÃO declara conformidade com normas. A validação é do profissional responsável.",
            "This report flags items for review and does NOT declare compliance with standards. Validation is the responsible professional's."));
        if (allIssues.Count == 0) ctx.Report.Success(Loc.F("{0} ambiente(s) sem pontos de atenção pelas regras usadas.", "{0} room(s) with no attention points under the rules used.", rooms.Count));

        if (drawIn != null)
        {
            var key = "ergo.check|" + drawIn.Id.Value;
            ctx.Modify(ctx.Tool.Name, () =>
            {
                InteriorsUtil.ClearMarkers(doc, key);
                var typeId = InteriorsUtil.FilledRegionType(doc, o.RegionType);
                var created = new List<ElementId>();
                foreach (var (_, i) in allIssues)
                {
                    var shape = i.Zone ?? (i.Location is { } l ? Polygon2.Circle(l, U.Mm(150), 16) : null);
                    if (shape == null) continue;
                    var fr = InteriorsUtil.Region(doc, drawIn, typeId, shape);
                    if (fr != null) created.Add(fr.Id);
                }
                foreach (var (c, ok) in circles)
                {
                    var fr = InteriorsUtil.Region(doc, drawIn, typeId, c);
                    if (fr != null) created.Add(fr.Id);
                }
                InteriorsUtil.SaveMarkers(doc, key, created);
                ctx.Report.Count(Loc.T("Marcações", "Markers"), created.Count);
            });
        }
        ctx.Report.ForceShow = true;
    }
}
