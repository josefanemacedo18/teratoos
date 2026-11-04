using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using TeraTools.Core.Checks;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Tools.Interiors;

/// <summary>Utilitários compartilhados pelas ferramentas de interiores.</summary>
internal static class InteriorsUtil
{
    /// <summary>Categorias tratadas como mobiliário/equipamento de interiores.</summary>
    public const string FurnitureCatList = "OST_Furniture,OST_FurnitureSystems,OST_Casework,OST_PlumbingFixtures,OST_SpecialityEquipment,OST_GenericModel";

    public static readonly BuiltInCategory[] FurnitureCats = Lookup.ParseCategories(FurnitureCatList).ToArray();

    public static bool IsFurniture(Element e) =>
        e is FamilyInstance && e.Category != null && FurnitureCats.Any(c => (long)c == e.Category.Id.Value);

    public static string Name(Element e) => e is FamilyInstance fi ? $"{fi.Symbol.FamilyName} : {fi.Symbol.Name}" : e.Name;

    public static string CategoryName(Element e) =>
        e.Category != null && Enum.IsDefined(typeof(BuiltInCategory), (int)e.Category.Id.Value) ? ((BuiltInCategory)e.Category.Id.Value).ToString() : "";

    // ------------------------------------------------------------ Pegadas

    /// <summary>Pegada em planta: envoltória convexa dos vértices dos sólidos (caixa envolvente como reserva).</summary>
    public static Footprint FootprintOf(Element e)
    {
        var pts = new List<Vec2>();
        double z0 = double.MaxValue, z1 = double.MinValue;
        foreach (var s in RevitGeo.Solids(e))
        {
            foreach (Edge edge in s.Edges)
            {
                foreach (var p in edge.Tessellate())
                {
                    pts.Add(new Vec2(p.X, p.Y));
                    z0 = Math.Min(z0, p.Z);
                    z1 = Math.Max(z1, p.Z);
                }
            }
        }
        Polygon2 outline;
        if (pts.Count >= 3)
        {
            outline = PolygonOps.ConvexHull(pts);
        }
        else
        {
            var bb = RevitGeo.Box(e);
            if (bb == null)
            {
                outline = Polygon2.Empty;
                z0 = z1 = 0;
            }
            else
            {
                outline = Polygon2.Rectangle(new Vec2(bb.Min.X, bb.Min.Y), bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
                z0 = bb.Min.Z;
                z1 = bb.Max.Z;
            }
        }
        return new Footprint
        {
            Id = e.Id.Value,
            Name = Name(e),
            Category = CategoryName(e),
            Outline = outline,
            BottomZ = z0 == double.MaxValue ? 0 : z0,
            TopZ = z1 == double.MinValue ? 0 : z1,
            Facing = Facing(e),
        };
    }

    public static Vec2 Facing(Element e)
    {
        if (e is FamilyInstance fi)
        {
            var f = fi.FacingOrientation;
            var v = new Vec2(f.X, f.Y);
            if (v.Length > 1e-9) return v.Normalized();
        }
        return Vec2.UnitY;
    }

    /// <summary>Extensão da pegada ao longo de uma direção (máx − mín das projeções).</summary>
    public static (double Min, double Max) Extent(Polygon2 outline, Vec2 dir)
    {
        if (outline.Count == 0) return (0, 0);
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var p in outline)
        {
            var d = p.Dot(dir);
            min = Math.Min(min, d);
            max = Math.Max(max, d);
        }
        return (min, max);
    }

    // ------------------------------------------------------------ Ambientes

    /// <summary>Instâncias (das categorias pedidas) cujo ponto de inserção está no ambiente.</summary>
    public static List<FamilyInstance> InstancesInRoom(Document doc, Room room, IEnumerable<BuiltInCategory> cats)
    {
        var list = new List<FamilyInstance>();
        var bb = room.get_BoundingBox(null);
        var lz = room.Level?.Elevation ?? 0;
        var top = lz + Math.Max(room.UnboundedHeight, 1.0);
        var col = new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(cats.ToList()))
            .WhereElementIsNotElementType().OfType<FamilyInstance>();
        foreach (var fi in col)
        {
            if (fi.SuperComponent != null) continue;
            var p = (fi.Location as LocationPoint)?.Point ?? RevitGeo.BoxCenter(fi);
            if (p == null) continue;
            if (bb != null && (p.X < bb.Min.X - 1 || p.X > bb.Max.X + 1 || p.Y < bb.Min.Y - 1 || p.Y > bb.Max.Y + 1)) continue;
            if (p.Z < lz - 1.0 || p.Z > top + 0.5) continue;
            if (room.IsPointInRoom(new XYZ(p.X, p.Y, lz + 1.0)) || fi.Room?.Id == room.Id) list.Add(fi);
        }
        return list;
    }

    /// <summary>Ponto mais próximo do contorno do ambiente e a direção (parede → ponto).</summary>
    public static (Vec2 Point, Vec2 Inward, double Distance)? NearestWall(Region2 region, Vec2 p)
    {
        var best = double.MaxValue;
        Vec2 bestPt = default;
        Segment2 bestSeg = default;
        foreach (var poly in new[] { region.Outer }.Concat(region.Holes))
        {
            foreach (var s in poly.Edges())
            {
                if (s.Length < 1e-6) continue;
                var cp = s.ClosestPoint(p);
                var d = cp.DistanceTo(p);
                if (d < best)
                {
                    best = d;
                    bestPt = cp;
                    bestSeg = s;
                }
            }
        }
        if (best == double.MaxValue) return null;
        Vec2 inward;
        if (best > 1e-6)
        {
            inward = (p - bestPt).Normalized();
        }
        else
        {
            var t = bestSeg.Vector.Normalized();
            inward = new Vec2(-t.Y, t.X);
            if (!region.Contains(bestPt + inward * 0.05)) inward = inward * -1;
        }
        // Perpendicular à parede (evita inclinação quando o ponto mais próximo é um canto).
        var dir = bestSeg.Vector.Normalized();
        var perp = new Vec2(-dir.Y, dir.X);
        if (perp.Dot(inward) < 0) perp = perp * -1;
        if (Math.Abs(perp.Dot(inward)) > 0.5) inward = perp;
        return (bestPt, inward, best);
    }

    // ------------------------------------------------------------ Posicionamento

    /// <summary>Gira a instância (em torno do ponto de inserção) para que a frente aponte na direção dada.</summary>
    public static void TurnTo(Document doc, FamilyInstance fi, Vec2 facing)
    {
        if (fi.Location is not LocationPoint lp || facing.Length < 1e-9) return;
        var cur = Facing(fi);
        var a = Math.Atan2(facing.Y, facing.X) - Math.Atan2(cur.Y, cur.X);
        a = Math.IEEERemainder(a, 2 * Math.PI);
        if (Math.Abs(a) < 1e-7) return;
        var p = lp.Point;
        ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(p, p + XYZ.BasisZ), a);
    }

    /// <summary>Move o elemento ao longo da normal para que sua pegada fique à distância indicada da linha.</summary>
    public static void PushOff(Document doc, Element e, Vec2 linePoint, Vec2 normal, double distance)
    {
        doc.Regenerate();
        var fp = FootprintOf(e);
        if (fp.Outline.Count == 0) return;
        var d = fp.Outline.Min(q => (q - linePoint).Dot(normal));
        var move = distance - d;
        if (Math.Abs(move) < 1e-6) return;
        ElementTransformUtils.MoveElement(doc, e.Id, new XYZ(normal.X * move, normal.Y * move, 0));
    }

    /// <summary>Cria uma instância de família em um ponto (famílias por nível ou por plano de trabalho).</summary>
    public static FamilyInstance PlaceFree(Document doc, FamilySymbol sym, Level level, XYZ point, double elevationFromLevel)
    {
        if (!sym.IsActive) sym.Activate();
        var pt = new XYZ(point.X, point.Y, level.Elevation + elevationFromLevel);
        var fi = doc.Create.NewFamilyInstance(pt, sym, level, StructuralType.NonStructural);
        if (Math.Abs(elevationFromLevel) > 1e-9)
        {
            var p = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM) ?? fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            if (p is { IsReadOnly: false } && Math.Abs(p.AsDouble() - elevationFromLevel) > 1e-6) p.Set(elevationFromLevel);
        }
        return fi;
    }

    public static bool IsFreePlacement(FamilySymbol sym) =>
        sym.Family.FamilyPlacementType is FamilyPlacementType.OneLevelBased or FamilyPlacementType.WorkPlaneBased;

    // ------------------------------------------------------------ Portas

    /// <summary>
    /// Área de abertura das folhas da porta: lida dos arcos da representação em planta;
    /// se a família não tiver arcos, estima um quarto de círculo do lado do ambiente.
    /// </summary>
    public static List<Polygon2> DoorSwings(FamilyInstance door, View? plan, Room room, out bool estimated)
    {
        estimated = false;
        var list = new List<Polygon2>();
        if (plan != null)
        {
            try
            {
                var ge = door.get_Geometry(new Options { View = plan });
                var arcs = new List<Arc>();
                if (ge != null) CollectArcs(ge, arcs);
                foreach (var arc in arcs.Where(a => a.IsBound && a.Radius > 0.8))
                {
                    var c = new Vec2(arc.Center.X, arc.Center.Y);
                    var pts = new List<Vec2> { c };
                    pts.AddRange(arc.Tessellate().Select(p => new Vec2(p.X, p.Y)));
                    var poly = new Polygon2(pts).EnsureCounterClockwise();
                    if (poly.Area > 0.05) list.Add(poly);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Arcos da porta: " + ex.Message);
            }
        }
        if (list.Count > 0) return list;

        if (door.Location is not LocationPoint lp) return list;
        var w = door.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                ?? door.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                ?? door.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0;
        if (w <= 0) return list;
        estimated = true;
        var p0 = new Vec2(lp.Point.X, lp.Point.Y);
        var h = new Vec2(door.HandOrientation.X, door.HandOrientation.Y).Normalized();
        var f = Facing(door);
        var half = ((door.Host as Wall)?.Width ?? 0.5) / 2;
        var lz = room.Level?.Elevation ?? 0;
        var probe = p0 + f * (half + 0.5);
        var side = room.IsPointInRoom(new XYZ(probe.X, probe.Y, lz + 1.0)) ? f : f * -1;
        var hinge = p0 - h * (w / 2) + side * half;
        var start = Math.Atan2(h.Y, h.X);
        var sweep = h.Cross(side) > 0 ? Math.PI / 2 : -Math.PI / 2;
        list.Add(PolygonOps.Sector(hinge, w, start, sweep, 12).EnsureCounterClockwise());
        return list;
    }

    private static void CollectArcs(GeometryElement ge, List<Arc> arcs)
    {
        foreach (var o in ge)
        {
            switch (o)
            {
                case Arc a:
                    arcs.Add(a);
                    break;
                case GeometryInstance gi:
                    var inst = gi.GetInstanceGeometry();
                    if (inst != null) CollectArcs(inst, arcs);
                    break;
            }
        }
    }

    /// <summary>Planta (de piso) para ler a representação em planta das portas de um nível.</summary>
    public static View? PlanFor(Document doc, View active, Level? level)
    {
        if (active is ViewPlan vp && vp.GenLevel != null && level != null && vp.GenLevel.Id == level.Id) return active;
        if (level == null) return active is ViewPlan ? active : null;
        return Lookup.Collect<ViewPlan>(doc).FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.GenLevel?.Id == level.Id);
    }

    // ------------------------------------------------------------ Marcações em vista

    public sealed class MarkerSection
    {
        public Dictionary<string, List<long>> Items { get; set; } = new();
    }

    private const string MarkerKey = "tt.markers";

    /// <summary>Exclui marcações criadas antes pela mesma ferramenta na mesma vista (dentro de transação).</summary>
    public static int ClearMarkers(Document doc, string key)
    {
        var s = ProjectStorage.GetSection<MarkerSection>(doc, MarkerKey);
        if (!s.Items.TryGetValue(key, out var ids)) return 0;
        var existing = ids.Select(i => new ElementId(i)).Where(i => doc.GetElement(i) != null).ToList();
        if (existing.Count > 0) doc.Delete(existing);
        s.Items.Remove(key);
        ProjectStorage.SetSection(doc, MarkerKey, s);
        return existing.Count;
    }

    public static void SaveMarkers(Document doc, string key, IEnumerable<ElementId> ids)
    {
        var s = ProjectStorage.GetSection<MarkerSection>(doc, MarkerKey);
        s.Items[key] = ids.Select(i => i.Value).ToList();
        ProjectStorage.SetSection(doc, MarkerKey, s);
    }

    /// <summary>Tipo de região preenchida pelo nome (ou o primeiro disponível).</summary>
    public static ElementId FilledRegionType(Document doc, string? name) =>
        (Lookup.Named<FilledRegionType>(doc, name) ?? Lookup.Collect<FilledRegionType>(doc).FirstOrDefault())?.Id
        ?? throw new ToolException(Loc.T("Nenhum tipo de região preenchida no projeto.", "No filled region type in the project."));

    /// <summary>Cria uma região preenchida na vista (cota Z da vista).</summary>
    public static FilledRegion? Region(Document doc, View view, ElementId typeId, Polygon2 poly)
    {
        if (poly.Count < 3 || Math.Abs(poly.Area) < 1e-4) return null;
        var z = (view as ViewPlan)?.GenLevel?.Elevation ?? 0;
        try
        {
            var loop = RevitGeo.PolygonToLoop(poly.Simplify(1e-4), z);
            return FilledRegion.Create(doc, typeId, view.Id, new List<CurveLoop> { loop });
        }
        catch (Exception ex)
        {
            Log.Debug("Região preenchida: " + ex.Message);
            return null;
        }
    }

    public static double Deg(double radians) => MathUtil.ToDegrees(radians);
}
