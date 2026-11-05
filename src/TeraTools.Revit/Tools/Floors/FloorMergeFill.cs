using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Floors;

/// <summary>
/// Operações de contorno em planta usando prismas simples do Revit (união/subtração exatas,
/// preservando arcos). Os prismas têm 1 pé de altura a partir de Z = 0.
/// </summary>
internal static class PlanBool
{
    private const double H = 1.0;

    public static List<CurveLoop> FloorLoops(Document doc, Floor f)
    {
        var loops = new List<CurveLoop>();
        if (doc.GetElement(f.SketchId) is not Sketch sk) return loops;
        foreach (CurveArray arr in sk.Profile)
        {
            var curves = arr.Cast<Curve>().ToList();
            foreach (var chain in Shapes.ChainLoops(curves))
                if (Shapes.ToCurveLoop(chain) is { } cl) loops.Add(cl);
        }
        return loops;
    }

    public static List<CurveLoop> RoomLoops(Room room)
    {
        var loops = new List<CurveLoop>();
        var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        foreach (var seg in room.GetBoundarySegments(opts))
        {
            var curves = seg.Select(s => s.GetCurve()).Where(c => c.Length > 1e-6).ToList();
            foreach (var chain in Shapes.ChainLoops(curves))
                if (Shapes.ToCurveLoop(chain) is { } cl) loops.Add(cl);
        }
        return loops;
    }

    /// <summary>Último motivo de falha informado pelo Revit (para explicar ao usuário).</summary>
    public static string? LastError { get; private set; }

    /// <summary>Prisma vertical a partir de laços (achatados em Z = 0).</summary>
    public static Solid? Prism(IList<CurveLoop> loops)
    {
        if (loops.Count == 0)
        {
            LastError = Loc.T("contorno vazio ou não fechado", "empty or open outline");
            return null;
        }
        try
        {
            var flat = new List<CurveLoop>();
            foreach (var l in loops)
            {
                var z = l.First().GetEndPoint(0).Z;
                flat.Add(CurveLoop.CreateViaTransform(l, Transform.CreateTranslation(new XYZ(0, 0, -z))));
            }
            // garante orientação: maior laço anti-horário e furos horários
            var ordered = flat.OrderByDescending(Area).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var ccw = ordered[i].IsCounterclockwise(XYZ.BasisZ);
                var outer = i == 0 || !IsInside(ordered[i], ordered[0]);
                if (outer != ccw) ordered[i].Flip();
            }
            return GeometryCreationUtilities.CreateExtrusionGeometry(ordered, XYZ.BasisZ, H);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Warn("Prisma em planta: " + ex.Message);
            return null;
        }
    }

    private static double Area(CurveLoop l)
    {
        var pts = l.SelectMany(c => c.Tessellate()).ToList();
        double a = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return Math.Abs(a / 2);
    }

    private static bool IsInside(CurveLoop inner, CurveLoop outer)
    {
        var p = inner.First().GetEndPoint(0);
        var poly = new Polygon2(outer.SelectMany(c => c.Tessellate()).Select(x => new Vec2(x.X, x.Y)));
        return poly.Contains(new Vec2(p.X, p.Y));
    }

    public static Solid? Combine(Solid? a, Solid? b, BooleanOperationsType op)
    {
        if (a == null) return op == BooleanOperationsType.Union ? b : null;
        if (b == null) return a;
        try
        {
            return BooleanOperationsUtils.ExecuteBooleanOperation(a, b, op);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Warn("Booleana em planta: " + ex.Message);
            return null;
        }
    }

    /// <summary>Laços das faces inferiores (cada face = uma região separada), na cota pedida.</summary>
    public static List<List<CurveLoop>> Regions(Solid? s, double z)
    {
        var res = new List<List<CurveLoop>>();
        if (s == null || s.Volume < 1e-9) return res;
        foreach (Face f in s.Faces)
        {
            if (f is not PlanarFace pf || pf.FaceNormal.Z > -0.99) continue;
            var loops = pf.GetEdgesAsCurveLoops().Select(l => CurveLoop.CreateViaTransform(l, Transform.CreateTranslation(new XYZ(0, 0, z - pf.Origin.Z)))).ToList();
            if (loops.Count > 0) res.Add(loops);
        }
        return res;
    }

    public static double AreaOf(Solid? s) => s == null ? 0 : s.Volume / H;
}

public sealed class FloorMergeOptions
{
    [Opt("Tipo do piso resultante (vazio = do primeiro piso)", "Resulting floor type (empty = first floor's)", Kind = OptKind.FloorType)]
    public string FloorType { get; set; } = "";

    [Opt("Manter os pisos originais", "Keep the original floors")]
    public bool KeepOriginals { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.merge", Modules.Floors, "Unir pisos (Floor Merger)", "Merge floors (Floor Merger)", Order = 25,
    DescPt = "Une vários pisos do mesmo nível em um único piso com o contorno exato da união (arcos preservados), mantendo tipo e altura.",
    DescEn = "Merges several floors on the same level into one floor with the exact union outline (arcs kept), keeping type and height.",
    HelpPt = "Selecione os pisos (do mesmo nível) e confirme. O contorno resultante é a união exata dos contornos. Pisos que não se tocam viram partes do mesmo piso. Pisos inclinados não são suportados.",
    HelpEn = "Select the floors (same level) and confirm. The outline is the exact union. Sloped floors are not supported.",
    Keywords = "unir pisos juntar fundir merge floors join combine")]
public sealed class FloorMergeCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var floors = ctx.SelectedOrPick("Selecione os pisos a unir", "Select the floors to merge", e => e is Floor).OfType<Floor>().ToList();
        if (floors.Count < 2) ToolContext.Fail("Selecione pelo menos dois pisos.", "Select at least two floors.");
        var levels = floors.Select(f => f.LevelId.Value).Distinct().ToList();
        if (levels.Count > 1) ToolContext.Fail("Os pisos precisam estar no mesmo nível.", "The floors must be on the same level.");
        var o = ctx.Ask<FloorMergeOptions>(okText: Loc.T("Unir", "Merge"));
        var first = floors[0];
        var level = (Level)doc.GetElement(first.LevelId);
        var offset = first.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;
        var typeId = string.IsNullOrWhiteSpace(o.FloorType) ? first.GetTypeId() : FloorUtil.FloorType(doc, o.FloorType);
        Solid? union = null;
        foreach (var f in floors)
        {
            var p = PlanBool.Prism(PlanBool.FloorLoops(doc, f));
            if (p == null)
            {
                ctx.ItemFailed("Pisos ignorados (contorno não lido; inclinado ou complexo)", "Skipped floors (outline not read)", PlanBool.LastError ?? "", f.Id.Value);
                continue;
            }
            union = PlanBool.Combine(union, p, BooleanOperationsType.Union);
            if (union == null)
                ToolContext.Fail($"O Revit não conseguiu unir os contornos destes pisos. Motivo: {PlanBool.LastError}",
                    $"Revit could not union these floor outlines. Reason: {PlanBool.LastError}");
        }
        var regions = PlanBool.Regions(union, level.Elevation);
        if (regions.Count == 0)
            ToolContext.Fail($"Não foi possível obter o contorno unido. {(ctx.Report.FirstFailure ?? PlanBool.LastError)}",
                $"Could not get the merged outline. {(ctx.Report.FirstFailure ?? PlanBool.LastError)}");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var loops = regions.SelectMany(r => r).ToList();
            var created = new List<Floor>();
            try
            {
                created.Add(Floor.Create(doc, loops, typeId, level.Id));
            }
            catch (Exception ex)
            {
                // Regiões separadas viram pisos separados.
                Log.Warn("Piso unido em uma peça: " + ex.Message);
                foreach (var r in regions)
                {
                    try
                    {
                        created.Add(Floor.Create(doc, r, typeId, level.Id));
                    }
                    catch (Exception ex2)
                    {
                        ctx.ItemFailed("Regiões do piso unido", "Merged floor regions", ex2);
                    }
                }
            }
            ctx.RequireCreated(created.Count, "piso unido", "merged floor");
            foreach (var nf in created)
            {
                if (Math.Abs(offset) > 1e-9) RevitGeo.Set(nf, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, offset);
                ctx.Report.Created();
                ctx.Report.AddResult(nf.Id.Value);
            }
            if (!o.KeepOriginals)
            {
                doc.Delete(floors.Select(f => f.Id).ToList());
                ctx.Report.Deleted(floors.Count);
            }
        });
        ctx.Report.Info(Loc.F("Área do piso unido: {0}", "Merged floor area: {0}", U.FormatArea(PlanBool.AreaOf(union))));
    }
}

public enum FillerMode
{
    [Text("Preencher as áreas sem piso", "Fill the areas without floor")] Fill,
    [Text("Só listar (conferência)", "List only (review)")] Report,
}

public sealed class FloorFillerOptions
{
    [Opt("Modo", "Mode")]
    public FillerMode Mode { get; set; } = FillerMode.Fill;

    [Opt("Tipo de piso", "Floor type", Kind = OptKind.FloorType, VisibleIf = "Mode=Fill")]
    public string FloorType { get; set; } = "";

    [Opt("Deslocamento do nível", "Height offset", Kind = OptKind.Length, VisibleIf = "Mode=Fill")]
    public double Offset { get; set; }

    [Opt("Ignorar sobras menores que", "Ignore leftovers smaller than", Suffix = "m²", Min = 0)]
    public double MinAreaM2 { get; set; } = 0.05;

    [Opt("Espessura máxima de piso de acabamento", "Maximum finish floor thickness", Kind = OptKind.Length, Min = 0,
        Tip = "Só pisos de acabamento (do tipo escolhido, ou finos e não estruturais até esta espessura) contam como piso existente. A laje estrutural não conta.|Only finish floors (of the chosen type, or thin non-structural ones up to this thickness) count as existing floor. The structural slab does not.")]
    public double FinishMaxThickness { get; set; } = U.Mm(60);
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.filler", Modules.Floors, "Preencher pisos (Floor Filler)", "Fill floors (Floor Filler)", Order = 26,
    DescPt = "Encontra o que falta de piso em cada ambiente (contorno do ambiente menos os pisos existentes) e cria pisos exatamente nessas sobras — ou só lista para conferência.",
    DescEn = "Finds the missing floor in each room (room outline minus existing floors) and creates floors exactly in those gaps — or just lists them.",
    Keywords = "preencher piso faltando sobra lacuna completar fill floor missing gaps uncovered")]
public sealed class FloorFillerCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var rooms = ctx.Selected(e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        if (rooms.Count == 0) rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).ToList();
        var o = ctx.Ask<FloorFillerOptions>(okText: Loc.T("Executar", "Run"));
        var typeId = FloorUtil.FloorType(doc, o.FloorType);
        var minArea = U.M2ToSqFt(o.MinAreaM2);
        var t = new TableData(Loc.T("Pisos faltando por ambiente", "Missing floor by room"));
        t.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Área do ambiente (m²)", "Room area (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Sem piso (m²)", "Without floor (m²)"), ColumnKind.Number).AddColumn(Loc.T("Coberto (%)", "Covered (%)"), ColumnKind.Number, 0);
        var plan = new List<(Room Room, List<List<CurveLoop>> Gaps)>();
        foreach (var room in rooms)
        {
            var level = room.Level;
            if (level == null)
            {
                ctx.ItemFailed("Ambientes sem nível", "Rooms without level", Loc.T("ambiente sem nível", "room without level"), room.Id.Value);
                continue;
            }
            var roomSolid = PlanBool.Prism(PlanBool.RoomLoops(room));
            if (roomSolid == null)
            {
                ctx.ItemFailed("Ambientes não analisados (contorno não lido)", "Rooms not analysed (outline not read)", PlanBool.LastError ?? "", room.Id.Value);
                continue;
            }
            var reg = RevitGeo.RoomRegion(room);
            Solid? gap = roomSolid;
            if (reg != null)
                foreach (var f in FloorUtil.FloorsInRoom(doc, room, reg, x => FloorUtil.IsFinishFloor(x, string.IsNullOrWhiteSpace(o.FloorType) ? null : typeId, o.FinishMaxThickness)).Floors)
                {
                    var fp = PlanBool.Prism(PlanBool.FloorLoops(doc, f));
                    if (fp == null)
                    {
                        ctx.ItemFailed("Pisos não considerados (contorno não lido)", "Floors not considered (outline not read)", PlanBool.LastError ?? "", f.Id.Value);
                        continue;
                    }
                    var diff = PlanBool.Combine(gap, fp, BooleanOperationsType.Difference);
                    if (diff == null)
                        ctx.ItemFailed("Pisos não descontados (booleana recusada)", "Floors not subtracted (boolean refused)", PlanBool.LastError ?? "", f.Id.Value);
                    else gap = diff;
                }
            var missing = PlanBool.AreaOf(gap);
            var roomArea = PlanBool.AreaOf(roomSolid);
            if (missing < minArea) continue;
            t.AddRowWithIds(new[] { room.Id.Value }, FloorUtil.RoomLabel(room), U.SqFtToM2(roomArea), U.SqFtToM2(missing), roomArea > 0 ? (1 - missing / roomArea) * 100 : 0);
            plan.Add((room, PlanBool.Regions(gap, level.Elevation)));
        }
        ctx.Report.Tables.Add(t);
        if (plan.Count == 0)
        {
            if (ctx.Report.FailedItems > 0)
                ctx.Report.Warn(Loc.T("Nenhuma sobra encontrada nos ambientes analisados, mas alguns ambientes não puderam ser analisados (veja acima).",
                    "No gap found in the analysed rooms, but some rooms could not be analysed (see above)."));
            else
                ctx.Report.Success(Loc.T("Todos os ambientes estão totalmente cobertos por piso.", "All rooms are fully covered by floor."));
            ctx.Report.ForceShow = true;
            return;
        }
        if (o.Mode == FillerMode.Report)
        {
            ctx.Report.ForceShow = true;
            return;
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var (room, gaps) in plan)
            {
                foreach (var g in gaps)
                {
                    try
                    {
                        var nf = Floor.Create(doc, g, typeId, room.LevelId);
                        if (Math.Abs(o.Offset) > 1e-9) RevitGeo.Set(nf, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, o.Offset);
                        RevitGeo.Set(nf, FloorUtil.RoomLabel(room), SharedParams.Ambiente);
                        ctx.Report.Created();
                        ctx.Report.AddResult(nf.Id.Value);
                    }
                    catch (Exception ex)
                    {
                        ctx.ItemFailed("Pisos de preenchimento", "Fill floors", ex.Message, room.Id.Value);
                    }
                }
            }
            ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Criados", "Created")), "piso de preenchimento", "fill floor");
        });
    }
}
