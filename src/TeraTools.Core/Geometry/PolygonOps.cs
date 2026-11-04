namespace TeraTools.Core.Geometry;

/// <summary>Operações entre polígonos: interseção, distância, envoltória.</summary>
public static class PolygonOps
{
    /// <summary>Verdadeiro se os polígonos se sobrepõem (área comum &gt; tolerância ou bordas se cruzam).</summary>
    public static bool Intersects(Polygon2 a, Polygon2 b, double areaTol = 1e-8)
    {
        if (a.IsEmpty || b.IsEmpty) return false;
        if (!a.Bounds.Intersects(b.Bounds, -1e-9)) return false;
        if (b.IsConvex) return a.ClipByConvex(b).Area > areaTol;
        if (a.IsConvex) return b.ClipByConvex(a).Area > areaTol;
        foreach (var ea in a.Edges())
            foreach (var eb in b.Edges())
                if (ea.Intersect(eb, out _, out var t, out var u) && t > 1e-6 && t < 1 - 1e-6 && u > 1e-6 && u < 1 - 1e-6)
                    return true;
        return a.Contains(b[0], 0) || b.Contains(a[0], 0);
    }

    /// <summary>Menor distância entre as bordas de dois polígonos (0 se se sobrepõem).</summary>
    public static double Distance(Polygon2 a, Polygon2 b)
    {
        if (Intersects(a, b)) return 0;
        var best = double.PositiveInfinity;
        foreach (var p in a) best = Math.Min(best, b.DistanceToBoundary(p));
        foreach (var p in b) best = Math.Min(best, a.DistanceToBoundary(p));
        return best;
    }

    /// <summary>Distância de um ponto a um polígono (0 se dentro).</summary>
    public static double Distance(Vec2 p, Polygon2 poly) => poly.Contains(p) ? 0 : poly.DistanceToBoundary(p);

    /// <summary>Envoltória convexa (cadeia monótona de Andrew).</summary>
    public static Polygon2 ConvexHull(IEnumerable<Vec2> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3) return new Polygon2(pts);
        var hull = new List<Vec2>();
        for (var pass = 0; pass < 2; pass++)
        {
            var start = hull.Count;
            foreach (var p in pass == 0 ? pts : Enumerable.Reverse(pts))
            {
                while (hull.Count >= start + 2 && (hull[^1] - hull[^2]).Cross(p - hull[^2]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return new Polygon2(hull);
    }

    /// <summary>Retângulo orientado em frente a uma face (área de aproximação).</summary>
    public static Polygon2 FrontRectangle(Polygon2 footprint, Vec2 facing, double width, double depth)
    {
        var f = facing.Normalized();
        var side = f.Perp;
        var c = footprint.Centroid;
        var front = footprint.Max(p => (p - c).Dot(f));
        var baseCenter = c + f * front;
        var hw = width / 2;
        return new Polygon2(new[]
        {
            baseCenter - side * hw,
            baseCenter + side * hw,
            baseCenter + side * hw + f * depth,
            baseCenter - side * hw + f * depth,
        });
    }

    /// <summary>Setor circular (ex.: abertura de porta) aproximado por polígono convexo.</summary>
    public static Polygon2 Sector(Vec2 center, double radius, double startAngle, double sweep, int segments = 12)
    {
        var pts = new List<Vec2> { center };
        for (var i = 0; i <= segments; i++)
            pts.Add(center + Vec2.FromAngle(startAngle + sweep * i / segments, radius));
        var poly = new Polygon2(pts);
        return poly.EnsureCounterClockwise();
    }
}
