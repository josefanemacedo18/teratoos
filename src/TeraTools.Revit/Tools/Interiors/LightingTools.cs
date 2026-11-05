using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using TeraTools.Core.Colors;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Layout;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

/// <summary>Localiza forros e suas faces inferiores (para hospedar luminárias e abrir furos).</summary>
internal sealed class CeilingFinder
{
    private readonly List<(Ceiling Ceiling, List<(Reference Ref, Face Face)> Faces, BoundingBoxXYZ Box)> _items = new();

    public CeilingFinder(Document doc)
    {
        foreach (var c in new FilteredElementCollector(doc).OfClass(typeof(Ceiling)).Cast<Ceiling>())
        {
            var bb = c.get_BoundingBox(null);
            if (bb == null) continue;
            var faces = new List<(Reference, Face)>();
            try
            {
                foreach (var r in HostObjectUtils.GetBottomFaces(c))
                    if (c.GetGeometryObjectFromReference(r) is Face f) faces.Add((r, f));
            }
            catch (Exception ex)
            {
                Log.Warn("Faces do forro: " + ex.Message);
            }
            _items.Add((c, faces, bb));
        }
    }

    public int Count => _items.Count;

    /// <summary>Forro mais baixo sobre o ponto, entre as cotas indicadas, e a face inferior que o contém.</summary>
    public (Ceiling Ceiling, Reference? FaceRef, double Z)? Above(XYZ p, double zMin, double zMax)
    {
        (Ceiling, Reference?, double)? best = null;
        foreach (var (c, faces, bb) in _items)
        {
            if (p.X < bb.Min.X || p.X > bb.Max.X || p.Y < bb.Min.Y || p.Y > bb.Max.Y) continue;
            if (bb.Min.Z > zMax || bb.Max.Z < zMin) continue;
            foreach (var (r, f) in faces)
            {
                IntersectionResult? ir;
                try
                {
                    ir = f.Project(new XYZ(p.X, p.Y, bb.Min.Z - 1));
                }
                catch
                {
                    continue;
                }
                if (ir == null) continue;
                var q = ir.XYZPoint;
                if (Math.Abs(q.X - p.X) > 0.01 || Math.Abs(q.Y - p.Y) > 0.01) continue;
                if (q.Z < zMin || q.Z > zMax) continue;
                if (best == null || q.Z < best.Value.Item3) best = (c, r, q.Z);
            }
        }
        return best;
    }
}

/// <summary>Inserção de luminárias: na face do forro, hospedada no forro ou livre na altura indicada.</summary>
internal static class Fixtures
{
    public static FamilyInstance? Place(Document doc, FamilySymbol sym, Level level, Vec2 p, double angle, bool onCeiling, double height,
        CeilingFinder? ceilings, double roomHeight)
    {
        if (!sym.IsActive) sym.Activate();
        FamilyInstance? fi = null;
        if (onCeiling && ceilings != null && ceilings.Count > 0)
        {
            var hit = ceilings.Above(new XYZ(p.X, p.Y, level.Elevation), level.Elevation + 0.1, level.Elevation + Math.Max(roomHeight, 1) + 1.0);
            if (hit != null)
            {
                var (c, faceRef, z) = hit.Value;
                var loc = new XYZ(p.X, p.Y, z);
                var dir = new XYZ(Math.Cos(angle), Math.Sin(angle), 0);
                if (faceRef != null && Tx.TrySub(doc, () => fi = doc.Create.NewFamilyInstance(faceRef, loc, dir, sym))) return fi;
                if (Tx.TrySub(doc, () => fi = doc.Create.NewFamilyInstance(loc, sym, c, level, StructuralType.NonStructural)))
                {
                    Rotate(doc, fi!, angle);
                    return fi;
                }
                height = z - level.Elevation;
            }
        }
        if (InteriorsUtil.IsFreePlacement(sym) && Tx.TrySub(doc, () => fi = InteriorsUtil.PlaceFree(doc, sym, level, p.ToXYZ(), height)))
        {
            Rotate(doc, fi!, angle);
            return fi;
        }
        return null;
    }

    private static void Rotate(Document doc, FamilyInstance fi, double angle)
    {
        if (Math.Abs(angle) < 1e-9 || fi.Location is not LocationPoint lp) return;
        ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(lp.Point, lp.Point + XYZ.BasisZ), angle);
    }
}

public enum LightMode
{
    [Text("Por espaçamento", "By spacing")] Spacing,
    [Text("Por iluminância (método dos lúmens)", "By illuminance (lumen method)")] Lumen,
}

public enum LightMount
{
    [Text("No forro (se houver); senão na altura indicada", "On the ceiling (if any); else at the given height")] Ceiling,
    [Text("Na altura indicada", "At the given height")] Height,
}

public sealed class LightGridOptions
{
    [Opt("Luminária", "Fixture", Kind = OptKind.FamilyType, Filter = "OST_LightingFixtures", Required = true, Group = "Luminária|Fixture")]
    public string Type { get; set; } = "";

    [Opt("Fixação", "Mounting", Group = "Luminária|Fixture")]
    public LightMount Mount { get; set; } = LightMount.Ceiling;

    [Opt("Altura (a partir do piso)", "Height (from floor)", Kind = OptKind.Length, Min = 0, Group = "Luminária|Fixture")]
    public double Height { get; set; } = U.M(2.6);

    [Opt("Modo", "Mode", Group = "Distribuição|Layout")]
    public LightMode Mode { get; set; } = LightMode.Spacing;

    [Opt("Espaçamento X", "Spacing X", Kind = OptKind.Length, Min = 0.05, Group = "Distribuição|Layout", VisibleIf = "Mode=Spacing")]
    public double SpacingX { get; set; } = U.M(1.2);

    [Opt("Espaçamento Y", "Spacing Y", Kind = OptKind.Length, Min = 0.05, Group = "Distribuição|Layout", VisibleIf = "Mode=Spacing")]
    public double SpacingY { get; set; } = U.M(1.2);

    [Opt("Iluminância desejada", "Target illuminance", Suffix = "lux", Min = 1, Group = "Distribuição|Layout", VisibleIf = "Mode=Lumen")]
    public double Lux { get; set; } = 300;

    [Opt("Fluxo por luminária", "Flux per fixture", Suffix = "lm", Min = 1, Group = "Distribuição|Layout", VisibleIf = "Mode=Lumen")]
    public double Lumens { get; set; } = 1800;

    [Opt("Fator de utilização", "Utilization factor", Min = 0.05, Max = 1, Group = "Distribuição|Layout", VisibleIf = "Mode=Lumen")]
    public double Utilization { get; set; } = 0.6;

    [Opt("Fator de manutenção", "Maintenance factor", Min = 0.05, Max = 1, Group = "Distribuição|Layout", VisibleIf = "Mode=Lumen")]
    public double Maintenance { get; set; } = 0.8;

    [Opt("Distribuir uniformemente (margem = meio espaçamento)", "Distribute evenly (margin = half spacing)", Group = "Distribuição|Layout")]
    public bool FitEvenly { get; set; } = true;

    [Opt("Afastamento das paredes", "Distance from walls", Kind = OptKind.Length, Min = 0, Group = "Distribuição|Layout", VisibleIf = "!FitEvenly")]
    public double Margin { get; set; } = U.M(0.6);

    [Opt("Distância mínima da parede", "Minimum wall distance", Kind = OptKind.Length, Min = 0, Group = "Distribuição|Layout")]
    public double MinWall { get; set; } = U.M(0.3);

    [Opt("Alinhar com a direção do ambiente", "Align with the room direction", Group = "Distribuição|Layout")]
    public bool AutoOrient { get; set; } = true;

    [Opt("Rotação da grade", "Grid rotation", Kind = OptKind.Angle, Group = "Distribuição|Layout")]
    public double Rotation { get; set; }

    [Opt("Grupo/circuito (TT_Grupo)", "Group/circuit (TT_Grupo)", Group = "Dados|Data", Tip = "Marcadores: {Numero} {Nome}.|Tokens: {Numero} {Nome}.")]
    public string Group { get; set; } = "{Numero}";
}

[Transaction(TransactionMode.Manual)]
[Tool("light.grid", Modules.Interiors, "Luminárias em grade", "Fixtures in a grid", Order = 40, Large = true, Group = "Iluminação|Lighting",
    DescPt = "Distribui luminárias nos ambientes por espaçamento ou pelo método dos lúmens, alinhadas ao ambiente e hospedadas no forro quando possível.",
    DescEn = "Distributes fixtures in rooms by spacing or by the lumen method, aligned to the room and hosted on the ceiling when possible.",
    HelpPt = "Método dos lúmens: N = (E × A) / (Φ × FU × FM). É uma estimativa de pré-dimensionamento; o cálculo luminotécnico final deve ser feito em software próprio.\n\nFixação no forro: famílias baseadas em face são colocadas na face inferior do forro; famílias hospedadas em forro usam o forro como hospedeiro; as demais ficam na altura do forro encontrado (ou na altura indicada).",
    HelpEn = "Lumen method: N = (E × A) / (Φ × UF × MF). It is a preliminary estimate; final lighting design should be done in dedicated software.\n\nCeiling mounting: face-based families go on the ceiling bottom face; ceiling-hosted families use the ceiling as host; others are placed at the found ceiling height (or the given height).",
    Keywords = "luminarias grade iluminacao lumens lux distribuir lighting fixtures grid")]
public sealed class LightGridCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var regions = rooms.Select(r => (Room: r, Region: RevitGeo.RoomRegion(r))).Where(x => x.Region != null).Select(x => (x.Room, Region: x.Region!)).ToList();
        if (regions.Count == 0) ToolContext.Fail("Os ambientes não têm contorno.", "The rooms have no boundary.");
        var o = ctx.Ask<LightGridOptions>(preview: x => Preview(x, regions), okText: Loc.T("Inserir", "Place"));
        var sym = Lookup.Symbol(doc, o.Type, "OST_LightingFixtures") ?? throw new ToolException(Loc.T("Escolha a luminária.", "Choose the fixture."));
        var plans = regions.Select(x =>
        {
            var pts = Compute(o, x.Region, out var angle, out var lux);
            return (x.Room, Points: pts, Angle: angle, Lux: lux);
        }).ToList();
        ctx.CheckCount(plans.Sum(p => p.Points.Count), "luminárias", "fixtures");
        var ceilings = o.Mount == LightMount.Ceiling ? new CeilingFinder(doc) : null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            SharedParams.BindToProject(doc, new[] { SharedParams.Grupo, SharedParams.Ambiente }, new[] { BuiltInCategory.OST_LightingFixtures });
            foreach (var plan in plans)
            {
                var level = plan.Room.Level;
                if (level == null) continue;
                var group = NameTemplate.Apply(o.Group, new Dictionary<string, object?> { ["Numero"] = plan.Room.Number, ["Nome"] = plan.Room.Name });
                int placed = 0, failed = 0;
                foreach (var p in plan.Points)
                {
                    var fi = Fixtures.Place(doc, sym, level, p, plan.Angle, o.Mount == LightMount.Ceiling, o.Height, ceilings, plan.Room.UnboundedHeight);
                    if (fi == null)
                    {
                        failed++;
                        continue;
                    }
                    RevitGeo.Set(fi, group, SharedParams.Grupo);
                    RevitGeo.Set(fi, RoomUtil.Label(plan.Room), SharedParams.Ambiente);
                    placed++;
                    ctx.Report.Created();
                    ctx.Report.AddResult(fi.Id.Value);
                }
                var extra = o.Mode == LightMode.Lumen ? Loc.F(" · ≈{0} lux (estimativa)", " · ≈{0} lux (estimate)", Math.Round(plan.Lux)) : "";
                ctx.Report.Info(Loc.F("{0}: {1} luminárias{2}", "{0}: {1} fixtures{2}", RoomUtil.Label(plan.Room), placed, extra), plan.Room.Id.Value);
                if (failed > 0)
                    ctx.Report.Warn(Loc.F("{0}: {1} luminárias não puderam ser inseridas (família hospedada sem forro?).", "{0}: {1} fixtures could not be placed (hosted family without ceiling?).",
                        RoomUtil.Label(plan.Room), failed), plan.Room.Id.Value);
            }
        });
    }

    internal static List<Vec2> Compute(LightGridOptions o, Region2 region, out double angle, out double lux)
    {
        var s = new GridLayoutSettings
        {
            SpacingX = o.SpacingX, SpacingY = o.SpacingY, MarginX = o.Margin, MarginY = o.Margin, FitEvenly = o.FitEvenly,
            AutoOrient = o.AutoOrient, RotationDeg = o.Rotation, MinWallDistance = o.MinWall,
        };
        lux = 0;
        var areaM2 = U.SqFtToM2(region.Area);
        if (o.Mode == LightMode.Lumen)
        {
            var n = LumenMethod.Count(o.Lux, areaM2, o.Lumens, o.Utilization, o.Maintenance);
            var a = o.AutoOrient ? GridLayout.DominantDirection(region.Outer) + MathUtil.ToRadians(o.Rotation) : MathUtil.ToRadians(o.Rotation);
            var c = region.Outer.Centroid;
            var b = Rect2.FromPoints(region.Outer.Select(p => (p - c).Rotate(-a)).ToList());
            var lx = Math.Max(b.Width, 1e-6);
            var ly = Math.Max(b.Height, 1e-6);
            var nx = Math.Max(1, (int)Math.Round(Math.Sqrt(n * lx / ly)));
            var ny = Math.Max(1, (int)Math.Ceiling((double)n / nx));
            s.SpacingX = lx / nx;
            s.SpacingY = ly / ny;
            s.FitEvenly = true;
        }
        var pts = GridLayout.Generate(region, s, out angle);
        if (o.Mode == LightMode.Lumen) lux = LumenMethod.Illuminance(pts.Count, areaM2, o.Lumens, o.Utilization, o.Maintenance);
        return pts;
    }

    private static PreviewModel Preview(LightGridOptions o, List<(Room Room, Region2 Region)> regions)
    {
        var pm = new PreviewModel();
        var total = 0;
        var luxes = new List<double>();
        foreach (var (_, reg) in regions)
        {
            pm.Region(reg);
            var pts = Compute(o, reg, out _, out var lux);
            total += pts.Count;
            if (o.Mode == LightMode.Lumen) luxes.Add(lux);
            foreach (var p in pts) pm.Dot(p, U.Mm(120), "#E0A526");
            var sc = pm.S3;
            sc.Face(Frame3.World, reg.Outer, "#D9D4CC", 0.45);
            sc.Face(new Frame3(new Vec3(0, 0, o.Height), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), reg.Outer, "#FFFFFF", 0.12);
            foreach (var p in pts)
            {
                sc.Box(OrientedBox.FromCenter(new Vec3(p.X, p.Y, o.Height - U.Mm(30)), Vec3.UnitX, Vec3.UnitY, U.Mm(180), U.Mm(180), U.Mm(60)), "#E0A526");
                sc.Line(new Vec3(p.X, p.Y, o.Height - U.Mm(60)), new Vec3(p.X, p.Y, 0), "#F2D27A");
            }
        }
        pm.Info = o.Mode == LightMode.Lumen && luxes.Count > 0
            ? Loc.F("{0} luminárias · ≈{1}–{2} lux (estimativa)", "{0} fixtures · ≈{1}–{2} lux (estimate)", total, Math.Round(luxes.Min()), Math.Round(luxes.Max()))
            : Loc.F("{0} luminárias", "{0} fixtures", total);
        return pm;
    }
}

public sealed class LightOverOptions
{
    [Opt("Luminária", "Fixture", Kind = OptKind.FamilyType, Filter = "OST_LightingFixtures", Required = true)]
    public string Type { get; set; } = "";

    [Opt("Quantidade por móvel", "Count per item", Min = 1, Max = 20)]
    public int Count { get; set; } = 1;

    [Opt("Espaçamento entre luminárias", "Spacing between fixtures", Kind = OptKind.Length, Min = 0, VisibleIf = "Count!=1")]
    public double Spacing { get; set; } = U.Mm(600);

    [Opt("Fixação", "Mounting")]
    public LightMount Mount { get; set; } = LightMount.Ceiling;

    [Opt("Altura (a partir do piso)", "Height (from floor)", Kind = OptKind.Length, Min = 0)]
    public double Height { get; set; } = U.M(2.6);
}

[Transaction(TransactionMode.Manual)]
[Tool("light.overfurniture", Modules.Interiors, "Luminárias sobre mobiliário", "Fixtures over furniture", Order = 41, Group = "Iluminação|Lighting",
    DescPt = "Centraliza pendentes/luminárias sobre mesas, bancadas, camas e ilhas — uma ou várias ao longo do comprimento do móvel.",
    DescEn = "Centers pendants/fixtures over tables, counters, beds and islands — one or several along the item's length.",
    Keywords = "pendente luminaria sobre mesa bancada ilha centralizar pendant over table island")]
public sealed class LightOverCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var items = ctx.SelectedOrPick("Selecione os móveis", "Select the items", InteriorsUtil.IsFurniture).ToList();
        var o = ctx.Ask<LightOverOptions>();
        var sym = Lookup.Symbol(doc, o.Type, "OST_LightingFixtures") ?? throw new ToolException(Loc.T("Escolha a luminária.", "Choose the fixture."));
        var ceilings = o.Mount == LightMount.Ceiling ? new CeilingFinder(doc) : null;
        var rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).ToList();
        var viewLevel = RevitGeo.ViewLevel(ctx.ActiveView);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            SharedParams.BindToProject(doc, new[] { SharedParams.Grupo, SharedParams.Ambiente }, new[] { BuiltInCategory.OST_LightingFixtures });
            foreach (var it in items)
            {
                var fp = InteriorsUtil.FootprintOf(it);
                if (fp.Outline.Count < 3) continue;
                var level = RevitGeo.LevelOf(it) ?? viewLevel;
                if (level == null) continue;
                var c = fp.Outline.Centroid;
                var f = fp.Facing;
                var (a0, a1) = InteriorsUtil.Extent(fp.Outline, f.Perp);
                var (b0, b1) = InteriorsUtil.Extent(fp.Outline, f);
                var dir = a1 - a0 >= b1 - b0 ? f.Perp : f;
                // centro da caixa orientada (mais estável que o centróide para formas irregulares)
                c = f.Perp * ((a0 + a1) / 2) + f * ((b0 + b1) / 2);
                var room = RevitGeo.RoomAt(doc, new XYZ(c.X, c.Y, level.Elevation), rooms);
                for (var k = 0; k < o.Count; k++)
                {
                    var p = c + dir * ((k - (o.Count - 1) / 2.0) * o.Spacing);
                    var fi = Fixtures.Place(doc, sym, level, p, dir.Angle, o.Mount == LightMount.Ceiling, o.Height, ceilings, room?.UnboundedHeight ?? U.M(3));
                    if (fi == null)
                    {
                        ctx.Report.Warn(Loc.F("Não foi possível inserir sobre \"{0}\".", "Could not place over \"{0}\".", fp.Name), it.Id.Value);
                        break;
                    }
                    if (room != null) RevitGeo.Set(fi, RoomUtil.Label(room), SharedParams.Ambiente);
                    ctx.Report.Created();
                    ctx.Report.AddResult(fi.Id.Value);
                }
            }
        });
    }
}

public enum LightGroupMode
{
    [Text("Um nome para todas as selecionadas", "One name for all selected")] Single,
    [Text("Automático por ambiente e tipo (101-A, 101-B...)", "Automatic by room and type (101-A, 101-B...)")] ByRoom,
    [Text("Apenas colorir os grupos existentes", "Only color existing groups")] ColorOnly,
}

public sealed class LightGroupOptions
{
    [Opt("Modo", "Mode")]
    public LightGroupMode Mode { get; set; } = LightGroupMode.Single;

    [Opt("Nome do grupo/circuito", "Group/circuit name", VisibleIf = "Mode=Single")]
    public string Name { get; set; } = "C1";

    [Opt("Colorir os grupos na vista ativa (filtros)", "Color groups in the active view (filters)")]
    public bool Colorize { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("light.groups", Modules.Interiors, "Grupos e circuitos de iluminação", "Lighting groups and circuits", Order = 42, Group = "Iluminação|Lighting",
    DescPt = "Atribui grupos/circuitos às luminárias (TT_Grupo), manual ou automaticamente por ambiente, e os colore na vista com filtros.",
    DescEn = "Assigns groups/circuits to fixtures (TT_Grupo), manually or automatically by room, and colors them in the view with filters.",
    Keywords = "grupo circuito comando iluminacao colorir filtro group circuit switching lighting color")]
public sealed class LightGroupCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<LightGroupOptions>();
        var fixtures = o.Mode == LightGroupMode.ColorOnly
            ? new FilteredElementCollector(doc, ctx.ActiveView.Id).OfCategory(BuiltInCategory.OST_LightingFixtures).WhereElementIsNotElementType().ToList()
            : ctx.SelectedOrPick("Selecione as luminárias", "Select the fixtures", e => e.Category?.Id.Value == (long)BuiltInCategory.OST_LightingFixtures);
        var rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            SharedParams.BindToProject(doc, new[] { SharedParams.Grupo }, new[] { BuiltInCategory.OST_LightingFixtures });
            if (o.Mode == LightGroupMode.Single)
            {
                foreach (var f in fixtures)
                    if (RevitGeo.Set(f, o.Name, SharedParams.Grupo)) ctx.Report.Modified();
            }
            else if (o.Mode == LightGroupMode.ByRoom)
            {
                var byRoom = fixtures.GroupBy(f =>
                {
                    var p = RevitGeo.Anchor(f) ?? XYZ.Zero;
                    var lz = RevitGeo.LevelOf(f)?.Elevation ?? p.Z;
                    return RevitGeo.RoomAt(doc, new XYZ(p.X, p.Y, lz), rooms)?.Number ?? "SA";
                });
                foreach (var g in byRoom)
                {
                    var types = g.Select(f => f.GetTypeId().Value).Distinct().OrderBy(v => v).ToList();
                    foreach (var f in g)
                    {
                        var letter = TextUtil.ToLetters(types.IndexOf(f.GetTypeId().Value));
                        if (RevitGeo.Set(f, $"{g.Key}-{letter}", SharedParams.Grupo)) ctx.Report.Modified();
                    }
                }
            }
            if (o.Colorize) Colorize(ctx);
        });
        // Resumo por grupo
        var t = new TableData(Loc.T("Grupos de iluminação", "Lighting groups"));
        t.AddColumn(Loc.T("Grupo", "Group")).AddColumn(Loc.T("Tipos", "Types")).AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer);
        var all = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_LightingFixtures).WhereElementIsNotElementType().ToList();
        foreach (var g in all.GroupBy(f => RevitGeo.Param(f, SharedParams.Grupo)?.AsString() ?? "").Where(g => g.Key.Length > 0).OrderBy(g => g.Key, TextUtil.NaturalComparer))
            t.AddRowWithIds(g.Select(f => f.Id.Value), g.Key, string.Join(", ", g.Select(InteriorsUtil.Name).Distinct()), g.Count());
        ctx.Report.Tables.Add(t);
    }

    private static void Colorize(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var view = ctx.ActiveView;
        var values = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_LightingFixtures).WhereElementIsNotElementType()
            .Select(f => RevitGeo.Param(f, SharedParams.Grupo)?.AsString() ?? "").Where(v => v.Length > 0).Distinct().OrderBy(v => v, TextUtil.NaturalComparer).ToList();
        if (values.Count == 0) return;
        var paramId = ViewFilters.SharedParamId(doc, SharedParams.Grupo);
        if (paramId == ElementId.InvalidElementId) return;
        var colors = PaletteGenerator.Distinct(values.Count, 0.7, 0.45);
        var cats = ViewFilters.CategoryIds(BuiltInCategory.OST_LightingFixtures);
        for (var i = 0; i < values.Count; i++)
        {
            try
            {
                var rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, values[i]);
                var filter = ViewFilters.Ensure(doc, "TT Grupo " + values[i], cats, new ElementParameterFilter(rule));
                ViewFilters.Apply(view, filter.Id, ViewFilters.Color(doc, colors[i], 0, cut: true, lines: true));
                ctx.Report.Count(Loc.T("Filtros", "Filters"));
            }
            catch (Exception ex)
            {
                ctx.Report.Warn(Loc.F("Filtro do grupo {0}: {1}", "Group {0} filter: {1}", values[i], ex.Message));
            }
        }
    }
}
