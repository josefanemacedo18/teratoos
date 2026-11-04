namespace TeraTools.Core.Geometry;

/// <summary>
/// Região plana: contorno externo com furos (ex.: ambiente com pilares, piso com shafts).
/// </summary>
public sealed class Region2
{
    public Polygon2 Outer { get; }
    public IReadOnlyList<Polygon2> Holes { get; }

    public Region2(Polygon2 outer, IEnumerable<Polygon2>? holes = null)
    {
        Outer = outer.EnsureCounterClockwise();
        Holes = (holes ?? Enumerable.Empty<Polygon2>())
            .Where(h => !h.IsEmpty)
            .Select(h => h.EnsureCounterClockwise())
            .ToList();
    }

    public double Area => Math.Max(0, Outer.Area - Holes.Sum(h => h.Area));

    public double Perimeter => Outer.Perimeter + Holes.Sum(h => h.Perimeter);

    public Rect2 Bounds => Outer.Bounds;

    public bool IsEmpty => Outer.IsEmpty;

    public bool Contains(Vec2 p, double tol = 1e-9)
    {
        if (!Outer.Contains(p, tol)) return false;
        foreach (var h in Holes)
        {
            // Ponto sobre a borda do furo conta como dentro da região.
            if (h.Contains(p, 0) && h.DistanceToBoundary(p) > tol)
                return false;
        }
        return true;
    }

    /// <summary>Área da interseção com um polígono convexo (ex.: uma peça de revestimento).</summary>
    public double IntersectionArea(Polygon2 convex)
    {
        var a = Outer.ClipByConvex(convex).Area;
        foreach (var h in Holes)
            a -= h.ClipByConvex(convex).Area;
        return Math.Max(0, a);
    }

    /// <summary>Distância até a borda mais próxima (externa ou furos).</summary>
    public double DistanceToBoundary(Vec2 p)
    {
        var d = Outer.DistanceToBoundary(p);
        foreach (var h in Holes)
            d = Math.Min(d, h.DistanceToBoundary(p));
        return d;
    }

    /// <summary>Contração/expansão da região inteira (furos se deslocam no sentido oposto).</summary>
    public Region2 Offset(double distance)
    {
        var outer = Outer.Offset(distance);
        var holes = Holes.Select(h => h.Offset(-distance)).Where(h => !h.IsEmpty);
        return new Region2(outer, holes);
    }

    public Region2 Transform(Func<Vec2, Vec2> f) => new(Outer.Transform(f), Holes.Select(h => h.Transform(f)));

    /// <summary>
    /// Recorta um segmento pela região, devolvendo os trechos internos
    /// (usado para linhas de junta, aletas, distribuição de luminárias etc.).
    /// </summary>
    public List<Segment2> ClipSegment(Segment2 seg)
    {
        var ts = new List<double> { 0, 1 };
        foreach (var poly in Holes.Prepend(Outer))
        {
            foreach (var e in poly.Edges())
            {
                if (seg.Intersect(e, out _, out var t, out _))
                    ts.Add(Math.Clamp(t, 0, 1));
            }
        }
        ts.Sort();
        var result = new List<Segment2>();
        for (var i = 0; i < ts.Count - 1; i++)
        {
            var t0 = ts[i];
            var t1 = ts[i + 1];
            if (t1 - t0 < 1e-9) continue;
            var mid = seg.PointAt((t0 + t1) / 2);
            if (!Contains(mid)) continue;
            var a = seg.PointAt(t0);
            var b = seg.PointAt(t1);
            if (result.Count > 0 && result[^1].B.AlmostEquals(a, 1e-9))
                result[^1] = new Segment2(result[^1].A, b);
            else
                result.Add(new Segment2(a, b));
        }
        return result;
    }

    /// <summary>
    /// Monta regiões a partir de laços soltos, usando a profundidade de contenção:
    /// laços em profundidade par são contornos externos, ímpar são furos.
    /// </summary>
    public static List<Region2> FromLoops(IEnumerable<Polygon2> loops)
    {
        var list = loops.Where(l => !l.IsEmpty).OrderByDescending(l => l.Area).ToList();
        var depth = new int[list.Count];
        var parent = new int[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            parent[i] = -1;
            var probe = list[i].Centroid;
            if (!list[i].Contains(probe)) probe = list[i][0];
            for (var j = 0; j < i; j++)
            {
                if (list[j].Contains(probe))
                {
                    depth[i]++;
                    if (parent[i] == -1 || list[j].Area < list[parent[i]].Area)
                        parent[i] = j;
                }
            }
        }
        var regions = new List<Region2>();
        for (var i = 0; i < list.Count; i++)
        {
            if (depth[i] % 2 != 0) continue;
            var holes = new List<Polygon2>();
            for (var k = 0; k < list.Count; k++)
                if (parent[k] == i && depth[k] == depth[i] + 1)
                    holes.Add(list[k]);
            regions.Add(new Region2(list[i], holes));
        }
        return regions;
    }
}
