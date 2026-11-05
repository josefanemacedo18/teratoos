using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

/// <summary>Região plana serializável num sistema de coordenadas 3D (para regenerar sistemas).</summary>
public sealed class PlanarContext
{
    public double[] Frame { get; set; } = new double[12];
    public List<double[]> Outer { get; set; } = new();
    public List<List<double[]>> Holes { get; set; } = new();
    public long HostId { get; set; } = -1;
    public string? FaceReference { get; set; }
    public bool Curved { get; set; }

    public static PlanarContext From(Frame3 f, Region2 r, long hostId = -1) => new()
    {
        Frame = new[] { f.Origin.X, f.Origin.Y, f.Origin.Z, f.XAxis.X, f.XAxis.Y, f.XAxis.Z, f.YAxis.X, f.YAxis.Y, f.YAxis.Z, f.ZAxis.X, f.ZAxis.Y, f.ZAxis.Z },
        Outer = r.Outer.Select(p => new[] { p.X, p.Y }).ToList(),
        Holes = r.Holes.Select(h => h.Select(p => new[] { p.X, p.Y }).ToList()).ToList(),
        HostId = hostId,
    };

    public Frame3 GetFrame() => new(
        new Vec3(Frame[0], Frame[1], Frame[2]),
        new Vec3(Frame[3], Frame[4], Frame[5]),
        new Vec3(Frame[6], Frame[7], Frame[8]),
        new Vec3(Frame[9], Frame[10], Frame[11]));

    public Region2 GetRegion() => new(
        new Polygon2(Outer.Select(p => new Vec2(p[0], p[1]))),
        Holes.Select(h => new Polygon2(h.Select(p => new Vec2(p[0], p[1])))));
}

/// <summary>Superfície curva: avalia pontos e normais a partir de coordenadas "desenroladas" (comprimentos).</summary>
public sealed class CurvedSurface
{
    private readonly Face _face;
    private readonly Transform _t;
    private readonly BoundingBoxUV _uv;
    public double LengthU { get; }
    public double LengthV { get; }

    public CurvedSurface(Face face, Transform t)
    {
        _face = face;
        _t = t;
        _uv = face.GetBoundingBox();
        LengthU = Measure(true);
        LengthV = Measure(false);
    }

    private double Measure(bool alongU)
    {
        const int n = 24;
        double len = 0;
        XYZ? prev = null;
        for (var i = 0; i <= n; i++)
        {
            var a = (double)i / n;
            var uv = alongU
                ? new UV(_uv.Min.U + (_uv.Max.U - _uv.Min.U) * a, (_uv.Min.V + _uv.Max.V) / 2)
                : new UV((_uv.Min.U + _uv.Max.U) / 2, _uv.Min.V + (_uv.Max.V - _uv.Min.V) * a);
            var p = _face.Evaluate(uv);
            if (prev != null) len += p.DistanceTo(prev);
            prev = p;
        }
        return Math.Max(len, 1e-6);
    }

    public UV ToUV(Vec2 p) => new(
        _uv.Min.U + (_uv.Max.U - _uv.Min.U) * (p.X / LengthU),
        _uv.Min.V + (_uv.Max.V - _uv.Min.V) * (p.Y / LengthV));

    public bool IsInside(Vec2 p) => _face.IsInside(ToUV(p));

    /// <summary>Sistema local no ponto: X = tangente em U, Z = normal (para fora), Y = Z × X.</summary>
    public Frame3 FrameAt(Vec2 p)
    {
        var uv = ToUV(p);
        var pt = _t.OfPoint(_face.Evaluate(uv));
        var d = _face.ComputeDerivatives(uv);
        var tu = _t.OfVector(d.BasisX).Normalize();
        var n = _t.OfVector(_face.ComputeNormal(uv)).Normalize();
        return Frame3.FromXAndNormal(pt.ToVec3(), tu.ToVec3(), n.ToVec3());
    }

    public Region2 Domain => new(Polygon2.Rectangle(Vec2.Zero, LengthU, LengthV));
}

public enum SurfaceHost
{
    [Text("Face plana (parede, piso, qualquer elemento)", "Planar face (wall, floor, any element)")] PlanarFace,
    [Text("Face curva compatível", "Compatible curved face")] CurvedFace,
    [Text("Retângulo por dois pontos (horizontal)", "Rectangle by two points (horizontal)")] Rectangle,
    [Text("Ambiente (contorno)", "Room (outline)")] Room,
}

public static class GenHost
{
    /// <summary>Face plana alinhada: X horizontal (quando possível), Z = normal da face.</summary>
    public static (Region2 Region, Frame3 Frame) PlanarFaceRegion(PlanarFace face, Transform t)
    {
        var n = t.OfVector(face.FaceNormal).Normalize().ToVec3();
        var origin = t.OfPoint(face.Origin).ToVec3();
        var x = Math.Abs(n.Z) < 0.99 ? Vec3.UnitZ.Cross(n).Normalized() : t.OfVector(face.XVector).Normalize().ToVec3();
        var frame = Frame3.FromXAndNormal(origin, x, n);
        var loops = new List<Polygon2>();
        foreach (var loop in face.GetEdgesAsCurveLoops())
        {
            var pts = new List<Vec2>();
            foreach (var c in loop)
                foreach (var p in c.Tessellate())
                {
                    var l = frame.ToLocal(t.OfPoint(p).ToVec3());
                    var v = new Vec2(l.X, l.Y);
                    if (pts.Count == 0 || !pts[^1].AlmostEquals(v, 1e-7)) pts.Add(v);
                }
            loops.Add(new Polygon2(pts).Simplify(1e-7));
        }
        var region = Region2.FromLoops(loops).OrderByDescending(r => r.Area).FirstOrDefault()
                     ?? throw new ToolException(Loc.T("Não foi possível ler o contorno da face.", "Could not read the face outline."));
        // Desloca a origem para o canto inferior esquerdo da região (coordenadas positivas).
        var b = region.Bounds;
        var newOrigin = frame.ToWorld(b.MinX, b.MinY, 0);
        var shifted = region.Transform(p => p - b.Min);
        return (shifted, frame with { Origin = newOrigin });
    }

    public static Transform TransformOf(Element e) => e is FamilyInstance fi ? fi.GetTotalTransform() : Transform.Identity;

    /// <summary>Coleta a superfície de aplicação conforme o modo escolhido.</summary>
    public static PlanarContext Acquire(ToolContext ctx, SurfaceHost host)
    {
        switch (host)
        {
            case SurfaceHost.Rectangle:
            {
                var a = ctx.PickPoint("Primeiro canto do retângulo", "First rectangle corner");
                var b = ctx.PickPoint("Canto oposto", "Opposite corner");
                var r = Rect2.FromPoints(new[] { a.ToVec2(), b.ToVec2() });
                if (r.Width < 1e-3 || r.Height < 1e-3) ctx.Cancel();
                var frame = new Frame3(new Vec3(r.MinX, r.MinY, a.Z), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
                return PlanarContext.From(frame, new Region2(Polygon2.Rectangle(Vec2.Zero, r.Width, r.Height)));
            }
            case SurfaceHost.Room:
            {
                var room = ctx.PickElement("Escolha o ambiente", "Pick the room", e => e is Room rr && rr.Area > 0) as Room;
                var reg = RevitGeo.RoomRegion(room!) ?? throw new ToolException(Loc.T("Ambiente sem contorno.", "Room without boundary."));
                var z = room!.Level?.Elevation ?? 0;
                var b = reg.Bounds;
                var frame = new Frame3(new Vec3(b.MinX, b.MinY, z), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
                return PlanarContext.From(frame, reg.Transform(p => p - b.Min), room.Id.Value);
            }
            default:
            {
                var r = ctx.PickFace(host == SurfaceHost.CurvedFace ? "Escolha a face curva" : "Escolha a face plana",
                    host == SurfaceHost.CurvedFace ? "Pick the curved face" : "Pick the planar face");
                var el = ctx.Doc.GetElement(r);
                var face = el.GetGeometryObjectFromReference(r) as Face ?? throw new ToolException(Loc.T("Face inválida.", "Invalid face."));
                var t = TransformOf(el);
                if (face is PlanarFace pf && host == SurfaceHost.PlanarFace)
                {
                    var (reg, frame) = PlanarFaceRegion(pf, t);
                    var c = PlanarContext.From(frame, reg, el.Id.Value);
                    c.FaceReference = r.ConvertToStableRepresentation(ctx.Doc);
                    return c;
                }
                if (face is PlanarFace && host == SurfaceHost.CurvedFace)
                    Msg.Info(Loc.T("A face escolhida é plana; ela será tratada como superfície genérica.", "The picked face is planar; it will be treated as a generic surface."));
                return new PlanarContext { Curved = true, FaceReference = r.ConvertToStableRepresentation(ctx.Doc), HostId = el.Id.Value };
            }
        }
    }

    public static CurvedSurface? Curved(Document doc, PlanarContext c)
    {
        if (!c.Curved || c.FaceReference == null) return null;
        var r = Reference.ParseFromStableRepresentation(doc, c.FaceReference);
        var el = doc.GetElement(r) ?? throw new ToolException(Loc.T("O elemento hospedeiro não existe mais.", "The host element no longer exists."));
        var face = el.GetGeometryObjectFromReference(r) as Face ?? throw new ToolException(Loc.T("A face hospedeira não existe mais.", "The host face no longer exists."));
        return new CurvedSurface(face, TransformOf(el));
    }

    /// <summary>
    /// Fachada de uma parede em coordenadas (s, z): s = distância ao longo da linha da parede,
    /// com furos nas portas, janelas e aberturas.
    /// </summary>
    public static (PolylinePath Path, Region2 Facade, double Z0, double Z1, Vec2 Exterior) WallFacade(Wall wall)
    {
        var lc = wall.Location as LocationCurve ?? throw new ToolException(Loc.T("Parede sem linha de locação.", "Wall without location line."));
        var pts = RevitGeo.TessellateXY(lc.Curve);
        var path = new PolylinePath(pts);
        var b = RevitGeo.Box(wall) ?? throw new ToolException(Loc.T("Parede sem geometria.", "Wall without geometry."));
        var z0 = b.Min.Z;
        var z1 = b.Max.Z;
        var holes = new List<Polygon2>();
        foreach (var id in wall.FindInserts(true, false, false, false))
        {
            var ins = wall.Document.GetElement(id);
            var ib = ins == null ? null : RevitGeo.Box(ins);
            if (ib == null) continue;
            var s0 = double.MaxValue;
            var s1 = double.MinValue;
            foreach (var c in new[] { ib.Min, ib.Max, new XYZ(ib.Min.X, ib.Max.Y, 0), new XYZ(ib.Max.X, ib.Min.Y, 0) })
            {
                var s = ProjectOnPath(path, c.ToVec2());
                s0 = Math.Min(s0, s);
                s1 = Math.Max(s1, s);
            }
            if (s1 - s0 > 1e-3 && ib.Max.Z - ib.Min.Z > 1e-3)
                holes.Add(Polygon2.Rectangle(new Vec2(s0, ib.Min.Z), s1 - s0, ib.Max.Z - ib.Min.Z));
        }
        var facade = new Region2(Polygon2.Rectangle(new Vec2(0, z0), path.Length, z1 - z0), holes);
        return (path, facade, z0, z1, wall.Orientation.ToVec2());
    }

    public static double ProjectOnPath(PolylinePath path, Vec2 p)
    {
        var best = double.MaxValue;
        var bestS = 0.0;
        var acc = 0.0;
        var pts = path.Points;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var seg = new Segment2(pts[i], pts[i + 1]);
            var t = Math.Clamp(seg.ProjectParameter(p), 0, 1);
            var d = seg.PointAt(t).DistanceTo(p);
            if (d < best)
            {
                best = d;
                bestS = acc + t * seg.Length;
            }
            acc += seg.Length;
        }
        return bestS;
    }

    /// <summary>Caminho (polilinha) a partir de linhas de modelo selecionadas, encadeadas em ordem.</summary>
    public static PolylinePath PathFromCurves(IEnumerable<Curve> curves)
    {
        var list = curves.ToList();
        var chain = Shapes.ChainLoops(list, 0.01).FirstOrDefault();
        var ordered = chain ?? OrderOpenChain(list);
        var pts = new List<Vec2>();
        foreach (var c in ordered)
            foreach (var p in RevitGeo.TessellateXY(c))
                if (pts.Count == 0 || !pts[^1].AlmostEquals(p, 1e-6)) pts.Add(p);
        return new PolylinePath(pts);
    }

    public static List<Curve> OrderOpenChain(List<Curve> curves)
    {
        if (curves.Count <= 1) return curves;
        var pool = new List<Curve>(curves);
        var chain = new List<Curve> { pool[0] };
        pool.RemoveAt(0);
        var grown = true;
        while (pool.Count > 0 && grown)
        {
            grown = false;
            var end = chain[^1].GetEndPoint(1);
            var start = chain[0].GetEndPoint(0);
            for (var i = 0; i < pool.Count; i++)
            {
                var c = pool[i];
                if (c.GetEndPoint(0).DistanceTo(end) < 0.01) { chain.Add(c); }
                else if (c.GetEndPoint(1).DistanceTo(end) < 0.01) { chain.Add(c.CreateReversed()); }
                else if (c.GetEndPoint(1).DistanceTo(start) < 0.01) { chain.Insert(0, c); }
                else if (c.GetEndPoint(0).DistanceTo(start) < 0.01) { chain.Insert(0, c.CreateReversed()); }
                else continue;
                pool.RemoveAt(i);
                grown = true;
                break;
            }
        }
        return chain;
    }

    /// <summary>Converte uma célula 2D (no sistema da superfície) em caixa 3D orientada.</summary>
    public static OrientedBox CellBox(Frame3 f, Vec2 center, double rotation, double w, double h, double depth, double offset)
    {
        var c = Math.Cos(rotation);
        var s = Math.Sin(rotation);
        var x = f.XAxis * c + f.YAxis * s;
        var y = f.YAxis * c - f.XAxis * s;
        var p = f.ToWorld(center.X, center.Y, offset);
        var origin = p - x * (w / 2) - y * (h / 2);
        return new OrientedBox(new Frame3(origin, x, y, f.ZAxis), w, h, depth);
    }

    public static string Gradient(double v) =>
        Core.Colors.RgbColor.Lerp(new Core.Colors.RgbColor(0xE9, 0xD5, 0xC1), new Core.Colors.RgbColor(0xB5, 0x65, 0x2B), v).Hex;
}
