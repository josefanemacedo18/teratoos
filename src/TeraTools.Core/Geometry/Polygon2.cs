using System.Collections;

namespace TeraTools.Core.Geometry;

/// <summary>
/// Polígono simples 2D (fechado implicitamente). Base para contornos de ambientes,
/// pisos, forros, peças de revestimento e chapas.
/// </summary>
public sealed class Polygon2 : IReadOnlyList<Vec2>
{
    private readonly Vec2[] _pts;

    public static readonly Polygon2 Empty = new(Array.Empty<Vec2>());

    public Polygon2(IEnumerable<Vec2> points)
    {
        _pts = Clean(points as Vec2[] ?? points.ToArray());
    }

    public int Count => _pts.Length;
    public Vec2 this[int index] => _pts[index];
    public IReadOnlyList<Vec2> Points => _pts;
    public bool IsEmpty => _pts.Length < 3;

    public IEnumerator<Vec2> GetEnumerator() => ((IEnumerable<Vec2>)_pts).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _pts.GetEnumerator();

    private static Vec2[] Clean(Vec2[] pts)
    {
        if (pts.Length == 0)
            return pts;
        var list = new List<Vec2>(pts.Length);
        foreach (var p in pts)
        {
            if (list.Count == 0 || !list[^1].AlmostEquals(p, 1e-9))
                list.Add(p);
        }
        if (list.Count > 1 && list[0].AlmostEquals(list[^1], 1e-9))
            list.RemoveAt(list.Count - 1);
        return list.ToArray();
    }

    /// <summary>Área com sinal (positiva = anti-horário).</summary>
    public double SignedArea
    {
        get
        {
            var n = _pts.Length;
            if (n < 3) return 0;
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
                a += _pts[j].X * _pts[i].Y - _pts[i].X * _pts[j].Y;
            return a * 0.5;
        }
    }

    public double Area => Math.Abs(SignedArea);

    public bool IsCounterClockwise => SignedArea > 0;

    public double Perimeter
    {
        get
        {
            var n = _pts.Length;
            if (n < 2) return 0;
            double p = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
                p += _pts[i].DistanceTo(_pts[j]);
            return p;
        }
    }

    public Vec2 Centroid
    {
        get
        {
            var n = _pts.Length;
            if (n == 0) return Vec2.Zero;
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var f = _pts[j].X * _pts[i].Y - _pts[i].X * _pts[j].Y;
                a += f;
                cx += (_pts[j].X + _pts[i].X) * f;
                cy += (_pts[j].Y + _pts[i].Y) * f;
            }
            if (Math.Abs(a) < 1e-14)
            {
                double sx = 0, sy = 0;
                foreach (var p in _pts) { sx += p.X; sy += p.Y; }
                return new Vec2(sx / n, sy / n);
            }
            a *= 0.5;
            return new Vec2(cx / (6 * a), cy / (6 * a));
        }
    }

    public Rect2 Bounds => Rect2.FromPoints(_pts);

    public IEnumerable<Segment2> Edges()
    {
        var n = _pts.Length;
        for (var i = 0; i < n; i++)
            yield return new Segment2(_pts[i], _pts[(i + 1) % n]);
    }

    /// <summary>Ponto dentro do polígono (pontos sobre a borda contam como dentro).</summary>
    public bool Contains(Vec2 p, double boundaryTol = 1e-9)
    {
        var n = _pts.Length;
        if (n < 3) return false;
        var inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = _pts[j];
            var b = _pts[i];
            if (new Segment2(a, b).DistanceTo(p) <= boundaryTol)
                return true;
            if ((b.Y > p.Y) != (a.Y > p.Y))
            {
                var x = (a.X - b.X) * (p.Y - b.Y) / (a.Y - b.Y) + b.X;
                if (p.X < x)
                    inside = !inside;
            }
        }
        return inside;
    }

    public bool IsConvex
    {
        get
        {
            var n = _pts.Length;
            if (n < 3) return false;
            var sign = 0;
            for (var i = 0; i < n; i++)
            {
                var a = _pts[i];
                var b = _pts[(i + 1) % n];
                var c = _pts[(i + 2) % n];
                var cr = (b - a).Cross(c - b);
                if (Math.Abs(cr) < 1e-12) continue;
                var s = Math.Sign(cr);
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }
    }

    public Polygon2 Reversed() => new(_pts.Reverse());

    public Polygon2 EnsureCounterClockwise() => SignedArea < 0 ? Reversed() : this;

    public Polygon2 Transform(Func<Vec2, Vec2> f) => new(_pts.Select(f));

    public Polygon2 Translate(Vec2 v) => Transform(p => p + v);

    public Polygon2 Rotate(double radians, Vec2 center) => Transform(p => p.RotateAround(center, radians));

    public Polygon2 Scale(double s, Vec2 center) => Transform(p => center + (p - center) * s);

    public double DistanceToBoundary(Vec2 p)
    {
        var best = double.PositiveInfinity;
        foreach (var e in Edges())
            best = Math.Min(best, e.DistanceTo(p));
        return best;
    }

    public Vec2 ClosestPointOnBoundary(Vec2 p)
    {
        var best = double.PositiveInfinity;
        var bp = p;
        foreach (var e in Edges())
        {
            var c = e.ClosestPoint(p);
            var d = c.DistanceTo(p);
            if (d < best) { best = d; bp = c; }
        }
        return bp;
    }

    /// <summary>
    /// Recorta este polígono (pode ser côncavo) por um polígono CONVEXO (Sutherland–Hodgman).
    /// A área do resultado é exata; pedaços desconexos podem aparecer ligados por arestas de largura zero.
    /// </summary>
    public Polygon2 ClipByConvex(Polygon2 convexClipper)
    {
        if (IsEmpty || convexClipper.IsEmpty) return Empty;
        var clip = convexClipper.EnsureCounterClockwise();
        var output = new List<Vec2>(_pts);
        var cn = clip.Count;
        for (var i = 0; i < cn && output.Count > 0; i++)
        {
            var a = clip[i];
            var b = clip[(i + 1) % cn];
            var edge = b - a;
            var input = output;
            output = new List<Vec2>(input.Count + 4);
            for (var k = 0; k < input.Count; k++)
            {
                var cur = input[k];
                var prev = input[(k + input.Count - 1) % input.Count];
                var curIn = edge.Cross(cur - a) >= -1e-12;
                var prevIn = edge.Cross(prev - a) >= -1e-12;
                if (curIn)
                {
                    if (!prevIn && Segment2.LineIntersection(prev, cur - prev, a, edge, out var ip))
                        output.Add(ip);
                    output.Add(cur);
                }
                else if (prevIn && Segment2.LineIntersection(prev, cur - prev, a, edge, out var ip2))
                {
                    output.Add(ip2);
                }
            }
        }
        return output.Count < 3 ? Empty : new Polygon2(output);
    }

    /// <summary>
    /// Deslocamento (offset) do contorno. Distância positiva expande, negativa contrai.
    /// Arestas que colapsam são removidas; cantos muito agudos ao expandir são chanfrados.
    /// </summary>
    public Polygon2 Offset(double distance, double miterLimit = 4.0)
    {
        if (IsEmpty) return Empty;
        if (Math.Abs(distance) < 1e-12) return this;
        var poly = EnsureCounterClockwise();
        var n = poly.Count;
        var lines = new List<(Vec2 P, Vec2 D)>(n * 2);
        for (var i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            var d = (b - a).Normalized();
            if (d.LengthSquared < 1e-20) continue;
            var nrm = new Vec2(d.Y, -d.X);

            // Chanfro no vértice "a" se o canto for convexo no sentido do deslocamento e muito agudo.
            {
                var prevA = poly[(i + n - 1) % n];
                var pd = (a - prevA).Normalized();
                var pn = new Vec2(pd.Y, -pd.X);
                var turn = pd.Cross(d); // >0 = canto convexo em polígono anti-horário
                var bis = (pn + nrm);
                var cosHalf = bis.Length / 2;
                if (bis.LengthSquared > 1e-20 && cosHalf > 1e-9)
                {
                    var miter = 1 / cosHalf;
                    var convexForOffset = distance > 0 ? turn > 1e-12 : turn < -1e-12;
                    if (convexForOffset && miter > miterLimit)
                    {
                        var b0 = bis.Normalized();
                        lines.Add((a + b0 * distance, new Vec2(-b0.Y, b0.X)));
                    }
                }
            }
            lines.Add((a + nrm * distance, d));
        }

        for (var guard = 0; guard < 4 * n + 8; guard++)
        {
            if (lines.Count < 3) return Empty;
            var verts = ComputeVertices(lines);
            var removed = false;
            for (var k = 0; k < lines.Count; k++)
            {
                var v0 = verts[k];
                var v1 = verts[(k + 1) % lines.Count];
                if ((v1 - v0).Dot(lines[k].D) < -1e-9)
                {
                    lines.RemoveAt(k);
                    removed = true;
                    break;
                }
            }
            if (!removed)
            {
                var result = new Polygon2(verts);
                return result.SignedArea <= 1e-12 ? Empty : result;
            }
        }
        return Empty;
    }

    private static List<Vec2> ComputeVertices(List<(Vec2 P, Vec2 D)> lines)
    {
        var m = lines.Count;
        var verts = new List<Vec2>(m);
        for (var k = 0; k < m; k++)
        {
            var prev = lines[(k + m - 1) % m];
            var cur = lines[k];
            if (!Segment2.LineIntersection(prev.P, prev.D, cur.P, cur.D, out var ip))
                ip = cur.P;
            verts.Add(ip);
        }
        return verts;
    }

    /// <summary>Remove vértices colineares (desvio menor que a tolerância).</summary>
    public Polygon2 Simplify(double tol = 1e-6)
    {
        if (Count < 4) return this;
        var pts = new List<Vec2>(_pts);
        var changed = true;
        while (changed && pts.Count > 3)
        {
            changed = false;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[(i + pts.Count - 1) % pts.Count];
                var b = pts[i];
                var c = pts[(i + 1) % pts.Count];
                if (new Segment2(a, c).DistanceTo(b) <= tol)
                {
                    pts.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return new Polygon2(pts);
    }

    /// <summary>
    /// Contorno pronto para um esboço do Revit: nenhuma aresta menor que <paramref name="minEdge"/>
    /// (os vértices próximos são FUNDIDOS — pular o segmento deixaria o laço aberto, e o Revit recusa),
    /// sem vértices colineares e sem auto-interseção. Devolve nulo se a forma degenerar.
    /// </summary>
    public Polygon2? CleanForSketch(double minEdge)
    {
        var pts = new List<Vec2>(_pts.Length);
        foreach (var p in _pts)
            if (pts.Count == 0 || pts[^1].DistanceTo(p) >= minEdge) pts.Add(p);
        while (pts.Count > 2 && pts[^1].DistanceTo(pts[0]) < minEdge) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) return null;
        var clean = new Polygon2(pts).Simplify(minEdge * 0.05);
        // a remoção de colineares pode reaproximar vértices: confere de novo
        for (var i = 0; i < clean.Count; i++)
            if (clean[i].DistanceTo(clean[(i + 1) % clean.Count]) < minEdge) return null;
        if (clean.Count < 3 || clean.Area < minEdge * minEdge || !clean.IsSimple()) return null;
        return clean;
    }

    /// <summary>Verdadeiro se nenhuma aresta cruza outra (exceto as vizinhas, no vértice comum).</summary>
    public bool IsSimple()
    {
        var n = _pts.Length;
        if (n < 4) return n == 3 && Area > 0;
        for (var i = 0; i < n; i++)
        {
            var a = new Segment2(_pts[i], _pts[(i + 1) % n]);
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // vizinhas (fecham o laço)
                var b = new Segment2(_pts[j], _pts[(j + 1) % n]);
                if (a.Intersect(b, out _, out _, out _)) return false;
            }
        }
        return true;
    }

    public static Polygon2 Rectangle(Vec2 min, double width, double height) =>
        Rect2.FromSize(min.X, min.Y, width, height).ToPolygon();

    /// <summary>Retângulo centrado, girado (radianos).</summary>
    public static Polygon2 Rectangle(Vec2 center, double width, double height, double rotation)
    {
        var hw = width / 2;
        var hh = height / 2;
        var pts = new[] { new Vec2(-hw, -hh), new Vec2(hw, -hh), new Vec2(hw, hh), new Vec2(-hw, hh) };
        return new Polygon2(pts.Select(p => p.Rotate(rotation) + center));
    }

    public static Polygon2 Regular(int sides, double radius, Vec2 center, double rotation = 0)
    {
        sides = Math.Max(3, sides);
        var pts = new Vec2[sides];
        for (var i = 0; i < sides; i++)
            pts[i] = center + Vec2.FromAngle(rotation + i * 2 * Math.PI / sides, radius);
        return new Polygon2(pts);
    }

    public static Polygon2 Circle(Vec2 center, double radius, int segments = 32) =>
        Regular(Math.Max(8, segments), radius, center);
}
