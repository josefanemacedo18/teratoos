using TeraTools.Core.Geometry;

namespace TeraTools.Core.Organic;

/// <summary>Formato da seção (corte horizontal) de uma coluna.</summary>
public enum ColumnSection
{
    Circle,
    Polygon,
    Lobed,
    Star,
    Ellipse,
    /// <summary>Contorno desenhado pelo usuário (linhas de modelo ou de detalhe).</summary>
    Drawn,
}

/// <summary>Seções unitárias (raio máximo = 1, centro na origem) e reamostragem com quantidade fixa de pontos.</summary>
public static class OrganicSection
{
    /// <summary>
    /// Seção unitária com <paramref name="n"/> pontos, no sentido anti-horário, começando no ângulo 0.
    /// Para seções desenhadas o contorno é centrado no centroide e escalado para raio máximo 1.
    /// </summary>
    public static List<Vec2> Unit(ColumnSection kind, int n, int sides = 6, int lobes = 5, double lobeDepth = 0.25,
        double starInner = 0.55, double ellipseRatio = 0.6, IReadOnlyList<Vec2>? drawn = null)
    {
        n = Math.Max(8, n);
        switch (kind)
        {
            case ColumnSection.Circle:
                return Polar(n, _ => 1);
            case ColumnSection.Lobed:
            {
                var d = Math.Clamp(lobeDepth, 0, 0.9);
                var k = Math.Max(2, lobes);
                // 1 - d·(1 - cos(kθ))/2: lóbulos arredondados com o raio máximo em 1
                return Polar(n, a => 1 - d * (1 - Math.Cos(k * a)) / 2);
            }
            case ColumnSection.Ellipse:
            {
                var r = Math.Clamp(ellipseRatio, 0.1, 1);
                return Enumerable.Range(0, n).Select(i =>
                {
                    var a = 2 * Math.PI * i / n;
                    return new Vec2(Math.Cos(a), r * Math.Sin(a));
                }).ToList();
            }
            case ColumnSection.Polygon:
                return Resample(Polygon2.Regular(Math.Clamp(sides, 3, 24), 1, Vec2.Zero), n);
            case ColumnSection.Star:
            {
                var k = Math.Clamp(sides, 3, 24);
                var inner = Math.Clamp(starInner, 0.15, 0.95);
                var pts = new List<Vec2>();
                for (var i = 0; i < 2 * k; i++)
                    pts.Add(Vec2.FromAngle(Math.PI * i / k, i % 2 == 0 ? 1 : inner));
                return Resample(new Polygon2(pts), n);
            }
            case ColumnSection.Drawn when drawn is { Count: >= 3 }:
                return Normalize(drawn, n);
            default:
                return Polar(n, _ => 1);
        }
    }

    private static List<Vec2> Polar(int n, Func<double, double> radius) =>
        Enumerable.Range(0, n).Select(i =>
        {
            var a = 2 * Math.PI * i / n;
            return Vec2.FromAngle(a, radius(a));
        }).ToList();

    /// <summary>Centraliza no centroide, garante sentido anti-horário, escala para raio máximo 1 e reamostra.</summary>
    public static List<Vec2> Normalize(IReadOnlyList<Vec2> contour, int n)
    {
        var poly = new Polygon2(contour).Simplify(1e-6).EnsureCounterClockwise();
        if (poly.IsEmpty) return Polar(n, _ => 1);
        var c = poly.Centroid;
        var max = poly.Max(p => p.DistanceTo(c));
        if (max < 1e-9) return Polar(n, _ => 1);
        var unit = poly.Transform(p => (p - c) / max);
        // começa no ponto mais próximo do ângulo 0, para que as seções de níveis diferentes fiquem alinhadas
        var start = 0;
        var best = double.MaxValue;
        for (var i = 0; i < unit.Count; i++)
        {
            var d = Math.Abs(unit[i].Angle);
            if (d < best) { best = d; start = i; }
        }
        var rotated = new Polygon2(Enumerable.Range(0, unit.Count).Select(i => unit[(start + i) % unit.Count]));
        return Resample(rotated, n);
    }

    /// <summary>
    /// Reamostra um contorno fechado em <paramref name="n"/> pontos distribuídos pelo comprimento. Se o contorno
    /// tiver até n vértices, todos os cantos são preservados (as arestas recebem pontos proporcionalmente).
    /// </summary>
    public static List<Vec2> Resample(Polygon2 poly, int n)
    {
        var pts = poly.Points;
        var m = pts.Count;
        var lengths = Enumerable.Range(0, m).Select(i => pts[i].DistanceTo(pts[(i + 1) % m])).ToArray();
        var total = lengths.Sum();
        var result = new List<Vec2>(n);
        if (total < 1e-12) return Enumerable.Repeat(pts[0], n).ToList();
        if (m <= n)
        {
            // pelo menos 1 ponto (o vértice) por aresta; o restante proporcional ao comprimento
            var counts = new int[m];
            var spare = n - m;
            var shares = lengths.Select(l => l / total * spare).ToArray();
            for (var i = 0; i < m; i++) counts[i] = 1 + (int)Math.Floor(shares[i]);
            var left = n - counts.Sum();
            foreach (var i in Enumerable.Range(0, m).OrderByDescending(i => shares[i] - Math.Floor(shares[i])))
            {
                if (left-- <= 0) break;
                counts[i]++;
            }
            for (var i = 0; i < m; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % m];
                for (var k = 0; k < counts[i]; k++) result.Add(Vec2.Lerp(a, b, (double)k / counts[i]));
            }
            return result;
        }
        var step = total / n;
        var edge = 0;
        var acc = 0.0;
        for (var k = 0; k < n; k++)
        {
            var target = k * step;
            while (edge < m - 1 && acc + lengths[edge] < target)
            {
                acc += lengths[edge];
                edge++;
            }
            var t = lengths[edge] < 1e-12 ? 0 : (target - acc) / lengths[edge];
            result.Add(Vec2.Lerp(pts[edge], pts[(edge + 1) % m], Math.Clamp(t, 0, 1)));
        }
        return result;
    }

    /// <summary>Área e menor momento de inércia (em relação ao centroide) de um contorno fechado.</summary>
    public static (double Area, double IMin) Properties(IReadOnlyList<Vec2> contour)
    {
        double a = 0, cx = 0, cy = 0;
        var n = contour.Count;
        for (var i = 0; i < n; i++)
        {
            var p = contour[i];
            var q = contour[(i + 1) % n];
            var cr = p.X * q.Y - q.X * p.Y;
            a += cr;
            cx += (p.X + q.X) * cr;
            cy += (p.Y + q.Y) * cr;
        }
        a /= 2;
        if (Math.Abs(a) < 1e-15) return (0, 0);
        cx /= 6 * a;
        cy /= 6 * a;
        double ixx = 0, iyy = 0, ixy = 0;
        for (var i = 0; i < n; i++)
        {
            var p = contour[i] - new Vec2(cx, cy);
            var q = contour[(i + 1) % n] - new Vec2(cx, cy);
            var cr = p.X * q.Y - q.X * p.Y;
            ixx += (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) * cr;
            iyy += (p.X * p.X + p.X * q.X + q.X * q.X) * cr;
            ixy += (p.X * q.Y + 2 * p.X * p.Y + 2 * q.X * q.Y + q.X * p.Y) * cr;
        }
        var sign = Math.Sign(a);
        ixx = ixx / 12 * sign;
        iyy = iyy / 12 * sign;
        ixy = ixy / 24 * sign;
        var avg = (ixx + iyy) / 2;
        var dif = Math.Sqrt(Math.Pow((ixx - iyy) / 2, 2) + ixy * ixy);
        return (Math.Abs(a), Math.Max(0, avg - dif));
    }
}
