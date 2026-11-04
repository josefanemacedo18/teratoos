using Autodesk.Revit.DB;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Formas livres (DirectShape), encadeamento de curvas e utilidades de sólidos.</summary>
public static class Shapes
{
    public const string AppId = "TeraTools";

    public static DirectShape Create(Document doc, IList<GeometryObject> geometry, BuiltInCategory category, string name)
    {
        var catId = new ElementId(category);
        if (!DirectShape.IsValidCategoryId(catId, doc)) catId = new ElementId(BuiltInCategory.OST_GenericModel);
        var ds = DirectShape.CreateElement(doc, catId);
        ds.ApplicationId = AppId;
        ds.ApplicationDataId = Guid.NewGuid().ToString();
        ds.SetShape(geometry);
        if (!string.IsNullOrWhiteSpace(name)) ds.SetName(name);
        return ds;
    }

    public static SolidOptions Options(Document doc, string? materialName)
    {
        var mat = Lookup.Material(doc, materialName);
        return new SolidOptions(mat?.Id ?? ElementId.InvalidElementId, ElementId.InvalidElementId);
    }

    /// <summary>Caixa orientada como sólido (para formas de apresentação e vazios).</summary>
    public static Solid Box(OrientedBox box, SolidOptions? opts = null)
    {
        var f = box.Frame;
        var p0 = f.Origin.ToXYZ();
        var x = f.XAxis.ToXYZ() * box.SizeX;
        var y = f.YAxis.ToXYZ() * box.SizeY;
        var loop = CurveLoop.Create(new List<Curve>
        {
            Line.CreateBound(p0, p0 + x),
            Line.CreateBound(p0 + x, p0 + x + y),
            Line.CreateBound(p0 + x + y, p0 + y),
            Line.CreateBound(p0 + y, p0),
        });
        return opts == null
            ? GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, f.ZAxis.ToXYZ(), box.SizeZ)
            : GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, f.ZAxis.ToXYZ(), box.SizeZ, opts);
    }

    public static Solid Prism(Polygon2 poly, double z0, double height, SolidOptions? opts = null)
    {
        var loop = RevitGeo.PolygonToLoop(poly, z0);
        return opts == null
            ? GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, height)
            : GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, height, opts);
    }

    /// <summary>Encadeia curvas soltas em laços fechados (tolerância de conexão em pés).</summary>
    public static List<List<Curve>> ChainLoops(IEnumerable<Curve> curves, double tol = 0.01)
    {
        var pool = curves.Where(c => c.IsBound && c.Length > Tolerance.ShortCurve).ToList();
        var loops = new List<List<Curve>>();
        while (pool.Count > 0)
        {
            var chain = new List<Curve> { pool[0] };
            pool.RemoveAt(0);
            var start = chain[0].GetEndPoint(0);
            var end = chain[0].GetEndPoint(1);
            var grown = true;
            while (grown && start.DistanceTo(end) > tol)
            {
                grown = false;
                for (var i = 0; i < pool.Count; i++)
                {
                    var c = pool[i];
                    if (c.GetEndPoint(0).DistanceTo(end) <= tol)
                    {
                        chain.Add(c);
                        end = c.GetEndPoint(1);
                    }
                    else if (c.GetEndPoint(1).DistanceTo(end) <= tol)
                    {
                        chain.Add(c.CreateReversed());
                        end = c.GetEndPoint(0);
                    }
                    else continue;
                    pool.RemoveAt(i);
                    grown = true;
                    break;
                }
            }
            if (start.DistanceTo(end) <= tol && chain.Count >= 2) loops.Add(chain);
        }
        return loops;
    }

    /// <summary>Monta um CurveLoop válido a partir de curvas encadeadas, fechando pequenas folgas.</summary>
    public static CurveLoop? ToCurveLoop(List<Curve> chain)
    {
        try
        {
            var loop = new CurveLoop();
            for (var i = 0; i < chain.Count; i++)
            {
                var c = chain[i];
                var next = chain[(i + 1) % chain.Count];
                var a = c.GetEndPoint(0);
                var b = next.GetEndPoint(0);
                if (c is Line)
                {
                    if (a.DistanceTo(b) > Tolerance.ShortCurve) loop.Append(Line.CreateBound(a, b));
                }
                else
                {
                    loop.Append(c);
                }
            }
            return loop;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Curvas de um elemento: linhas de modelo/detalhe ou geometria de CAD importado.</summary>
    public static List<(Curve Curve, string Layer)> CurvesOf(Element e)
    {
        var list = new List<(Curve, string)>();
        if (e is CurveElement ce)
        {
            list.Add((ce.GeometryCurve, ce.LineStyle?.Name ?? ""));
            return list;
        }
        var ge = e.get_Geometry(new Options { ComputeReferences = false, IncludeNonVisibleObjects = false });
        if (ge == null) return list;
        void Walk(GeometryElement g)
        {
            foreach (var obj in g)
            {
                switch (obj)
                {
                    case GeometryInstance gi:
                        var inst = gi.GetInstanceGeometry();
                        if (inst != null) Walk(inst);
                        break;
                    case Curve c when c.IsBound:
                        list.Add((c, LayerName(e.Document, c.GraphicsStyleId)));
                        break;
                    case PolyLine pl:
                        var pts = pl.GetCoordinates();
                        for (var i = 0; i + 1 < pts.Count; i++)
                            if (pts[i].DistanceTo(pts[i + 1]) > Tolerance.ShortCurve)
                                list.Add((Line.CreateBound(pts[i], pts[i + 1]), LayerName(e.Document, pl.GraphicsStyleId)));
                        break;
                }
            }
        }
        Walk(ge);
        return list;
    }

    private static string LayerName(Document doc, ElementId styleId)
    {
        if (styleId == ElementId.InvalidElementId) return "";
        return doc.GetElement(styleId) is GraphicsStyle gs ? gs.GraphicsStyleCategory?.Name ?? gs.Name : "";
    }

    public static string FailText(Exception ex) => Loc.T("falhou: ", "failed: ") + ex.Message;
}
