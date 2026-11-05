using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

// ============================================================ Pintura de faces

public sealed class WallPaintOptions
{
    [Opt("Material", "Material", Kind = OptKind.Material, VisibleIf = "!Remove")]
    public string Material { get; set; } = "";

    [Opt("Remover pintura (em vez de aplicar)", "Remove paint (instead of applying)")]
    public bool Remove { get; set; }

    [Opt("Preencher \"Acabamento de parede\" do ambiente", "Fill the room \"Wall finish\"", VisibleIf = "!Remove")]
    public bool FillFinish { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("wall.paint", Modules.Interiors, "Pintar paredes do ambiente", "Paint room walls", Order = 60, Group = "Paredes|Walls",
    DescPt = "Aplica (ou remove) um material nas faces das paredes voltadas para os ambientes escolhidos, sem alterar o tipo das paredes.",
    DescEn = "Applies (or removes) a material on the wall faces facing the chosen rooms, without changing the wall types.",
    HelpPt = "Se uma face continua em outro ambiente, o Revit pinta a face inteira. Para pintar só o trecho do ambiente, use antes \"Dividir face\" do Revit no limite entre os ambientes: o plugin pinta apenas as regiões voltadas para o ambiente escolhido.",
    HelpEn = "If a face continues into another room, Revit paints the whole face. To paint only the room's portion, first use Revit's \"Split Face\" at the boundary: the plugin then paints only the regions facing the chosen room.",
    Keywords = "pintar parede pintura material face ambiente paint wall room face material")]
public sealed class WallPaintCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var o = ctx.Ask<WallPaintOptions>();
        var doc = ctx.Doc;
        var mat = o.Remove ? null : Lookup.Material(doc, o.Material) ?? throw new ToolException(Loc.T("Escolha o material.", "Choose the material."));
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms)
            {
                var faces = RoomWallPainter.Paint(doc, room, mat, ctx.Report);
                ctx.Report.Count(Loc.T("Faces", "Faces"), faces);
                if (mat != null && o.FillFinish) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_WALL, mat.Name);
                if (faces > 0) ctx.Report.AddResult(room.Id.Value);
                else ctx.ItemFailed("Ambientes sem parede pintada", "Rooms with no wall painted",
                    Loc.T("nenhuma face de parede voltada para o ambiente foi encontrada (o ambiente é delimitado por paredes?)", "no wall face facing the room was found"), room.Id.Value);
            }
            ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Faces", "Faces")), "faces de parede", "wall faces");
        });
    }
}

/// <summary>Pinta (ou remove a pintura) das faces de parede voltadas para um ambiente.</summary>
public static class RoomWallPainter
{
    public static int Paint(Document doc, Room room, Material? mat, Core.Reporting.OperationReport report)
    {
        var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        var lz = (room.Level?.Elevation ?? 0) + 1.0;
        var segs = room.GetBoundarySegments(opts).SelectMany(l => l).Where(s => doc.GetElement(s.ElementId) is Wall).ToList();
        var faces = 0;
        foreach (var g in segs.GroupBy(s => s.ElementId.Value))
        {
            var wall = (Wall)doc.GetElement(new ElementId(g.Key));
            var segLen = g.Sum(s => s.GetCurve().Length);
            var refs = new List<Reference>();
            try
            {
                refs.AddRange(HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior));
                refs.AddRange(HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior));
            }
            catch
            {
                continue;
            }
            foreach (var r in refs)
            {
                if (wall.GetGeometryObjectFromReference(r) is not Face face) continue;
                var targets = face.HasRegions ? face.GetRegions().ToList() : new List<Face> { face };
                foreach (var f in targets)
                {
                    if (!Faces(room, f, lz)) continue;
                    try
                    {
                        if (mat == null) doc.RemovePaint(wall.Id, f);
                        else doc.Paint(wall.Id, f, mat.Id);
                        faces++;
                        if (!face.HasRegions && HorizontalLength(f) > segLen + 0.5)
                            report.Info(Loc.F("{0}: a face da parede continua além do ambiente (toda a face foi pintada).", "{0}: the wall face continues beyond the room (the whole face was painted).",
                                RoomUtil.Label(room)), wall.Id.Value);
                    }
                    catch (Exception ex)
                    {
                        report.ItemFailed(Loc.T("Faces de parede (pintura)", "Wall faces (paint)"), ex.Message, wall.Id.Value);
                    }
                }
            }
        }
        return faces;
    }

    /// <summary>A face está voltada para o ambiente? (ponto da face deslocado pela normal cai dentro dele)</summary>
    internal static bool Faces(Room room, Face f, double probeZ)
    {
        try
        {
            var bb = f.GetBoundingBox();
            var uv = (bb.Min + bb.Max) / 2;
            if (!f.IsInside(uv))
            {
                var found = false;
                for (var i = 1; i < 4 && !found; i++)
                    for (var j = 1; j < 4 && !found; j++)
                    {
                        var t = new UV(bb.Min.U + (bb.Max.U - bb.Min.U) * i / 4, bb.Min.V + (bb.Max.V - bb.Min.V) * j / 4);
                        if (f.IsInside(t)) { uv = t; found = true; }
                    }
            }
            var p = f.Evaluate(uv);
            var n = f.ComputeNormal(uv).Normalize();
            if (Math.Abs(n.Z) > 0.9) return false;
            var probe = p + n * 0.3;
            return room.IsPointInRoom(new XYZ(probe.X, probe.Y, probeZ));
        }
        catch
        {
            return false;
        }
    }

    internal static double HorizontalLength(Face f)
    {
        var pts = f.Triangulate(0.5).Vertices.Cast<XYZ>().ToList();
        if (pts.Count < 2) return 0;
        var best = 0.0;
        foreach (var a in pts.Take(60))
            foreach (var b in pts.Take(60))
                best = Math.Max(best, new XYZ(a.X - b.X, a.Y - b.Y, 0).GetLength());
        return best;
    }
}

// ============================================================ Revestimento de parede

internal static class WallFinishBuilder
{
    public readonly record struct Opening(double S0, double S1, double Z0, double Z1);

    public static Curve? OffsetIntoRoom(Room room, Curve c, double d, double probeZ) => RevitGeo.OffsetIntoRoom(room, c, d, probeZ);

    /// <summary>Vãos (portas, janelas, aberturas) da parede projetados na linha do revestimento.</summary>
    public static List<Opening> Openings(Document doc, Wall host, Line line)
    {
        var list = new List<Opening>();
        var p0 = line.GetEndPoint(0);
        var dir = new XYZ(line.Direction.X, line.Direction.Y, 0).Normalize();
        var len = line.Length;
        foreach (var id in host.FindInserts(true, false, true, true))
        {
            var ins = doc.GetElement(id);
            if (ins == null) continue;
            double s0, s1, z0, z1;
            var bb = RevitGeo.Box(ins);
            if (ins is FamilyInstance fi && fi.Location is LocationPoint lp)
            {
                var w = new[]
                {
                    fi.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? 0, fi.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? 0,
                    fi.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble() ?? 0, fi.Symbol.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble() ?? 0,
                    fi.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0, fi.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM)?.AsDouble() ?? 0,
                }.Max();
                if (w <= 0 && bb == null) continue;
                var s = (new XYZ(lp.Point.X, lp.Point.Y, p0.Z) - p0).DotProduct(dir);
                if (w > 0)
                {
                    s0 = s - w / 2;
                    s1 = s + w / 2;
                }
                else
                {
                    (s0, s1) = Project(bb!, p0, dir);
                }
                var lz = (doc.GetElement(fi.LevelId) as Level)?.Elevation ?? (bb?.Min.Z ?? p0.Z);
                var sill = fi.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.AsDouble();
                var head = fi.get_Parameter(BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM)?.AsDouble();
                z0 = sill.HasValue ? lz + sill.Value : bb?.Min.Z ?? lz;
                z1 = head.HasValue && head.Value > (sill ?? 0) ? lz + head.Value : bb?.Max.Z ?? z0;
            }
            else
            {
                if (bb == null) continue;
                (s0, s1) = Project(bb, p0, dir);
                z0 = bb.Min.Z;
                z1 = bb.Max.Z;
            }
            if (s1 < 0 || s0 > len || z1 - z0 < 1e-3) continue;
            list.Add(new Opening(Math.Max(0, s0), Math.Min(len, s1), z0, z1));
        }
        return list;
    }

    private static (double, double) Project(BoundingBoxXYZ bb, XYZ p0, XYZ dir)
    {
        double s0 = double.MaxValue, s1 = double.MinValue;
        foreach (var x in new[] { bb.Min.X, bb.Max.X })
            foreach (var y in new[] { bb.Min.Y, bb.Max.Y })
            {
                var s = (new XYZ(x, y, p0.Z) - p0).DotProduct(dir);
                s0 = Math.Min(s0, s);
                s1 = Math.Max(s1, s);
            }
        return (s0, s1);
    }

    /// <summary>Trechos (s0, s1, z0, z1) livres de vãos, mesclando trechos contíguos com a mesma faixa de altura.</summary>
    public static List<(double S0, double S1, double Z0, double Z1)> Pieces(double len, double z0, double z1, List<Opening> openings, double minLen, double minHeight)
    {
        var cuts = new SortedSet<double> { 0, len };
        var active = openings.Where(o => o.Z1 > z0 + 1e-6 && o.Z0 < z1 - 1e-6).ToList();
        foreach (var o in active)
        {
            cuts.Add(Math.Clamp(o.S0, 0, len));
            cuts.Add(Math.Clamp(o.S1, 0, len));
        }
        var xs = cuts.ToList();
        var raw = new List<(double S0, double S1, double Z0, double Z1)>();
        for (var i = 0; i + 1 < xs.Count; i++)
        {
            var a = xs[i];
            var b = xs[i + 1];
            if (b - a < 1e-6) continue;
            var mid = (a + b) / 2;
            var blocks = active.Where(o => o.S0 < mid && o.S1 > mid).Select(o => (Math.Max(o.Z0, z0), Math.Min(o.Z1, z1))).OrderBy(r => r.Item1).ToList();
            var cur = z0;
            foreach (var (bz0, bz1) in blocks)
            {
                if (bz0 > cur) raw.Add((a, b, cur, bz0));
                cur = Math.Max(cur, bz1);
            }
            if (z1 > cur) raw.Add((a, b, cur, z1));
        }
        var merged = new List<(double S0, double S1, double Z0, double Z1)>();
        foreach (var p in raw.OrderBy(r => Math.Round(r.Z0, 5)).ThenBy(r => Math.Round(r.Z1, 5)).ThenBy(r => r.S0))
        {
            if (merged.Count > 0)
            {
                var l = merged[^1];
                if (Math.Abs(l.Z0 - p.Z0) < 1e-5 && Math.Abs(l.Z1 - p.Z1) < 1e-5 && Math.Abs(l.S1 - p.S0) < 1e-5)
                {
                    merged[^1] = (l.S0, p.S1, l.Z0, l.Z1);
                    continue;
                }
            }
            merged.Add(p);
        }
        return merged.Where(p => p.S1 - p.S0 >= minLen && p.Z1 - p.Z0 >= minHeight).ToList();
    }
}

public sealed class WallFinishOptions
{
    [Opt("Tipo de parede do revestimento", "Finish wall type", Kind = OptKind.WallType, Required = true,
        Tip = "Crie um tipo fino com o material do revestimento (ex.: Revestimento cerâmico 10 mm).|Create a thin type with the finish material (e.g. Ceramic tile 10 mm).")]
    public string Type { get; set; } = "";

    [Opt("Até o limite superior do ambiente", "Up to the room upper limit")]
    public bool FullHeight { get; set; }

    [Opt("Altura do revestimento", "Finish height", Kind = OptKind.Length, Min = 0.01, VisibleIf = "!FullHeight",
        Tip = "Ex.: 1,20 m para barrado; altura do forro para revestimento completo.|E.g. 1.20 m for wainscot; ceiling height for full finish.")]
    public double Height { get; set; } = U.M(2.6);

    [Opt("Deslocamento da base", "Base offset", Kind = OptKind.Length)]
    public double BaseOffset { get; set; }

    [Opt("Recortar portas, janelas e aberturas", "Cut at doors, windows and openings")]
    public bool CutOpenings { get; set; } = true;

    [Opt("Comprimento mínimo de trecho", "Minimum piece length", Kind = OptKind.Length, Min = 0)]
    public double MinLength { get; set; } = U.Mm(50);

    [Opt("Preencher \"Acabamento de parede\" do ambiente", "Fill the room \"Wall finish\"")]
    public bool FillFinish { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("wall.finish", Modules.Interiors, "Revestimento de parede por ambiente", "Wall finish by room", Order = 61, Group = "Paredes|Walls",
    DescPt = "Cria o revestimento (parede fina) em todo o contorno do ambiente, com a altura escolhida, recortando portas e janelas (acima e abaixo dos vãos).",
    DescEn = "Creates the finish (thin wall) around the whole room boundary, at the chosen height, cutting doors and windows (above and below openings).",
    HelpPt = "O revestimento não delimita ambientes e não se une às paredes existentes; cada trecho recebe TT_Ambiente. Use um tipo de parede fino com o material desejado — assim a área aparece nos quantitativos de parede.",
    HelpEn = "The finish does not bound rooms nor join existing walls; each piece gets TT_Ambiente. Use a thin wall type with the desired material — the area then appears in wall takeoffs.",
    Keywords = "revestimento parede ceramica barrado azulejo ambiente wall finish tile wainscot room")]
public sealed class WallFinishCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var o = ctx.Ask<WallFinishOptions>();
        Execute(ctx, rooms, o);
    }

    /// <summary>Executa com ambientes e opções já definidos (usado também pelo catálogo de acabamentos).</summary>
    internal static void Execute(ToolContext ctx, List<Room> rooms, WallFinishOptions o)
    {
        var doc = ctx.Doc;
        var wt = Lookup.Named<WallType>(doc, o.Type) ?? throw new ToolException(Loc.T("Escolha o tipo de parede do revestimento.", "Choose the finish wall type."));
        var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            foreach (var room in rooms)
            {
                var level = room.Level;
                if (level == null) continue;
                var label = RoomUtil.Label(room);
                var baseZ = level.Elevation + (room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET)?.AsDouble() ?? 0) + o.BaseOffset;
                var topZ = o.FullHeight ? level.Elevation + (room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET)?.AsDouble() ?? 0) + room.UnboundedHeight : baseZ + o.Height;
                if (topZ - baseZ < U.Mm(10)) continue;
                double area = 0;
                foreach (var seg in room.GetBoundarySegments(opts).SelectMany(l => l))
                {
                    if (doc.GetElement(seg.ElementId) is not Wall host) continue;
                    var curve = WallFinishBuilder.OffsetIntoRoom(room, seg.GetCurve(), wt.Width / 2, level.Elevation + 1.0);
                    if (curve == null) continue;
                    curve = curve.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, level.Elevation - curve.GetEndPoint(0).Z)));
                    var pieces = new List<(Curve Curve, double Z0, double Z1)>();
                    if (curve is Line line)
                    {
                        var openings = o.CutOpenings ? WallFinishBuilder.Openings(doc, host, line) : new List<WallFinishBuilder.Opening>();
                        var p0 = line.GetEndPoint(0);
                        foreach (var (s0, s1, z0, z1) in WallFinishBuilder.Pieces(line.Length, baseZ, topZ, openings, Math.Max(o.MinLength, Tolerance.ShortCurve), U.Mm(20)))
                            pieces.Add((Line.CreateBound(p0 + line.Direction * s0, p0 + line.Direction * s1), z0, z1));
                    }
                    else
                    {
                        if (o.CutOpenings && host.FindInserts(true, false, true, true).Count > 0)
                            ctx.Report.Info(Loc.F("{0}: parede curva com vãos — revestimento criado sem recortes.", "{0}: curved wall with openings — finish created without cuts.", label), host.Id.Value);
                        pieces.Add((curve, baseZ, topZ));
                    }
                    foreach (var (c, z0, z1) in pieces)
                    {
                        try
                        {
                            var w = Wall.Create(doc, c, wt.Id, level.Id, z1 - z0, z0 - level.Elevation, false, false);
                            RevitGeo.Set(w, BuiltInParameter.WALL_ATTR_ROOM_BOUNDING, 0);
                            WallUtils.DisallowWallJoinAtEnd(w, 0);
                            WallUtils.DisallowWallJoinAtEnd(w, 1);
                            RevitGeo.Set(w, label, SharedParams.Ambiente);
                            area += c.Length * (z1 - z0);
                            ctx.Report.Created();
                            ctx.Report.AddResult(w.Id.Value);
                        }
                        catch (Exception ex)
                        {
                            ctx.ItemFailed("Trechos de revestimento", "Finish segments", ex, host.Id.Value);
                        }
                    }
                }
                if (o.FillFinish) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_WALL, wt.Name);
                ctx.Report.Info(Loc.F("{0}: {1} de revestimento", "{0}: {1} of finish", label, U.FormatArea(area)), room.Id.Value);
            }
            ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Criados", "Created")), "trechos de revestimento", "finish segments");
        });
    }
}

// ============================================================ Ripado

public enum SlatDirection
{
    [Text("Vertical", "Vertical")] Vertical,
    [Text("Horizontal", "Horizontal")] Horizontal,
}

public sealed class SlatOptions
{
    [Opt("Direção das ripas", "Slat direction", Group = "Ripas|Slats")]
    public SlatDirection Direction { get; set; } = SlatDirection.Vertical;

    [Opt("Largura da ripa", "Slat width", Kind = OptKind.Length, Min = 0.001, Group = "Ripas|Slats")]
    public double Width { get; set; } = U.Mm(40);

    [Opt("Espessura da ripa", "Slat thickness", Kind = OptKind.Length, Min = 0.001, Group = "Ripas|Slats")]
    public double Depth { get; set; } = U.Mm(20);

    [Opt("Espaço entre ripas", "Gap between slats", Kind = OptKind.Length, Min = 0, Group = "Ripas|Slats")]
    public double Gap { get; set; } = U.Mm(20);

    [Opt("Ajustar o espaço para fechar a largura", "Adjust the gap to fill the width", Group = "Ripas|Slats")]
    public bool FitEvenly { get; set; } = true;

    [Opt("Afastamento da face", "Distance from face", Kind = OptKind.Length, Min = 0, Group = "Ripas|Slats")]
    public double Offset { get; set; }

    [Opt("Margem inicial", "Start margin", Kind = OptKind.Length, Min = 0, Group = "Limites|Limits")]
    public double MarginStart { get; set; }

    [Opt("Margem final", "End margin", Kind = OptKind.Length, Min = 0, Group = "Limites|Limits")]
    public double MarginEnd { get; set; }

    [Opt("Recuo inferior", "Bottom offset", Kind = OptKind.Length, Min = 0, Group = "Limites|Limits")]
    public double Bottom { get; set; }

    [Opt("Recuo superior", "Top offset", Kind = OptKind.Length, Min = 0, Group = "Limites|Limits")]
    public double Top { get; set; }

    [Opt("Interromper nas aberturas da face", "Stop at face openings", Group = "Limites|Limits")]
    public bool ClipToFace { get; set; } = true;

    [Opt("Painel de fundo", "Backing panel", Group = "Fundo|Backing")]
    public bool Backing { get; set; } = true;

    [Opt("Espessura do fundo", "Backing thickness", Kind = OptKind.Length, Min = 0.001, Group = "Fundo|Backing", VisibleIf = "Backing")]
    public double BackingThickness { get; set; } = U.Mm(15);

    [Opt("Material das ripas", "Slat material", Kind = OptKind.Material, Group = "Materiais|Materials")]
    public string SlatMaterial { get; set; } = "";

    [Opt("Material do fundo", "Backing material", Kind = OptKind.Material, Group = "Materiais|Materials", VisibleIf = "Backing")]
    public string BackingMaterial { get; set; } = "";

    [Opt("Saída", "Output", Group = "Saída|Output")]
    public GenOutput Output { get; set; } = GenOutput.Parametric;

    [Opt("Família do usuário", "User family", Kind = OptKind.FamilyType, Group = "Saída|Output", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";
}

internal static class FaceBoxes
{
    public static OrientedBox Box(Frame3 f, Rect2 r, double z0, double depth) =>
        new(new Frame3(f.ToWorld(r.MinX, r.MinY, z0), f.XAxis, f.YAxis, f.ZAxis), r.Width, r.Height, depth);

    public static PlanarContext AcquirePlanar(ToolContext ctx)
    {
        var c = GenHost.Acquire(ctx, SurfaceHost.PlanarFace);
        if (c.Curved) ToolContext.Fail("Escolha uma face plana (parede, painel, divisória).", "Pick a planar face (wall, panel, partition).");
        return c;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("wall.slats", Modules.Interiors, "Ripado / painel ripado", "Slatted panel", Order = 62, Large = true, Group = "Paredes|Walls",
    DescPt = "Gera ripado vertical ou horizontal sobre uma face, com painel de fundo, margens, recortes em vãos e materiais — como peças BIM editáveis.",
    DescEn = "Generates vertical or horizontal slats on a face, with backing panel, margins, cuts at openings and materials — as editable BIM pieces.",
    HelpPt = "Escolha a face (parede, divisória, lateral de móvel). As ripas usam a família paramétrica TT_Bloco (largura, profundidade e altura editáveis). Use \"Editar sistema gerado\" para mudar o ripado depois.",
    HelpEn = "Pick the face (wall, partition, furniture side). Slats use the parametric TT_Bloco family. Use \"Edit generated system\" to change it later.",
    Keywords = "ripado ripas painel muxarabi slats slatted panel battens")]
public sealed class SlatsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var c = FaceBoxes.AcquirePlanar(ctx);
        var o = ctx.LoadOptions<SlatOptions>();
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    internal static List<(Rect2 R, bool Backing)> Compute(SlatOptions o, Region2 region)
    {
        var list = new List<(Rect2, bool)>();
        var b = region.Bounds;
        var x0 = b.MinX + o.MarginStart;
        var x1 = b.MaxX - o.MarginEnd;
        var y0 = b.MinY + o.Bottom;
        var y1 = b.MaxY - o.Top;
        if (x1 - x0 < 1e-3 || y1 - y0 < 1e-3) return list;
        var vertical = o.Direction == SlatDirection.Vertical;
        var a0 = vertical ? x0 : y0;
        var a1 = vertical ? x1 : y1;
        var c0 = vertical ? y0 : x0;
        var c1 = vertical ? y1 : x1;
        var len = a1 - a0;
        var n = (int)Math.Floor((len + o.Gap) / (o.Width + o.Gap) + 1e-9);
        if (n >= 1)
        {
            var gap = o.FitEvenly && n > 1 ? (len - n * o.Width) / (n - 1) : o.Gap;
            var start = o.FitEvenly && n > 1 ? a0 : a0 + (len - (n * o.Width + (n - 1) * o.Gap)) / 2;
            for (var i = 0; i < n; i++)
            {
                var a = start + i * (o.Width + gap);
                var m = a + o.Width / 2;
                var seg = vertical ? new Segment2(new Vec2(m, c0), new Vec2(m, c1)) : new Segment2(new Vec2(c0, m), new Vec2(c1, m));
                var pieces = o.ClipToFace ? region.ClipSegment(seg) : new List<Segment2> { seg };
                foreach (var p in pieces)
                {
                    if (p.Length < U.Mm(20)) continue;
                    var r = vertical
                        ? new Rect2(a, Math.Min(p.A.Y, p.B.Y), a + o.Width, Math.Max(p.A.Y, p.B.Y))
                        : new Rect2(Math.Min(p.A.X, p.B.X), a, Math.Max(p.A.X, p.B.X), a + o.Width);
                    list.Add((r, false));
                }
            }
        }
        if (o.Backing)
        {
            var cuts = new SortedSet<double> { x0, x1 };
            if (o.ClipToFace)
                foreach (var h in region.Holes)
                {
                    var hb = h.Bounds;
                    cuts.Add(Math.Clamp(hb.MinX, x0, x1));
                    cuts.Add(Math.Clamp(hb.MaxX, x0, x1));
                }
            var xs = cuts.ToList();
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var xa = xs[i];
                var xb = xs[i + 1];
                if (xb - xa < 1e-3) continue;
                var xm = (xa + xb) / 2;
                var seg = new Segment2(new Vec2(xm, y0), new Vec2(xm, y1));
                foreach (var p in o.ClipToFace ? region.ClipSegment(seg) : new List<Segment2> { seg })
                    if (p.Length > U.Mm(20)) list.Add((new Rect2(xa, Math.Min(p.A.Y, p.B.Y), xb, Math.Max(p.A.Y, p.B.Y)), true));
            }
        }
        return list;
    }

    private static PreviewModel Preview(SlatOptions o, PlanarContext c)
    {
        var reg = c.GetRegion();
        var rects = Compute(o, reg);
        var pm = new PreviewModel();
        pm.Region(reg);
        foreach (var (r, back) in rects.Where(x => x.Backing)) pm.Polygon(r.ToPolygon(), "#EFE6DC", "#B9A48F", 0.6);
        foreach (var (r, _) in rects.Where(x => !x.Backing).Take(4000)) pm.Polygon(r.ToPolygon(), "#B5652B", "#6B4A33", 0.4);
        var slats = rects.Where(x => !x.Backing).ToList();
        var f3 = c.GetFrame();
        var sc = pm.S3;
        sc.Face(f3, reg.Outer, "#D9D4CC", 0.35);
        var backZ = o.Offset;
        var slatZ = o.Offset + (o.Backing ? o.BackingThickness : 0);
        foreach (var (r, back) in rects.Take(8000))
            sc.Box(back ? FaceBoxes.Box(f3, r, backZ, o.BackingThickness) : FaceBoxes.Box(f3, r, slatZ, o.Depth), back ? "#E7DCCD" : "#A9693A");
        var lin = slats.Sum(x => o.Direction == SlatDirection.Vertical ? x.R.Height : x.R.Width);
        pm.Info = Loc.F("{0} ripas · {1} lineares · fundo {2}", "{0} slats · {1} linear · backing {2}",
            slats.Count, U.FormatM(lin), U.FormatArea(rects.Where(x => x.Backing).Sum(x => x.R.Area)));
        return pm;
    }

    internal static void Generate(ToolContext ctx, SlatOptions o, PlanarContext c, string systemId)
    {
        var doc = ctx.Doc;
        var frame = c.GetFrame();
        var rects = Compute(o, c.GetRegion());
        ctx.CheckCount(rects.Count, "peças", "pieces");
        var sink = new BoxSink(ctx, o.Output, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Ripado" };
        sink.Prepare();
        var backZ = o.Offset;
        var slatZ = o.Offset + (o.Backing ? o.BackingThickness : 0);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var i = 0;
            var k = 0;
            foreach (var (r, back) in rects)
            {
                var box = back ? FaceBoxes.Box(frame, r, backZ, o.BackingThickness) : FaceBoxes.Box(frame, r, slatZ, o.Depth);
                var mat = back ? o.BackingMaterial : o.SlatMaterial;
                var code = back ? "FUNDO" : $"R{++k:000}";
                sink.Add(box, string.IsNullOrWhiteSpace(mat) ? null : mat, i++, e =>
                {
                    RevitGeo.Set(e, code, SharedParams.Codigo);
                    RevitGeo.Set(e, back ? Loc.T("Fundo", "Backing") : Loc.T("Ripa", "Slat"), SharedParams.Grupo);
                });
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, "wall.slats", o, c, Loc.T("Ripado", "Slats"));
            ctx.Report.Created(sink.Created.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });
        var t = new TableData(Loc.T("Ripado", "Slats"));
        t.AddColumn(Loc.T("Peça", "Piece")).AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer)
         .AddColumn(Loc.T("Comprimento total (m)", "Total length (m)"), ColumnKind.Number).AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number);
        var slats = rects.Where(x => !x.Backing).ToList();
        var backs = rects.Where(x => x.Backing).ToList();
        t.AddRow(Loc.T("Ripas", "Slats"), slats.Count, U.ToM(slats.Sum(x => Math.Max(x.R.Width, x.R.Height))), U.SqFtToM2(slats.Sum(x => x.R.Area)));
        if (backs.Count > 0) t.AddRow(Loc.T("Fundo", "Backing"), backs.Count, U.ToM(backs.Sum(x => x.R.Width)), U.SqFtToM2(backs.Sum(x => x.R.Area)));
        ctx.Report.Tables.Add(t);
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<SlatOptions>(optionsJson) ?? new SlatOptions();
        var c = Json.Deserialize<PlanarContext>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "wall.slats");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}

// ============================================================ Boiserie

public sealed class BoiserieOptions
{
    [Opt("Colunas de quadros", "Frame columns", Min = 1, Max = 60, Group = "Quadros|Frames")]
    public int Columns { get; set; } = 3;

    [Opt("Linhas de quadros", "Frame rows", Min = 1, Max = 20, Group = "Quadros|Frames")]
    public int Rows { get; set; } = 2;

    [Opt("Altura do primeiro quadro (0 = iguais)", "First frame height (0 = equal)", Kind = OptKind.Length, Min = 0, Group = "Quadros|Frames", VisibleIf = "!ChairRail")]
    public double LowerHeight { get; set; }

    [Opt("Espaço entre quadros", "Space between frames", Kind = OptKind.Length, Min = 0, Group = "Quadros|Frames")]
    public double Spacing { get; set; } = U.Mm(120);

    [Opt("Margem lateral", "Side margin", Kind = OptKind.Length, Min = 0, Group = "Quadros|Frames")]
    public double SideMargin { get; set; } = U.Mm(150);

    [Opt("Margem inferior (acima do rodapé)", "Bottom margin (above baseboard)", Kind = OptKind.Length, Min = 0, Group = "Quadros|Frames")]
    public double BottomMargin { get; set; } = U.Mm(200);

    [Opt("Margem superior", "Top margin", Kind = OptKind.Length, Min = 0, Group = "Quadros|Frames")]
    public double TopMargin { get; set; } = U.Mm(250);

    [Opt("Largura da moldura", "Molding width", Kind = OptKind.Length, Min = 0.001, Group = "Moldura|Molding")]
    public double MoldWidth { get; set; } = U.Mm(25);

    [Opt("Projeção da moldura", "Molding projection", Kind = OptKind.Length, Min = 0.001, Group = "Moldura|Molding")]
    public double MoldDepth { get; set; } = U.Mm(15);

    [Opt("Roda-meio (moldura horizontal contínua)", "Chair rail (continuous horizontal molding)", Group = "Roda-meio|Chair rail")]
    public bool ChairRail { get; set; }

    [Opt("Altura do roda-meio (eixo)", "Chair rail height (axis)", Kind = OptKind.Length, Min = 0, Group = "Roda-meio|Chair rail", VisibleIf = "ChairRail")]
    public double ChairRailHeight { get; set; } = U.Mm(900);

    [Opt("Largura do roda-meio", "Chair rail width", Kind = OptKind.Length, Min = 0.001, Group = "Roda-meio|Chair rail", VisibleIf = "ChairRail")]
    public double ChairRailWidth { get; set; } = U.Mm(50);

    [Opt("Material", "Material", Kind = OptKind.Material, Group = "Saída|Output")]
    public string Material { get; set; } = "";

    [Opt("Saída", "Output", Group = "Saída|Output")]
    public GenOutput Output { get; set; } = GenOutput.Parametric;

    [Opt("Família do usuário", "User family", Kind = OptKind.FamilyType, Group = "Saída|Output", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("wall.boiserie", Modules.Interiors, "Boiserie / molduras", "Boiserie / moldings", Order = 63, Group = "Paredes|Walls",
    DescPt = "Gera quadros de moldura (boiserie) em grade sobre uma face, com margens, roda-meio e desvio automático de portas e janelas.",
    DescEn = "Generates molding frames (boiserie) in a grid on a face, with margins, chair rail and automatic avoidance of doors and windows.",
    Keywords = "boiserie moldura quadros roda-meio parede classica molding frames chair rail")]
public sealed class BoiserieCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var c = FaceBoxes.AcquirePlanar(ctx);
        var o = ctx.LoadOptions<BoiserieOptions>();
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    /// <summary>Retângulos das molduras (peças) e dos quadros (para pré-visualização).</summary>
    internal static (List<Rect2> Pieces, List<Rect2> Frames, int Skipped) Compute(BoiserieOptions o, Region2 region)
    {
        var pieces = new List<Rect2>();
        var frames = new List<Rect2>();
        var skipped = 0;
        var b = region.Bounds;
        var x0 = b.MinX + o.SideMargin;
        var x1 = b.MaxX - o.SideMargin;
        var y0 = b.MinY + o.BottomMargin;
        var y1 = b.MaxY - o.TopMargin;
        if (x1 - x0 < 2 * o.MoldWidth || y1 - y0 < 2 * o.MoldWidth) return (pieces, frames, 0);
        var zones = new List<(double Y0, double Y1, int Rows, double First)>();
        if (o.ChairRail)
        {
            var yr = b.MinY + o.ChairRailHeight;
            var lo = yr - o.ChairRailWidth / 2 - o.Spacing;
            var hi = yr + o.ChairRailWidth / 2 + o.Spacing;
            if (lo - y0 > 2 * o.MoldWidth) zones.Add((y0, lo, 1, 0));
            if (y1 - hi > 2 * o.MoldWidth) zones.Add((hi, y1, o.Rows, 0));
            var rail = new Segment2(new Vec2(b.MinX, yr), new Vec2(b.MaxX, yr));
            foreach (var p in region.ClipSegment(rail))
                if (p.Length > U.Mm(30))
                    pieces.Add(new Rect2(Math.Min(p.A.X, p.B.X), yr - o.ChairRailWidth / 2, Math.Max(p.A.X, p.B.X), yr + o.ChairRailWidth / 2));
        }
        else
        {
            zones.Add((y0, y1, o.Rows, o.LowerHeight));
        }
        var colW = (x1 - x0 - (o.Columns - 1) * o.Spacing) / o.Columns;
        if (colW < 2 * o.MoldWidth) return (pieces, frames, 0);
        foreach (var (zy0, zy1, rows, first) in zones)
        {
            var avail = zy1 - zy0 - (rows - 1) * o.Spacing;
            var heights = new List<double>();
            if (first > 0 && rows > 1 && first < avail)
            {
                heights.Add(first);
                for (var r = 1; r < rows; r++) heights.Add((avail - first) / (rows - 1));
            }
            else
            {
                for (var r = 0; r < rows; r++) heights.Add(avail / rows);
            }
            var y = zy0;
            foreach (var h in heights)
            {
                for (var col = 0; col < o.Columns; col++)
                {
                    var fx0 = x0 + col * (colW + o.Spacing);
                    var f = new Rect2(fx0, y, fx0 + colW, y + h);
                    if (h < 2 * o.MoldWidth) continue;
                    var blocked = region.Holes.Any(hole => Overlaps(hole.Bounds, f, U.Mm(30))) || !region.Contains(f.Center);
                    if (blocked)
                    {
                        skipped++;
                        continue;
                    }
                    frames.Add(f);
                    var mw = o.MoldWidth;
                    pieces.Add(new Rect2(f.MinX, f.MinY, f.MaxX, f.MinY + mw));
                    pieces.Add(new Rect2(f.MinX, f.MaxY - mw, f.MaxX, f.MaxY));
                    pieces.Add(new Rect2(f.MinX, f.MinY + mw, f.MinX + mw, f.MaxY - mw));
                    pieces.Add(new Rect2(f.MaxX - mw, f.MinY + mw, f.MaxX, f.MaxY - mw));
                }
                y += h + o.Spacing;
            }
        }
        return (pieces, frames, skipped);
    }

    private static bool Overlaps(Rect2 a, Rect2 b, double margin) =>
        a.MinX < b.MaxX + margin && a.MaxX > b.MinX - margin && a.MinY < b.MaxY + margin && a.MaxY > b.MinY - margin;

    private static PreviewModel Preview(BoiserieOptions o, PlanarContext c)
    {
        var reg = c.GetRegion();
        var (pieces, frames, skipped) = Compute(o, reg);
        var pm = new PreviewModel();
        pm.Region(reg);
        foreach (var f in frames) pm.Outline(f.ToPolygon(), "#B9A48F", 0.6, dashed: true);
        foreach (var p in pieces) pm.Polygon(p.ToPolygon(), "#D9C2AE", "#6B4A33", 0.5);
        var f3 = c.GetFrame();
        var sc = pm.S3;
        sc.Face(f3, reg.Outer, "#ECE7DF", 0.6);
        foreach (var p in pieces.Take(8000)) sc.Box(FaceBoxes.Box(f3, p, 0, o.MoldDepth), "#C9B79E");
        pm.Info = Loc.F("{0} quadros · {1} peças de moldura{2}", "{0} frames · {1} molding pieces{2}", frames.Count, pieces.Count,
            skipped > 0 ? Loc.F(" · {0} quadros omitidos (vãos)", " · {0} frames skipped (openings)", skipped) : "");
        return pm;
    }

    internal static void Generate(ToolContext ctx, BoiserieOptions o, PlanarContext c, string systemId)
    {
        var doc = ctx.Doc;
        var frame = c.GetFrame();
        var (pieces, frames, skipped) = Compute(o, c.GetRegion());
        ctx.CheckCount(pieces.Count, "peças", "pieces");
        var sink = new BoxSink(ctx, o.Output, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Boiserie" };
        sink.Prepare();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var i = 0;
            foreach (var p in pieces)
            {
                var idx = i;
                sink.Add(FaceBoxes.Box(frame, p, 0, o.MoldDepth), string.IsNullOrWhiteSpace(o.Material) ? null : o.Material, i++,
                    e => RevitGeo.Set(e, $"M{idx + 1:000}", SharedParams.Codigo));
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, "wall.boiserie", o, c, "Boiserie");
            ctx.Report.Created(sink.Created.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });
        if (skipped > 0) ctx.Report.Info(Loc.F("{0} quadros omitidos por coincidirem com vãos.", "{0} frames skipped because they overlap openings.", skipped));
        var t = new TableData(Loc.T("Molduras", "Moldings"));
        t.AddColumn(Loc.T("Item", "Item")).AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer).AddColumn(Loc.T("Comprimento total (m)", "Total length (m)"), ColumnKind.Number);
        t.AddRow(Loc.T("Quadros", "Frames"), frames.Count, U.ToM(frames.Sum(f => 2 * (f.Width + f.Height))));
        t.AddRow(Loc.T("Peças de moldura", "Molding pieces"), pieces.Count, U.ToM(pieces.Sum(p => Math.Max(p.Width, p.Height))));
        ctx.Report.Tables.Add(t);
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<BoiserieOptions>(optionsJson) ?? new BoiserieOptions();
        var c = Json.Deserialize<PlanarContext>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "wall.boiserie");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}
