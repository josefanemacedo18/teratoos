using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Tiling;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Floors;

internal static class FloorUtil
{
    public static List<Room> PickRooms(ToolContext ctx) =>
        ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();

    public static double LevelZ(Room r) => r.Level?.Elevation ?? 0;

    public static string RoomLabel(Room r) => $"{r.Number} {r.Name}".Trim();

    /// <summary>Linhas do contorno de um polígono para o Floor/Ceiling.Create.</summary>
    public static List<CurveLoop> Loops(Region2 region, double z) => RevitGeo.RegionToLoops(region, z);

    public static Floor CreateFloor(Document doc, Region2 region, ElementId typeId, Level level, double offset)
    {
        var floor = Floor.Create(doc, Loops(region, level.Elevation), typeId, level.Id);
        if (Math.Abs(offset) > 1e-9) RevitGeo.Set(floor, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, offset);
        return floor;
    }

    public static ElementId FloorType(Document doc, string? name) =>
        Lookup.Named<FloorType>(doc, name)?.Id ?? doc.GetDefaultElementTypeId(ElementTypeGroup.FloorType);

    public static (List<Floor> Floors, double Area) FloorsInRoom(Document doc, Room room, Region2 region)
    {
        var floors = new List<Floor>();
        double area = 0;
        var rb = region.Bounds;
        foreach (var f in Lookup.Collect<Floor>(doc))
        {
            var fb = RevitGeo.Box(f);
            if (fb == null) continue;
            var lz = FloorUtil.LevelZ(room);
            if (fb.Max.Z < lz - 1 || fb.Min.Z > lz + 3) continue;
            if (fb.Max.X < rb.MinX || fb.Min.X > rb.MaxX || fb.Max.Y < rb.MinY || fb.Min.Y > rb.MaxY) continue;
            var fr = RevitGeo.HostRegion(f);
            if (fr == null) continue;
            var common = 0.0;
            if (fr.Outer.IsConvex) common = region.IntersectionArea(fr.Outer);
            else if (region.Outer.IsConvex) common = fr.IntersectionArea(region.Outer);
            else common = Math.Min(fr.Area, region.Area) * (region.Contains(fr.Outer.Centroid) ? 1 : 0);
            if (common > 0)
            {
                floors.Add(f);
                area += common;
            }
        }
        return (floors, area);
    }
}

public enum BoundaryAt
{
    [Text("Face de acabamento das paredes", "Wall finish face")] Finish,
    [Text("Eixo das paredes", "Wall centerline")] Center,
    [Text("Núcleo da parede (face)", "Wall core face")] CoreBoundary,
}

public sealed class FloorByRoomOptions
{
    [Opt("Tipo de piso", "Floor type", Kind = OptKind.FloorType, Required = true)]
    public string FloorType { get; set; } = "";

    [Opt("Contorno pela", "Outline at")]
    public BoundaryAt Boundary { get; set; } = BoundaryAt.Finish;

    [Opt("Deslocamento do nível", "Offset from level", Kind = OptKind.Length)]
    public double Offset { get; set; }

    [Opt("Ignorar ambientes que já têm piso", "Skip rooms that already have a floor")]
    public bool SkipExisting { get; set; } = true;

    [Opt("Preencher acabamento de piso do ambiente", "Fill the room floor finish parameter")]
    public bool FillFinish { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.byroom", Modules.Floors, "Piso por ambiente", "Floor by room", Order = 10, Large = true,
    DescPt = "Cria pisos pelo contorno dos ambientes (face de acabamento, eixo ou núcleo), com furos de pilares e shafts.",
    DescEn = "Creates floors from room outlines (finish face, centerline or core), with column and shaft holes.",
    Keywords = "piso ambiente contorno floor room outline")]
public sealed class FloorByRoomCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = FloorUtil.PickRooms(ctx);
        var o = ctx.Ask<FloorByRoomOptions>();
        var typeId = FloorUtil.FloorType(ctx.Doc, o.FloorType);
        var typeName = ctx.Doc.GetElement(typeId)?.Name ?? "";
        var loc = o.Boundary switch
        {
            BoundaryAt.Center => SpatialElementBoundaryLocation.Center,
            BoundaryAt.CoreBoundary => SpatialElementBoundaryLocation.CoreBoundary,
            _ => SpatialElementBoundaryLocation.Finish,
        };
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(ctx.Doc, ctx.Report);
            foreach (var room in rooms)
            {
                var level = room.Level;
                if (level == null) continue;
                foreach (var reg in RevitGeo.RoomRegions(room, loc))
                {
                    if (o.SkipExisting && FloorUtil.FloorsInRoom(ctx.Doc, room, reg).Area > reg.Area * 0.5)
                    {
                        ctx.Report.Skipped();
                        continue;
                    }
                    try
                    {
                        var f = FloorUtil.CreateFloor(ctx.Doc, reg, typeId, level, o.Offset);
                        RevitGeo.Set(f, FloorUtil.RoomLabel(room), SharedParams.Ambiente);
                        if (o.FillFinish) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_FLOOR, typeName);
                        ctx.Report.Created();
                        ctx.Report.AddResult(f.Id.Value);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.Warn(Loc.F("{0}: {1}", "{0}: {1}", FloorUtil.RoomLabel(room), ex.Message), room.Id.Value);
                    }
                }
            }
        });
    }
}

public sealed class FloorRegionsOptions
{
    [Opt("Tipo de piso das regiões", "Region floor type", Kind = OptKind.FloorType, Required = true)]
    public string FloorType { get; set; } = "";

    [Opt("Recortar as regiões do piso base", "Cut the regions out of the base floor", Tip = "O piso base é recriado com os recortes (mesmo tipo e nível).|The base floor is recreated with the cut-outs (same type and level).")]
    public bool CutBase { get; set; } = true;

    [Opt("Excluir as linhas usadas", "Delete the lines used")]
    public bool DeleteLines { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.regions", Modules.Floors, "Dividir piso em regiões", "Split floor into regions", Order = 20,
    DescPt = "Usa contornos fechados de linhas de modelo para criar regiões de piso com outro tipo/material, recortando o piso base.",
    DescEn = "Uses closed model-line loops to create floor regions of another type/material, cutting the base floor.",
    Keywords = "regiao piso dividir recortar region floor split")]
public sealed class FloorRegionsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var baseFloor = (Floor)ctx.PickElement("Escolha o piso base", "Pick the base floor", e => e is Floor);
        var lines = ctx.SelectedOrPick("Selecione as linhas das regiões", "Select the region lines", e => e is CurveElement);
        var o = ctx.Ask<FloorRegionsOptions>();
        var loops = Shapes.ChainLoops(lines.OfType<CurveElement>().Select(l => l.GeometryCurve))
            .Select(chain => RevitGeo.LoopToPolygon(chain)).Where(p => !p.IsEmpty).ToList();
        if (loops.Count == 0) ToolContext.Fail("As linhas não formam contornos fechados.", "The lines do not form closed loops.");
        var level = (Level)ctx.Doc.GetElement(baseFloor.LevelId);
        var offset = baseFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;
        var baseRegion = RevitGeo.HostRegion(baseFloor) ?? throw new ToolException(Loc.T("Contorno do piso base não encontrado.", "Base floor outline not found."));
        var typeId = FloorUtil.FloorType(ctx.Doc, o.FloorType);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var loop in loops)
            {
                var f = FloorUtil.CreateFloor(ctx.Doc, new Region2(loop), typeId, level, offset);
                ctx.Report.Created();
                ctx.Report.AddResult(f.Id.Value);
            }
            if (o.CutBase)
            {
                var holes = baseRegion.Holes.Concat(loops.Where(l => baseRegion.Outer.Contains(l.Centroid))).ToList();
                var nf = FloorUtil.CreateFloor(ctx.Doc, new Region2(baseRegion.Outer, holes), baseFloor.GetTypeId(), level, offset);
                ctx.Doc.Delete(baseFloor.Id);
                ctx.Report.AddResult(nf.Id.Value);
                ctx.Report.Info(Loc.T("Piso base recriado com os recortes.", "Base floor recreated with the cut-outs."));
            }
            if (o.DeleteLines) ctx.Doc.Delete(lines.Select(l => l.Id).ToList());
        });
    }
}

public sealed class BorderOptions
{
    [Opt("Largura da faixa", "Band width", Kind = OptKind.Length, Min = 0.01)]
    public double Width { get; set; } = U.Mm(100);

    [Opt("Tipo de piso da faixa", "Band floor type", Kind = OptKind.FloorType, Required = true)]
    public string BandType { get; set; } = "";

    [Opt("Criar piso interno", "Create inner floor")]
    public bool CreateInner { get; set; } = true;

    [Opt("Tipo de piso interno", "Inner floor type", Kind = OptKind.FloorType, VisibleIf = "CreateInner")]
    public string InnerType { get; set; } = "";

    [Opt("Criar soleiras nas portas", "Create thresholds at doors")]
    public bool Thresholds { get; set; } = true;

    [Opt("Tipo de piso da soleira", "Threshold floor type", Kind = OptKind.FloorType, VisibleIf = "Thresholds")]
    public string ThresholdType { get; set; } = "";

    [Opt("Deslocamento do nível", "Offset from level", Kind = OptKind.Length)]
    public double Offset { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.border", Modules.Floors, "Faixas, bordas e soleiras", "Bands, borders and thresholds", Order = 30,
    DescPt = "Cria faixa perimetral (borda) e piso interno por ambiente, e soleiras de transição sob as portas.",
    DescEn = "Creates a perimeter band (border) and inner floor per room, and transition thresholds under doors.",
    Keywords = "faixa borda soleira transicao border band threshold")]
public sealed class BorderCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = FloorUtil.PickRooms(ctx);
        var o = ctx.Ask<BorderOptions>();
        var doc = ctx.Doc;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms)
            {
                var level = room.Level;
                var reg = RevitGeo.RoomRegion(room);
                if (level == null || reg == null) continue;
                var inner = reg.Offset(-o.Width);
                if (inner.IsEmpty)
                {
                    ctx.Report.Warn(Loc.F("{0}: faixa larga demais para o ambiente.", "{0}: band too wide for the room.", FloorUtil.RoomLabel(room)), room.Id.Value);
                    continue;
                }
                try
                {
                    var bandRegion = new Region2(reg.Outer, new[] { inner.Outer });
                    var band = FloorUtil.CreateFloor(doc, bandRegion, FloorUtil.FloorType(doc, o.BandType), level, o.Offset);
                    ctx.Report.AddResult(band.Id.Value);
                    ctx.Report.Created();
                    if (o.CreateInner)
                    {
                        var f = FloorUtil.CreateFloor(doc, inner, FloorUtil.FloorType(doc, string.IsNullOrWhiteSpace(o.InnerType) ? o.BandType : o.InnerType), level, o.Offset);
                        ctx.Report.AddResult(f.Id.Value);
                        ctx.Report.Created();
                    }
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("{0}: {1}", "{0}: {1}", FloorUtil.RoomLabel(room), ex.Message), room.Id.Value);
                }
                if (o.Thresholds) CreateThresholds(ctx, room, level, o);
            }
        });
    }

    private static void CreateThresholds(ToolContext ctx, Room room, Level level, BorderOptions o)
    {
        var doc = ctx.Doc;
        var typeId = FloorUtil.FloorType(doc, string.IsNullOrWhiteSpace(o.ThresholdType) ? o.BandType : o.ThresholdType);
        var doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().OfType<FamilyInstance>()
            .Where(d => SafeRoom(d, true)?.Id == room.Id || SafeRoom(d, false)?.Id == room.Id).ToList();
        foreach (var d in doors)
        {
            if (d.Host is not Wall wall || d.Location is not LocationPoint lp || wall.Location is not LocationCurve lc) continue;
            if (ctx.Report.ResultElementIds.Contains(d.Id.Value)) continue;
            var width = d.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                        ?? d.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                        ?? d.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0;
            if (width <= 0) continue;
            var dir = lc.Curve is Line l ? l.Direction : d.HandOrientation;
            var center = lc.Curve.Project(lp.Point).XYZPoint.ToVec2();
            var poly = Polygon2.Rectangle(center, width, wall.Width, Math.Atan2(dir.Y, dir.X));
            try
            {
                var f = FloorUtil.CreateFloor(doc, new Region2(poly), typeId, level, o.Offset);
                ctx.Report.Count(Loc.T("Soleiras", "Thresholds"));
                ctx.Report.AddResult(f.Id.Value);
                ctx.Report.AddResult(d.Id.Value);
            }
            catch (Exception ex)
            {
                Log.Debug("Soleira: " + ex.Message);
            }
        }
    }

    internal static Room? SafeRoom(FamilyInstance d, bool from)
    {
        try
        {
            return from ? d.FromRoom : d.ToRoom;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Opções de paginação (comuns à paginação, quantitativos e forros).</summary>
public class TileOptions
{
    [Opt("Comprimento da peça", "Tile length", Group = "Peça|Tile", Kind = OptKind.Length, Min = 0.005)]
    public double TileLength { get; set; } = U.Mm(600);

    [Opt("Largura da peça", "Tile width", Group = "Peça|Tile", Kind = OptKind.Length, Min = 0.005)]
    public double TileWidth { get; set; } = U.Mm(600);

    [Opt("Junta", "Joint", Group = "Peça|Tile", Kind = OptKind.Length, Min = 0)]
    public double Joint { get; set; } = U.Mm(2);

    [Opt("Paginação", "Layout", Group = "Paginação|Layout")]
    public TileLayout Layout { get; set; } = TileLayout.Grid;

    [Opt("Deslocamento da amarração (0–1)", "Bond offset (0–1)", Group = "Paginação|Layout", Min = 0, Max = 1, VisibleIf = "Layout=RunningBond")]
    public double BondOffset { get; set; } = 0.5;

    [Opt("Rotação", "Rotation", Group = "Paginação|Layout", Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Ponto inicial", "Start point", Group = "Paginação|Layout")]
    public TileStart Start { get; set; } = TileStart.Center;

    [Opt("Ajuste fino X", "Fine offset X", Group = "Paginação|Layout", Kind = OptKind.Length)]
    public double OffsetX { get; set; }

    [Opt("Ajuste fino Y", "Fine offset Y", Group = "Paginação|Layout", Kind = OptKind.Length)]
    public double OffsetY { get; set; }

    [Opt("Sugerir melhor ponto inicial (evitar filetes)", "Suggest best start point (avoid slivers)", Group = "Paginação|Layout")]
    public bool Optimize { get; set; }

    [Opt("Menor recorte aceitável", "Smallest acceptable cut", Group = "Quantitativos|Quantities", Kind = OptKind.Length, Min = 0)]
    public double MinCutSize { get; set; } = U.Mm(50);

    [Opt("Perdas (%)", "Losses (%)", Group = "Quantitativos|Quantities", Min = 0, Max = 100)]
    public double LossPercent { get; set; } = 10;

    [Opt("Peças por caixa (0 = ignorar)", "Tiles per box (0 = ignore)", Group = "Quantitativos|Quantities", Min = 0, Max = 10000)]
    public int TilesPerBox { get; set; }

    [Opt("Reaproveitar sobras de recortes", "Reuse cut offcuts", Group = "Quantitativos|Quantities")]
    public bool ReuseOffcuts { get; set; } = true;

    public TileSettings ToCore() => new()
    {
        TileLength = TileLength,
        TileWidth = TileWidth,
        Joint = Joint,
        Layout = Layout,
        BondOffset = BondOffset,
        RotationDeg = Rotation,
        Start = Start,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        MinCutSize = MinCutSize,
        LossPercent = LossPercent,
        TilesPerBox = TilesPerBox,
        ReuseOffcuts = ReuseOffcuts,
    };

    public static TileLayoutResult Layout2(Region2 region, TileSettings s, bool optimize, out TileSettings used)
    {
        if (optimize)
        {
            var (best, r) = TileLayoutEngine.OptimizeStart(region, s, 6);
            used = best;
            return r;
        }
        used = s;
        return TileLayoutEngine.Generate(region, s);
    }

    public static PreviewModel Preview(Region2 region, TileSettings s, bool optimize)
    {
        var r = Layout2(region, s, optimize, out _);
        var pm = new PreviewModel();
        var slivers = r.Slivers.Select(t => t.Index).ToHashSet();
        foreach (var t in r.Tiles.Take(15000))
        {
            var fill = slivers.Contains(t.Index) ? "#E07A6E" : t.Status == TileStatus.Full ? "#E6D2BE" : "#F4E9DE";
            pm.Polygon(t.Clipped, fill, "#8A6A50", 0.5);
        }
        pm.Region(region, "#3A4552");
        pm.Info = Loc.F("{0} inteiras · {1} recortadas · {2} filetes · comprar {3} · perda {4:0.0}%",
            "{0} full · {1} cut · {2} slivers · buy {3} · waste {4:0.0}%", r.FullCount, r.CutCount, r.Slivers.Count, r.TilesToBuy, r.WastePercent);
        return pm;
    }
}

public enum PagingHost
{
    [Text("Pisos", "Floors")] Floors,
    [Text("Face de parede", "Wall face")] WallFace,
    [Text("Forros", "Ceilings")] Ceilings,
}

public sealed class PagingOptions : TileOptions
{
    [Opt("Material das peças", "Tile material", Group = "Resultado|Result", Kind = OptKind.Material)]
    public string Material { get; set; } = "";

    [Opt("Marcar peças inteiras/recortadas (TT_Status)", "Mark full/cut pieces (TT_Status)", Group = "Resultado|Result")]
    public bool MarkStatus { get; set; } = true;

    [Opt("Criar tabela de paginação", "Create tiling schedule", Group = "Resultado|Result")]
    public bool CreateSchedule { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.paging", Modules.Floors, "Paginação de revestimento", "Tile layout", Order = 40, Large = true,
    DescPt = "Pagina pisos, paredes e forros com peças reais (partes do Revit) e juntas: alinhada, amarração, diagonal, espinha de peixe, trama e modular.",
    DescEn = "Lays out floors, walls and ceilings with real pieces (Revit parts) and joints: grid, running bond, diagonal, herringbone, basketweave and modular.",
    HelpPt = "O revestimento é dividido em PARTES nativas do Revit com a junta como folga: cada peça é um elemento com área, material e status (inteira/recortada), visível em vistas com 'Partes' e contável em tabelas.\n\n• Ponto inicial: canto, peça centralizada, junta centralizada ou ponto clicado.\n• 'Sugerir melhor ponto inicial' testa deslocamentos e escolhe o que evita filetes (recortes finos) e reduz compras.\n• Filetes menores que o recorte mínimo aparecem em vermelho na pré-visualização e no relatório.\n\nPara refazer: desfaça (Ctrl+Z) ou exclua as partes do elemento e rode de novo.",
    HelpEn = "The finish is divided into native Revit PARTS with the joint as gap: each piece has area, material and status (full/cut).\n\n• 'Suggest best start point' tests offsets and picks the one that avoids slivers and reduces purchases.",
    Keywords = "paginacao revestimento piso porcelanato ceramica juntas pecas espinha peixe tile layout herringbone joints")]
public sealed class PagingCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var mode = Msg.Choose(Loc.T("O que paginar?", "What to lay out?"), ctx.Tool.Name,
            Loc.T("Pisos", "Floors"), Loc.T("Face de parede", "Wall face"), Loc.T("Forros", "Ceilings"));
        if (mode < 0) ctx.Cancel();
        var host = (PagingHost)mode;
        var jobs = new List<(Element Host, Region2 Region, Frame3 Frame, Plane Plane, PartsService.Side Side)>();
        if (host == PagingHost.WallFace)
        {
            var r = ctx.PickFace("Escolha a face da parede a revestir", "Pick the wall face to tile", e => e is Wall);
            var wall = ctx.Doc.GetElement(r);
            if (wall.GetGeometryObjectFromReference(r) is not PlanarFace pf) throw new ToolException(Loc.T("A face precisa ser plana.", "The face must be planar."));
            var (reg, frame) = Generators.GenHost.PlanarFaceRegion(pf, Transform.Identity);
            jobs.Add((wall, reg, frame, Plane.CreateByNormalAndOrigin(frame.ZAxis.ToXYZ(), frame.Origin.ToXYZ()), PartsService.Side.All));
        }
        else
        {
            var els = ctx.SelectedOrPick(host == PagingHost.Floors ? "Selecione os pisos" : "Selecione os forros", host == PagingHost.Floors ? "Select the floors" : "Select the ceilings",
                e => host == PagingHost.Floors ? e is Floor : e is Ceiling);
            foreach (var e in els)
            {
                var reg = RevitGeo.HostRegion(e);
                var b = RevitGeo.Box(e);
                if (reg == null || b == null)
                {
                    ctx.Report.Warn(Loc.T("Contorno não encontrado.", "Outline not found."), e.Id.Value);
                    continue;
                }
                var z = host == PagingHost.Floors ? b.Max.Z : b.Min.Z;
                jobs.Add((e, reg, Frame3.World with { Origin = new Vec3(0, 0, z) }, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z)),
                    host == PagingHost.Floors ? PartsService.Side.Top : PartsService.Side.Bottom));
            }
        }
        if (jobs.Count == 0) ctx.Cancel();
        var first = jobs[0];
        var o = ctx.Ask(ctx.LoadOptions<PagingOptions>(), preview: x => TileOptions.Preview(first.Region, x.ToCore(), x.Optimize), okText: Loc.T("Paginar", "Lay out"));
        XYZ? custom = o.Start == TileStart.Custom ? ctx.PickPoint("Clique no ponto inicial da paginação", "Click the layout start point") : null;
        var systemId = GenSystem.NewId();
        var summary = new List<TableData>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(ctx.Doc, ctx.Report);
            var mat = Lookup.Material(ctx.Doc, o.Material);
            var jobIndex = 0;
            foreach (var job in jobs)
            {
                var s = o.ToCore();
                if (custom != null)
                {
                    var l = job.Frame.ToLocal(custom.ToVec3());
                    s.StartX = l.X;
                    s.StartY = l.Y;
                }
                var result = TileOptions.Layout2(job.Region, s, o.Optimize, out _);
                var label = Lookup.ElementLabel(job.Host) + $" [{job.Host.Id.Value}]";
                summary.Add(result.Summary(label));
                foreach (var sl in result.Slivers.Take(50))
                    ctx.Report.Warn(Loc.F("{0}: recorte de {1} (abaixo do mínimo).", "{0}: {1} cut (below minimum).", label, U.FormatCm(sl.MinPieceSize)), job.Host.Id.Value);
                List<Part> parts;
                try
                {
                    parts = PartsService.FinishLayer(PartsService.EnsureParts(ctx.Doc, job.Host), job.Side, job.Side == PartsService.Side.All ? job.Plane : null);
                }
                catch (ToolException ex)
                {
                    ctx.Report.Warn(ex.Message, job.Host.Id.Value);
                    continue;
                }
                var curves = new List<Curve>();
                foreach (var seg in result.JointLines)
                {
                    var a = job.Frame.ToWorld(seg.A.X, seg.A.Y, 0).ToXYZ();
                    var b = job.Frame.ToWorld(seg.B.X, seg.B.Y, 0).ToXYZ();
                    if (job.Side != PartsService.Side.All)
                    {
                        a = new XYZ(seg.A.X, seg.A.Y, job.Frame.Origin.Z);
                        b = new XYZ(seg.B.X, seg.B.Y, job.Frame.Origin.Z);
                    }
                    if (a.DistanceTo(b) > Tolerance.ShortCurve) curves.Add(Line.CreateBound(a, b));
                }
                try
                {
                    PartsService.Divide(ctx.Doc, parts.Select(p => p.Id).ToList(), curves, job.Plane, o.Joint);
                    ctx.Doc.Regenerate();
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("{0}: divisão falhou ({1})", "{0}: division failed ({1})", label, ex.Message), job.Host.Id.Value);
                    continue;
                }
                var leaf = PartsService.FinishLayer(PartUtils.GetAssociatedParts(ctx.Doc, job.Host.Id, false, true)
                    .Select(id => ctx.Doc.GetElement(id)).OfType<Part>().Where(p => !PartUtils.HasAssociatedParts(ctx.Doc, p.Id)).ToList(), job.Side, job.Side == PartsService.Side.All ? job.Plane : null);
                var tileArea = o.TileLength * o.TileWidth;
                var idx = 0;
                foreach (var p in leaf)
                {
                    if (mat != null)
                    {
                        RevitGeo.Set(p, BuiltInParameter.DPART_MATERIAL_BY_ORIGINAL, 0);
                        RevitGeo.Set(p, BuiltInParameter.DPART_MATERIAL_ID_PARAM, mat.Id);
                    }
                    GenSystem.Mark(p, systemId, "floor.paging", jobIndex * 100000 + idx++);
                    if (o.MarkStatus)
                    {
                        var area = p.get_Parameter(BuiltInParameter.DPART_AREA_COMPUTED)?.AsDouble() ?? 0;
                        RevitGeo.Set(p, area >= tileArea * 0.97 ? Loc.T("Inteira", "Full") : Loc.T("Recortada", "Cut"), SharedParams.Status);
                    }
                }
                ctx.Report.Count(Loc.T("Peças (partes)", "Pieces (parts)"), leaf.Count);
                ctx.Report.AddResult(job.Host.Id.Value);
                jobIndex++;
            }
            if (o.CreateSchedule)
            {
                Schedules.Create(ctx.Doc, BuiltInCategory.OST_Parts, Loc.F("TT Paginação {0}", "TT Tiling {0}", systemId),
                    new[]
                    {
                        new Schedules.Spec("BIP:DPART_MATERIAL_ID_PARAM", Loc.T("Material", "Material")),
                        new Schedules.Spec(SharedParams.Status, Loc.T("Situação", "Status")),
                        new Schedules.Spec("BIP:DPART_AREA_COMPUTED", Loc.T("Área", "Area"), Totals: true),
                        new Schedules.Spec("#count", Loc.T("Peças", "Pieces")),
                    },
                    new[] { (SharedParams.Sistema, ScheduleFilterType.Equal, systemId) },
                    new[] { "BIP:DPART_MATERIAL_ID_PARAM", SharedParams.Status }, itemized: false, report: ctx.Report);
                ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
            }
        });
        ctx.Report.Tables.AddRange(summary);
        ctx.Report.ForceShow = true;
        if (ctx.ActiveView.PartsVisibility != PartsVisibility.ShowPartsOnly && ctx.ActiveView.PartsVisibility != PartsVisibility.ShowPartsAndOriginal)
            ctx.Report.Info(Loc.T("Dica: em Propriedades da vista, ajuste 'Visibilidade das partes' para 'Mostrar partes'.", "Tip: in view Properties set 'Parts Visibility' to 'Show Parts'."));
    }
}

public enum BaseboardMode
{
    [Text("Parede fina (rodapé como parede, por ambiente)", "Thin wall (baseboard as wall, per room)")] ThinWall,
}

public sealed class BaseboardOptions
{
    [Opt("Tipo de parede do rodapé", "Baseboard wall type", Kind = OptKind.WallType, Required = true, Tip = "Use um tipo fino (ex.: 15 mm) com o material do rodapé.|Use a thin type (e.g. 15 mm) with the baseboard material.")]
    public string WallType { get; set; } = "";

    [Opt("Altura do rodapé", "Baseboard height", Kind = OptKind.Length, Min = 0.005)]
    public double Height { get; set; } = U.Mm(70);

    [Opt("Deslocamento da base", "Base offset", Kind = OptKind.Length)]
    public double BaseOffset { get; set; }

    [Opt("Interromper nas portas", "Stop at doors")]
    public bool SkipDoors { get; set; } = true;

    [Opt("Folga extra junto às portas", "Extra gap at doors", Kind = OptKind.Length, Min = 0, VisibleIf = "SkipDoors")]
    public double DoorGap { get; set; }

    [Opt("Ignorar trechos menores que", "Skip pieces shorter than", Kind = OptKind.Length, Min = 0)]
    public double MinLength { get; set; } = U.Mm(50);

    [Opt("Preencher acabamento de rodapé do ambiente", "Fill the room base finish parameter")]
    public bool FillFinish { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.baseboard", Modules.Floors, "Rodapés automáticos", "Automatic baseboards", Order = 50, Large = true,
    DescPt = "Cria rodapés ao longo do contorno dos ambientes, interrompendo nas portas, sem alterar a área dos ambientes.",
    DescEn = "Creates baseboards along room outlines, stopping at doors, without changing room areas.",
    HelpPt = "O rodapé é criado como parede fina (tipo escolhido) junto à face de acabamento, com 'Delimitação de ambiente' desligada e sem unir com as paredes. Assim aparece em tabelas de paredes/materiais e pode ser editado normalmente.",
    HelpEn = "The baseboard is a thin wall (chosen type) at the finish face, with 'Room Bounding' off and no wall joins, so it appears in wall/material schedules and remains editable.",
    Keywords = "rodape rodapés baseboard skirting")]
public sealed class BaseboardCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = FloorUtil.PickRooms(ctx);
        var o = ctx.Ask<BaseboardOptions>();
        var doc = ctx.Doc;
        var wt = Lookup.Named<WallType>(doc, o.WallType) ?? throw new ToolException(Loc.T("Escolha o tipo de parede do rodapé.", "Choose the baseboard wall type."));
        var thick = wt.Width;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
            foreach (var room in rooms)
            {
                var level = room.Level;
                if (level == null) continue;
                var doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().OfType<FamilyInstance>()
                    .Where(d => BorderCommand.SafeRoom(d, true)?.Id == room.Id || BorderCommand.SafeRoom(d, false)?.Id == room.Id).ToList();
                var length = 0.0;
                foreach (var loop in room.GetBoundarySegments(opts))
                {
                    foreach (var seg in loop)
                    {
                        if (doc.GetElement(seg.ElementId) is not Wall) continue;
                        var curve = seg.GetCurve();
                        var offsetCurve = InwardOffset(room, curve, thick / 2);
                        if (offsetCurve == null) continue;
                        foreach (var piece in SplitAtDoors(offsetCurve, o.SkipDoors ? doors : new List<FamilyInstance>(), o.DoorGap))
                        {
                            if (piece.Length < Math.Max(o.MinLength, Tolerance.ShortCurve)) continue;
                            try
                            {
                                var flat = piece.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, level.Elevation - piece.GetEndPoint(0).Z)));
                                var w = Wall.Create(doc, flat, wt.Id, level.Id, o.Height, o.BaseOffset, false, false);
                                RevitGeo.Set(w, BuiltInParameter.WALL_ATTR_ROOM_BOUNDING, 0);
                                WallUtils.DisallowWallJoinAtEnd(w, 0);
                                WallUtils.DisallowWallJoinAtEnd(w, 1);
                                RevitGeo.Set(w, FloorUtil.RoomLabel(room), SharedParams.Ambiente);
                                length += piece.Length;
                                ctx.Report.Created();
                                ctx.Report.AddResult(w.Id.Value);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("Rodapé: " + ex.Message);
                            }
                        }
                    }
                }
                if (o.FillFinish) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_BASE, wt.Name);
                ctx.Report.Info(Loc.F("{0}: {1} de rodapé", "{0}: {1} of baseboard", FloorUtil.RoomLabel(room), U.FormatM(length)), room.Id.Value);
            }
        });
    }

    /// <summary>Desloca a curva para dentro do ambiente (testa o lado com IsPointInRoom).</summary>
    private static Curve? InwardOffset(Room room, Curve c, double d)
    {
        try
        {
            var mid = c.Evaluate(0.5, true);
            var tan = c.ComputeDerivatives(0.5, true).BasisX.Normalize();
            var left = XYZ.BasisZ.CrossProduct(tan).Normalize();
            var probe = mid + left * Math.Max(d, 0.05) + XYZ.BasisZ * 1.0;
            var sign = room.IsPointInRoom(probe) ? 1 : -1;
            return c.CreateOffset(sign * d, XYZ.BasisZ);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Curve> SplitAtDoors(Curve c, List<FamilyInstance> doors, double extra)
    {
        if (c is not Line line || doors.Count == 0)
        {
            yield return c;
            yield break;
        }
        var p0 = line.GetEndPoint(0);
        var dir = line.Direction;
        var len = line.Length;
        var gaps = new List<(double A, double B)>();
        foreach (var d in doors)
        {
            if (d.Location is not LocationPoint lp) continue;
            var v = new XYZ(lp.Point.X - p0.X, lp.Point.Y - p0.Y, 0);
            var s = v.DotProduct(new XYZ(dir.X, dir.Y, 0));
            var dist = (v - new XYZ(dir.X, dir.Y, 0) * s).GetLength();
            var hostWidth = (d.Host as Wall)?.Width ?? 0.5;
            if (dist > hostWidth + 0.3 || s < -1 || s > len + 1) continue;
            var w = d.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                    ?? d.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                    ?? d.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0;
            if (w <= 0) continue;
            gaps.Add((s - w / 2 - extra, s + w / 2 + extra));
        }
        if (gaps.Count == 0)
        {
            yield return c;
            yield break;
        }
        var cur = 0.0;
        foreach (var (a, b) in gaps.OrderBy(g => g.A))
        {
            if (a > cur + Tolerance.ShortCurve) yield return Line.CreateBound(p0 + dir * cur, p0 + dir * Math.Min(a, len));
            cur = Math.Max(cur, b);
            if (cur >= len) yield break;
        }
        if (len - cur > Tolerance.ShortCurve) yield return Line.CreateBound(p0 + dir * cur, p0 + dir * len);
    }
}

public sealed class TileQuantityOptions : TileOptions
{
    [Opt("Calcular para", "Compute for", Group = "Origem|Source")]
    public QuantitySource Source { get; set; } = QuantitySource.Rooms;
}

public enum QuantitySource
{
    [Text("Ambientes selecionados", "Selected rooms")] Rooms,
    [Text("Pisos selecionados", "Selected floors")] Floors,
    [Text("Forros selecionados", "Selected ceilings")] Ceilings,
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.quantities", Modules.Floors, "Quantitativo de revestimento", "Finish quantities", Order = 60,
    DescPt = "Calcula peças inteiras, recortes, perdas, caixas e área a comprar por ambiente, piso ou forro — sem alterar o modelo.",
    DescEn = "Computes full pieces, cuts, losses, boxes and area to buy per room, floor or ceiling — without changing the model.",
    Keywords = "quantitativo revestimento pecas caixas perdas compra tile quantities boxes waste")]
public sealed class TileQuantityCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<TileQuantityOptions>();
        var regions = new List<(string Name, Region2 Region, long Id)>();
        switch (o.Source)
        {
            case QuantitySource.Rooms:
                foreach (var r in FloorUtil.PickRooms(ctx))
                    if (RevitGeo.RoomRegion(r) is { } reg) regions.Add((FloorUtil.RoomLabel(r), reg, r.Id.Value));
                break;
            default:
                var els = ctx.SelectedOrPick("Selecione os elementos", "Select the elements", e => o.Source == QuantitySource.Floors ? e is Floor : e is Ceiling);
                foreach (var e in els)
                    if (RevitGeo.HostRegion(e) is { } reg) regions.Add((Lookup.ElementLabel(e) + $" [{e.Id.Value}]", reg, e.Id.Value));
                break;
        }
        var t = new TableData(Loc.T("Quantitativo de revestimento", "Finish quantities"));
        t.AddColumn(Loc.T("Local", "Location")).AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Peça", "Tile")).AddColumn(Loc.T("Inteiras", "Full"), ColumnKind.Integer).AddColumn(Loc.T("Recortadas", "Cut"), ColumnKind.Integer)
         .AddColumn(Loc.T("Filetes", "Slivers"), ColumnKind.Integer).AddColumn(Loc.T("Comprar", "To buy"), ColumnKind.Integer)
         .AddColumn(Loc.T("Caixas", "Boxes"), ColumnKind.Integer).AddColumn(Loc.T("Perda (%)", "Waste (%)"), ColumnKind.Number, 1);
        foreach (var (name, reg, id) in regions)
        {
            var r = TileOptions.Layout2(reg, o.ToCore(), o.Optimize, out _);
            foreach (var b in r.BySize)
                t.AddRowWithIds(new[] { id }, name, U.SqFtToM2(reg.Area), b.SizeKey, b.Full, b.Cut, r.Slivers.Count, b.ToBuy, b.Boxes, r.WastePercent);
        }
        t.AddTotalsRow(Loc.T("TOTAL", "TOTAL"), 1, 3, 4, 5, 6, 7);
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.uncovered", Modules.Floors, "Ambientes sem revestimento", "Rooms without finish", Order = 70,
    DescPt = "Lista ambientes sem piso modelado (ou sem acabamento de piso informado) para conferência.",
    DescEn = "Lists rooms without a modeled floor (or without a floor finish value) for review.",
    Keywords = "ambientes sem piso revestimento faltando uncovered missing floor")]
public sealed class UncoveredCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = RevitGeo.Rooms(ctx.Doc).Where(r => r.Area > 0).ToList();
        var t = new TableData(Loc.T("Ambientes sem revestimento", "Rooms without finish"));
        t.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Nível", "Level")).AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Piso coberto (%)", "Floor covered (%)"), ColumnKind.Number, 0).AddColumn(Loc.T("Acabamento informado", "Finish value"));
        foreach (var room in rooms)
        {
            var reg = RevitGeo.RoomRegion(room);
            if (reg == null) continue;
            var (_, area) = FloorUtil.FloorsInRoom(ctx.Doc, room, reg);
            var pct = reg.Area > 0 ? Math.Min(100, area / reg.Area * 100) : 0;
            var finish = RevitGeo.Text(room, BuiltInParameter.ROOM_FINISH_FLOOR);
            if (pct < 95 || string.IsNullOrWhiteSpace(finish))
            {
                t.AddRowWithIds(new[] { room.Id.Value }, FloorUtil.RoomLabel(room), room.Level?.Name ?? "", U.SqFtToM2(room.Area), pct, finish);
                ctx.Report.Warn(Loc.F("{0}: piso {1:0}% coberto{2}", "{0}: floor {1:0}% covered{2}", FloorUtil.RoomLabel(room), pct,
                    string.IsNullOrWhiteSpace(finish) ? Loc.T(", sem acabamento informado", ", no finish value") : ""), room.Id.Value);
            }
        }
        if (t.RowCount == 0) ctx.Report.Success(Loc.T("Todos os ambientes têm piso e acabamento.", "All rooms have floor and finish."));
        else ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
    }
}

public sealed class PagingViewOptions
{
    [Opt("Criar vista de paginação (cópia da planta ativa)", "Create tiling view (copy of active plan)")]
    public bool CreateView { get; set; } = true;

    [Opt("Colorir peças recortadas", "Color cut pieces", VisibleIf = "CreateView")]
    public bool ColorCuts { get; set; } = true;

    [Opt("Criar tabela de peças por material", "Create pieces-by-material schedule")]
    public bool CreateSchedule { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("floor.pagingviews", Modules.Floors, "Vistas e tabelas de paginação", "Tiling views and schedules", Order = 80,
    DescPt = "Cria a planta de paginação (partes visíveis, recortes destacados) e a tabela de peças por material e situação.",
    DescEn = "Creates the tiling plan (parts visible, cuts highlighted) and the pieces-by-material-and-status schedule.",
    Keywords = "vista paginacao legenda tabela partes tiling view schedule")]
public sealed class PagingViewsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var view = ctx.ActiveView;
        var o = ctx.Ask<PagingViewOptions>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.CreateView)
            {
                if (!RevitGeo.IsPlanLike(view) || !view.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                    throw new ToolException(Loc.T("Abra uma planta para criar a vista de paginação.", "Open a plan to create the tiling view."));
                var v = (View)ctx.Doc.GetElement(view.Duplicate(ViewDuplicateOption.WithDetailing));
                v.Name = Lookup.UniqueViewName(ctx.Doc, view.Name + Loc.T(" — Paginação", " — Tiling"));
                if (v.ViewTemplateId != ElementId.InvalidElementId) v.ViewTemplateId = ElementId.InvalidElementId;
                v.PartsVisibility = PartsVisibility.ShowPartsOnly;
                if (o.ColorCuts)
                {
                    var statusId = ViewFilters.SharedParamId(ctx.Doc, SharedParams.Status);
                    if (statusId != ElementId.InvalidElementId)
                    {
                        var rule = ParameterFilterRuleFactory.CreateEqualsRule(statusId, Loc.T("Recortada", "Cut"));
                        var f = ViewFilters.Ensure(ctx.Doc, "TT Peças recortadas", ViewFilters.CategoryIds(BuiltInCategory.OST_Parts), new ElementParameterFilter(rule));
                        ViewFilters.Apply(v, f.Id, ViewFilters.Color(ctx.Doc, new Core.Colors.RgbColor(244, 200, 170)));
                    }
                }
                ctx.Report.Count(Loc.T("Vistas", "Views"));
                ctx.Report.AddResult(v.Id.Value);
            }
            if (o.CreateSchedule)
            {
                var vs = Schedules.Create(ctx.Doc, BuiltInCategory.OST_Parts, Loc.T("TT Peças de revestimento", "TT Finish pieces"),
                    new[]
                    {
                        new Schedules.Spec("BIP:DPART_MATERIAL_ID_PARAM", Loc.T("Material", "Material")),
                        new Schedules.Spec(SharedParams.Status, Loc.T("Situação", "Status")),
                        new Schedules.Spec("BIP:DPART_AREA_COMPUTED", Loc.T("Área", "Area"), Totals: true),
                        new Schedules.Spec("#count", Loc.T("Peças", "Pieces")),
                    }, null, new[] { "BIP:DPART_MATERIAL_ID_PARAM", SharedParams.Status }, itemized: false, report: ctx.Report);
                ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
                ctx.Report.AddResult(vs.Id.Value);
            }
        });
    }
}
