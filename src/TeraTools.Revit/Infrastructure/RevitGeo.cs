using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Geometry;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Conversões e utilitários geométricos entre o Revit e o núcleo do Téra Tools.</summary>
public static class RevitGeo
{
    // ------------------------------------------------------------ Conversões

    public static XYZ ToXYZ(this Vec3 v) => new(v.X, v.Y, v.Z);
    public static XYZ ToXYZ(this Vec2 v, double z = 0) => new(v.X, v.Y, z);
    public static Vec3 ToVec3(this XYZ p) => new(p.X, p.Y, p.Z);
    public static Vec2 ToVec2(this XYZ p) => new(p.X, p.Y);
    public static ElementId ToId(this long v) => new(v);
    public static long IdValue(this Element e) => e.Id.Value;

    public static Transform ToTransform(this Frame3 f)
    {
        var t = Transform.Identity;
        t.Origin = f.Origin.ToXYZ();
        t.BasisX = f.XAxis.ToXYZ();
        t.BasisY = f.YAxis.ToXYZ();
        t.BasisZ = f.ZAxis.ToXYZ();
        return t;
    }

    // ------------------------------------------------------------ Posição

    public static BoundingBoxXYZ? Box(Element e, View? view = null)
    {
        try
        {
            return e.get_BoundingBox(view) ?? e.get_BoundingBox(null);
        }
        catch
        {
            return null;
        }
    }

    public static XYZ? BoxCenter(Element e)
    {
        var b = Box(e);
        return b == null ? null : (b.Min + b.Max) / 2;
    }

    /// <summary>Ponto representativo: ponto de inserção, meio da linha de locação ou centro da caixa.</summary>
    public static XYZ? Anchor(Element e)
    {
        switch (e.Location)
        {
            case LocationPoint lp:
                return lp.Point;
            case LocationCurve lc:
                return lc.Curve.Evaluate(0.5, true);
            default:
                return BoxCenter(e);
        }
    }

    public static double Rotation(Element e) => e.Location is LocationPoint lp ? lp.Rotation : 0;

    // ------------------------------------------------------------ Curvas e polígonos

    public static List<Vec2> TessellateXY(Curve c)
    {
        IList<XYZ> pts;
        try
        {
            pts = c.Tessellate();
        }
        catch
        {
            pts = new List<XYZ> { c.GetEndPoint(0), c.GetEndPoint(1) };
        }
        return pts.Select(p => p.ToVec2()).ToList();
    }

    /// <summary>
    /// Desloca a curva do contorno do ambiente para DENTRO dele (rodapés, revestimentos). Testa os dois lados:
    /// o lado do Curve.CreateOffset depende do tipo de curva (linha: à direita; arco: pelo eixo), então
    /// escolher o sinal "na teoria" punha o elemento dentro da parede e ele ficava sem volume visível.
    /// </summary>
    public static Curve? OffsetIntoRoom(Autodesk.Revit.DB.Architecture.Room room, Curve c, double d, double probeZ)
    {
        if (Math.Abs(d) < 1e-9) return c;
        try
        {
            var mid = c.Evaluate(0.5, true);
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                Curve oc;
                try
                {
                    oc = c.CreateOffset(sign * d, XYZ.BasisZ);
                }
                catch
                {
                    continue;
                }
                var om = oc.Evaluate(0.5, true);
                var dir = new XYZ(om.X - mid.X, om.Y - mid.Y, 0);
                if (dir.GetLength() < 1e-9) continue;
                // sonda um pouco além do deslocamento, na direção dele
                var probe = mid + dir.Normalize() * Math.Max(d * 2, 0.15);
                if (room.IsPointInRoom(new XYZ(probe.X, probe.Y, probeZ))) return oc;
            }
        }
        catch
        {
            // cai no nulo
        }
        return null;
    }

    public static Polygon2 LoopToPolygon(IEnumerable<Curve> loop)
    {
        var pts = new List<Vec2>();
        foreach (var c in loop)
        {
            var seg = TessellateXY(c);
            foreach (var p in seg)
                if (pts.Count == 0 || !pts[^1].AlmostEquals(p, 1e-7))
                    pts.Add(p);
        }
        return new Polygon2(pts).Simplify(1e-7);
    }

    /// <summary>Polígono → CurveLoop horizontal na cota z (descarta segmentos curtos demais para o Revit).</summary>
    public static CurveLoop PolygonToLoop(Polygon2 poly, double z, bool counterClockwise = true)
    {
        var p = counterClockwise ? poly.EnsureCounterClockwise() : poly.EnsureCounterClockwise().Reversed();
        var pts = new List<Vec2>();
        foreach (var v in p)
            if (pts.Count == 0 || pts[^1].DistanceTo(v) > Tolerance.ShortCurve * 1.5)
                pts.Add(v);
        if (pts.Count > 2 && pts[^1].DistanceTo(pts[0]) <= Tolerance.ShortCurve * 1.5) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) throw new ToolException(Core.Localization.Loc.T("Contorno pequeno demais.", "Outline too small."));
        var loop = new CurveLoop();
        for (var i = 0; i < pts.Count; i++)
            loop.Append(Line.CreateBound(pts[i].ToXYZ(z), pts[(i + 1) % pts.Count].ToXYZ(z)));
        return loop;
    }

    public static List<CurveLoop> RegionToLoops(Region2 region, double z)
    {
        var list = new List<CurveLoop> { PolygonToLoop(region.Outer, z) };
        foreach (var h in region.Holes)
        {
            try
            {
                list.Add(PolygonToLoop(h, z, counterClockwise: false));
            }
            catch (ToolException)
            {
                // furo pequeno demais: ignora
            }
        }
        return list;
    }

    /// <summary>Contornos do ambiente (face de acabamento) como regiões 2D.</summary>
    public static List<Region2> RoomRegions(SpatialElement room, SpatialElementBoundaryLocation location = SpatialElementBoundaryLocation.Finish)
    {
        var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = location };
        var segs = room.GetBoundarySegments(opts);
        if (segs == null || segs.Count == 0) return new List<Region2>();
        var loops = segs.Select(loop => LoopToPolygon(loop.Select(s => s.GetCurve()))).Where(p => !p.IsEmpty);
        return Region2.FromLoops(loops);
    }

    /// <summary>Região principal (maior) do ambiente.</summary>
    public static Region2? RoomRegion(SpatialElement room, SpatialElementBoundaryLocation location = SpatialElementBoundaryLocation.Finish) =>
        RoomRegions(room, location).OrderByDescending(r => r.Area).FirstOrDefault();

    /// <summary>Contorno de piso/forro a partir do esboço (Sketch).</summary>
    public static Region2? SketchRegion(Document doc, ElementId sketchId)
    {
        if (sketchId == ElementId.InvalidElementId) return null;
        if (doc.GetElement(sketchId) is not Sketch sk) return null;
        var loops = new List<Polygon2>();
        foreach (CurveArray arr in sk.Profile)
        {
            var curves = new List<Curve>();
            foreach (Curve c in arr) curves.Add(c);
            loops.Add(LoopToPolygon(curves));
        }
        return Region2.FromLoops(loops).OrderByDescending(r => r.Area).FirstOrDefault();
    }

    public static Region2? HostRegion(Element e)
    {
        var doc = e.Document;
        ElementId sketch = e switch
        {
            Floor f => f.SketchId,
            Ceiling c => c.SketchId,
            _ => ElementId.InvalidElementId,
        };
        var r = SketchRegion(doc, sketch);
        if (r != null) return r;
        var top = TopFace(e);
        if (top == null) return null;
        var loops = top.GetEdgesAsCurveLoops().Select(l => LoopToPolygon(l));
        return Region2.FromLoops(loops).OrderByDescending(x => x.Area).FirstOrDefault();
    }

    /// <summary>Região de uma face plana no sistema local da face (origem, X e Y da face).</summary>
    public static (Region2 Region, Frame3 Frame) FaceRegion(PlanarFace face)
    {
        var frame = new Frame3(face.Origin.ToVec3(), face.XVector.ToVec3().Normalized(), face.YVector.ToVec3().Normalized(), face.FaceNormal.ToVec3().Normalized());
        var loops = new List<Polygon2>();
        foreach (var loop in face.GetEdgesAsCurveLoops())
        {
            var pts = new List<Vec2>();
            foreach (var c in loop)
            {
                foreach (var p in c.Tessellate())
                {
                    var l = frame.ToLocal(p.ToVec3());
                    var v = new Vec2(l.X, l.Y);
                    if (pts.Count == 0 || !pts[^1].AlmostEquals(v, 1e-7)) pts.Add(v);
                }
            }
            loops.Add(new Polygon2(pts).Simplify(1e-7));
        }
        var region = Region2.FromLoops(loops).OrderByDescending(r => r.Area).FirstOrDefault()
                     ?? throw new ToolException(Core.Localization.Loc.T("Não foi possível ler o contorno da face.", "Could not read the face outline."));
        return (region, frame);
    }

    // ------------------------------------------------------------ Geometria

    public static Options GeometryOptions(bool references = false, View? view = null)
    {
        var o = new Options { ComputeReferences = references, IncludeNonVisibleObjects = false };
        if (view != null) o.View = view;
        else o.DetailLevel = ViewDetailLevel.Fine;
        return o;
    }

    public static List<Solid> Solids(Element e, Options? opt = null)
    {
        var list = new List<Solid>();
        GeometryElement? ge;
        try
        {
            ge = e.get_Geometry(opt ?? GeometryOptions());
        }
        catch
        {
            return list;
        }
        if (ge == null) return list;
        Collect(ge, list);
        return list;

        static void Collect(GeometryElement g, List<Solid> acc)
        {
            foreach (var obj in g)
            {
                switch (obj)
                {
                    case Solid s when s.Volume > 1e-9 && s.Faces.Size > 0:
                        acc.Add(s);
                        break;
                    case GeometryInstance gi:
                        var inst = gi.GetInstanceGeometry();
                        if (inst != null) Collect(inst, acc);
                        break;
                }
            }
        }
    }

    public static PlanarFace? TopFace(Element e)
    {
        PlanarFace? best = null;
        foreach (var s in Solids(e, GeometryOptions(references: true)))
            foreach (Face f in s.Faces)
                if (f is PlanarFace pf && pf.FaceNormal.Z > 0.99 && (best == null || pf.Origin.Z > best.Origin.Z || pf.Area > best.Area))
                    best = pf;
        return best;
    }

    public static Solid? UnionSolid(Element e)
    {
        Solid? result = null;
        foreach (var s in Solids(e))
        {
            try
            {
                result = result == null ? SolidUtils.Clone(s) : BooleanOperationsUtils.ExecuteBooleanOperation(result, s, BooleanOperationsType.Union);
            }
            catch
            {
                // sólidos que não unem: mantém o que já tem
            }
        }
        return result;
    }

    // ------------------------------------------------------------ Parâmetros

    public static Parameter? Param(Element e, params string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p != null) return p;
        }
        return null;
    }

    public static bool Set(Element e, double value, params string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p is { IsReadOnly: false, StorageType: StorageType.Double })
            {
                p.Set(value);
                return true;
            }
        }
        return false;
    }

    public static bool Set(Element e, string value, params string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p is { IsReadOnly: false, StorageType: StorageType.String })
            {
                p.Set(value ?? "");
                return true;
            }
        }
        return false;
    }

    public static bool Set(Element e, int value, params string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p is { IsReadOnly: false, StorageType: StorageType.Integer })
            {
                p.Set(value);
                return true;
            }
        }
        return false;
    }

    public static bool Set(Element e, ElementId value, params string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p is { IsReadOnly: false, StorageType: StorageType.ElementId })
            {
                p.Set(value);
                return true;
            }
        }
        return false;
    }

    public static bool Set(Element e, BuiltInParameter bip, double value)
    {
        var p = e.get_Parameter(bip);
        if (p is not { IsReadOnly: false, StorageType: StorageType.Double }) return false;
        p.Set(value);
        return true;
    }

    public static bool Set(Element e, BuiltInParameter bip, string value)
    {
        var p = e.get_Parameter(bip);
        if (p is not { IsReadOnly: false, StorageType: StorageType.String }) return false;
        p.Set(value);
        return true;
    }

    public static bool Set(Element e, BuiltInParameter bip, int value)
    {
        var p = e.get_Parameter(bip);
        if (p is not { IsReadOnly: false, StorageType: StorageType.Integer }) return false;
        p.Set(value);
        return true;
    }

    public static bool Set(Element e, BuiltInParameter bip, ElementId value)
    {
        var p = e.get_Parameter(bip);
        if (p is not { IsReadOnly: false, StorageType: StorageType.ElementId }) return false;
        p.Set(value);
        return true;
    }

    public static string Text(Element e, BuiltInParameter bip)
    {
        var p = e.get_Parameter(bip);
        if (p == null) return "";
        return p.StorageType == StorageType.String ? p.AsString() ?? "" : p.AsValueString() ?? "";
    }

    /// <summary>Valor do parâmetro como texto de exibição (qualquer tipo).</summary>
    public static string Display(Parameter? p)
    {
        if (p == null || !p.HasValue) return "";
        try
        {
            return p.StorageType switch
            {
                StorageType.String => p.AsString() ?? "",
                StorageType.ElementId => p.AsElementId() == ElementId.InvalidElementId ? "" : p.Element?.Document.GetElement(p.AsElementId())?.Name ?? p.AsValueString() ?? "",
                _ => p.AsValueString() ?? "",
            };
        }
        catch
        {
            return "";
        }
    }

    // ------------------------------------------------------------ Níveis

    public static Level? LevelOf(Element e)
    {
        var doc = e.Document;
        if (e.LevelId != ElementId.InvalidElementId && doc.GetElement(e.LevelId) is Level l) return l;
        foreach (var bip in new[] { BuiltInParameter.FAMILY_LEVEL_PARAM, BuiltInParameter.SCHEDULE_LEVEL_PARAM, BuiltInParameter.LEVEL_PARAM, BuiltInParameter.WALL_BASE_CONSTRAINT })
        {
            var p = e.get_Parameter(bip);
            if (p != null && p.StorageType == StorageType.ElementId && doc.GetElement(p.AsElementId()) is Level lv) return lv;
        }
        var c = Anchor(e);
        return c == null ? null : Lookup.LevelBelow(doc, c.Z);
    }

    /// <summary>Nível da vista ativa (plantas) ou o mais baixo do projeto.</summary>
    public static Level? ViewLevel(View v)
    {
        if (v is ViewPlan vp && vp.GenLevel != null) return vp.GenLevel;
        return Lookup.Collect<Level>(v.Document).OrderBy(l => l.Elevation).FirstOrDefault();
    }

    public static bool IsPlanLike(View v) => v.ViewType is ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan or ViewType.AreaPlan;

    public static IEnumerable<Room> Rooms(Document doc) =>
        new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().OfType<Room>();

    /// <summary>Ambiente que contém o ponto (testa a cota informada e um pouco acima).</summary>
    public static Room? RoomAt(Document doc, XYZ p, IEnumerable<Room>? rooms = null)
    {
        foreach (var r in rooms ?? Rooms(doc))
        {
            if (r.Area <= 0) continue;
            if (r.IsPointInRoom(p) || r.IsPointInRoom(p + new XYZ(0, 0, 1))) return r;
        }
        return null;
    }
}
