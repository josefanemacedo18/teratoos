using TeraTools.Core.Geometry;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Patterns;

/// <summary>Padrões geométricos do gerador (no estilo Pixy Tools).</summary>
public enum PixyPattern
{
    Hexagonal,
    Voronoi,
    StarBurst,
    Circles,
    Diamonds,
    Triangles,
    Squares,
    Waves,
    Bricks,
}

/// <summary>Efeito de "fade" (variação do tamanho das aberturas ao longo da face).</summary>
public enum FadeMode
{
    None,
    Linear,
    Radial,
    Noise,
}

public sealed class PixySettings
{
    public PixyPattern Pattern { get; set; } = PixyPattern.Hexagonal;

    /// <summary>Tamanho da célula (distância entre centros) — controla a densidade.</summary>
    public double CellSize { get; set; } = U.Mm(300);

    /// <summary>Proporção abertura/sólido (0,05–0,95): quanto de cada célula a forma ocupa.</summary>
    public double OpeningRatio { get; set; } = 0.6;

    /// <summary>Escala geral aplicada às formas.</summary>
    public double Scale { get; set; } = 1;

    public double RotationDeg { get; set; }
    public int StarPoints { get; set; } = 5;
    public double StarInnerRatio { get; set; } = 0.45;

    /// <summary>Amplitude da onda em células (padrão Ondas).</summary>
    public double WaveAmplitude { get; set; } = 0.6;

    /// <summary>Comprimento da onda em células (padrão Ondas).</summary>
    public double WaveLength { get; set; } = 8;

    public double RandomSize { get; set; }
    public double RandomRotationDeg { get; set; }
    public double RandomPosition { get; set; }
    public double RandomSkip { get; set; }
    public int Seed { get; set; } = 1;

    public FadeMode Fade { get; set; } = FadeMode.None;
    public double FadeAngleDeg { get; set; }
    public double FadeFrom { get; set; } = 1;
    public double FadeTo { get; set; } = 0.2;

    /// <summary>Centro do fade radial e do Star Burst, em fração da face (0–1).</summary>
    public double CenterU { get; set; } = 0.5;
    public double CenterV { get; set; } = 0.5;

    public double EdgeMargin { get; set; }

    /// <summary>Formas menores que isto (em pés) são descartadas.</summary>
    public double MinSize { get; set; } = U.Mm(4);
}

public sealed class PixyCell
{
    public Polygon2 Shape { get; init; } = Polygon2.Empty;
    public Vec2 Center { get; init; }

    /// <summary>Fator final de tamanho (fade × escala × aleatório), útil para variar espessura.</summary>
    public double Factor { get; init; }

    /// <summary>Valor aleatório estável (0–1) por célula (variação de espessura etc.).</summary>
    public double Random01 { get; init; }

    public int Index { get; init; }
}

public static class PixyPatternGenerator
{
    private readonly record struct Base(Vec2 Center, Polygon2 Shape, double Angle);

    public static List<PixyCell> Generate(PixySettings s, Region2 domain, int hardLimit = 5000)
    {
        var a = Math.Max(s.CellSize, U.Mm(5));
        var b = domain.Bounds;
        var bases = s.Pattern switch
        {
            PixyPattern.Voronoi => Voronoi(s, b, a),
            PixyPattern.StarBurst => StarBurst(s, b, a),
            PixyPattern.Waves => Waves(s, b, a),
            _ => Tiling(s, b, a),
        };
        var inset = s.EdgeMargin > 1e-9 ? domain.Offset(-s.EdgeMargin) : domain;
        var center = new Vec2(b.MinX + b.Width * s.CenterU, b.MinY + b.Height * s.CenterV);
        var dir = Vec2.FromAngle(MathUtil.ToRadians(s.FadeAngleDeg));
        double pMin = double.MaxValue, pMax = double.MinValue;
        foreach (var c in new[] { b.Min, b.Max, new Vec2(b.MinX, b.MaxY), new Vec2(b.MaxX, b.MinY) })
        {
            pMin = Math.Min(pMin, c.Dot(dir));
            pMax = Math.Max(pMax, c.Dot(dir));
        }
        var maxDist = new[] { b.Min, b.Max, new Vec2(b.MinX, b.MaxY), new Vec2(b.MaxX, b.MinY) }.Max(c => c.DistanceTo(center));
        var ratio = Math.Clamp(s.OpeningRatio, 0.05, 0.95);
        var result = new List<PixyCell>();
        var k = -1;
        foreach (var bc in bases)
        {
            k++;
            if (result.Count >= hardLimit) break;
            var r0 = Rng.Hash(k, 17, s.Seed);
            if (s.RandomSkip > 0 && r0 < s.RandomSkip) continue;
            var c = bc.Center;
            if (s.RandomPosition > 0 && s.Pattern != PixyPattern.Voronoi)
                c += new Vec2(Rng.Hash(k, 3, s.Seed) * 2 - 1, Rng.Hash(k, 5, s.Seed) * 2 - 1) * (s.RandomPosition * a);

            var t = s.Fade switch
            {
                FadeMode.Linear => pMax - pMin > 1e-9 ? (c.Dot(dir) - pMin) / (pMax - pMin) : 0,
                FadeMode.Radial => maxDist > 1e-9 ? c.DistanceTo(center) / maxDist : 0,
                FadeMode.Noise => Rng.ValueNoise(c.X / (a * 5), c.Y / (a * 5), s.Seed),
                _ => 0,
            };
            t = Math.Clamp(t, 0, 1);
            var fade = s.Fade == FadeMode.None ? 1 : s.FadeFrom + (s.FadeTo - s.FadeFrom) * t;
            var rnd = 1 + s.RandomSize * (Rng.Hash(k, 7, s.Seed) * 2 - 1);
            var factor = Math.Max(0, s.Scale * fade * rnd);
            if (factor <= 1e-6) continue;
            var rot = MathUtil.ToRadians(s.RotationDeg + s.RandomRotationDeg * (Rng.Hash(k, 11, s.Seed) * 2 - 1)) + bc.Angle;

            Polygon2 shape;
            if (s.Pattern == PixyPattern.Voronoi)
            {
                // Nervura de largura constante entre as células; depois escala pelo fade.
                var rib = (1 - ratio) * a * 0.5;
                var inner = bc.Shape.Offset(-rib / 2);
                if (inner.IsEmpty) continue;
                var cc = inner.Centroid;
                shape = inner.Transform(p => cc + (p - cc).Rotate(rot) * Math.Min(factor, 1));
                c = cc;
            }
            else
            {
                var sc = ratio * factor;
                shape = bc.Shape.Transform(p => c + (p - bc.Center).Rotate(rot) * sc);
            }
            if (shape.IsEmpty) continue;
            var sb = shape.Bounds;
            if (Math.Max(sb.Width, sb.Height) < s.MinSize) continue;
            if (!Inside(inset, shape)) continue;
            result.Add(new PixyCell { Shape = shape.EnsureCounterClockwise(), Center = c, Factor = factor, Random01 = Rng.Hash(k, 23, s.Seed), Index = result.Count });
        }
        return result;
    }

    private static bool Inside(Region2 region, Polygon2 shape)
    {
        foreach (var p in shape) if (!region.Contains(p)) return false;
        foreach (var h in region.Holes)
        {
            if (shape.Contains(h.Centroid)) return false;
            foreach (var hp in h) if (shape.Contains(hp)) return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- malhas

    private static IEnumerable<Base> Tiling(PixySettings s, Rect2 b, double a)
    {
        var pad = a * 2;
        switch (s.Pattern)
        {
            case PixyPattern.Hexagonal:
            case PixyPattern.Circles:
            {
                var dy = a * Math.Sqrt(3) / 2;
                var r = a / Math.Sqrt(3);
                for (int j = 0, nj = (int)Math.Ceiling((b.Height + 2 * pad) / dy); j <= nj; j++)
                    for (int i = 0, ni = (int)Math.Ceiling((b.Width + 2 * pad) / a); i <= ni; i++)
                    {
                        var c = new Vec2(b.MinX - pad + i * a + ((j & 1) != 0 ? a / 2 : 0), b.MinY - pad + j * dy);
                        yield return new Base(c, s.Pattern == PixyPattern.Hexagonal ? Polygon2.Regular(6, r, c, Math.PI / 2) : Polygon2.Circle(c, a / 2, 28), 0);
                    }
                break;
            }
            case PixyPattern.Diamonds:
            {
                var h = a / 2;
                for (int j = 0, nj = (int)Math.Ceiling((b.Height + 2 * pad) / h); j <= nj; j++)
                    for (int i = 0, ni = (int)Math.Ceiling((b.Width + 2 * pad) / a); i <= ni; i++)
                    {
                        var c = new Vec2(b.MinX - pad + i * a + ((j & 1) != 0 ? a / 2 : 0), b.MinY - pad + j * h);
                        yield return new Base(c, new Polygon2(new[] { c + new Vec2(a / 2, 0), c + new Vec2(0, a / 2), c + new Vec2(-a / 2, 0), c + new Vec2(0, -a / 2) }), 0);
                    }
                break;
            }
            case PixyPattern.Triangles:
            {
                var h = a * Math.Sqrt(3) / 2;
                for (int j = 0, nj = (int)Math.Ceiling((b.Height + 2 * pad) / h); j <= nj; j++)
                    for (int i = 0, ni = (int)Math.Ceiling((b.Width + 2 * pad) / (a / 2)); i <= ni; i++)
                    {
                        var up = ((i + j) & 1) == 0;
                        var x = b.MinX - pad + i * a / 2;
                        var y0 = b.MinY - pad + j * h;
                        var tri = up
                            ? new[] { new Vec2(x - a / 2, y0), new Vec2(x + a / 2, y0), new Vec2(x, y0 + h) }
                            : new[] { new Vec2(x - a / 2, y0 + h), new Vec2(x, y0), new Vec2(x + a / 2, y0 + h) };
                        var poly = new Polygon2(tri);
                        yield return new Base(poly.Centroid, poly, 0);
                    }
                break;
            }
            case PixyPattern.Bricks:
            {
                var w = a * 2;
                for (int j = 0, nj = (int)Math.Ceiling((b.Height + 2 * pad) / a); j <= nj; j++)
                    for (int i = 0, ni = (int)Math.Ceiling((b.Width + 2 * pad) / w); i <= ni; i++)
                    {
                        var c = new Vec2(b.MinX - pad + i * w + ((j & 1) != 0 ? w / 2 : 0), b.MinY - pad + j * a);
                        yield return new Base(c, Polygon2.Rectangle(c, w, a, 0), 0);
                    }
                break;
            }
            default: // Squares
            {
                for (int j = 0, nj = (int)Math.Ceiling((b.Height + 2 * pad) / a); j <= nj; j++)
                    for (int i = 0, ni = (int)Math.Ceiling((b.Width + 2 * pad) / a); i <= ni; i++)
                    {
                        var c = new Vec2(b.MinX - pad + (i + 0.5) * a, b.MinY - pad + (j + 0.5) * a);
                        yield return new Base(c, Polygon2.Rectangle(c, a, a, 0), 0);
                    }
                break;
            }
        }
    }

    private static IEnumerable<Base> Waves(PixySettings s, Rect2 b, double a)
    {
        var amp = s.WaveAmplitude * a;
        var lambda = Math.Max(1, s.WaveLength) * a;
        for (int j = -1, nj = (int)Math.Ceiling(b.Height / a) + 1; j <= nj; j++)
            for (int i = -1, ni = (int)Math.Ceiling(b.Width / a) + 1; i <= ni; i++)
            {
                var x = b.MinX + (i + 0.5) * a;
                var y = b.MinY + (j + 0.5) * a + amp * Math.Sin(2 * Math.PI * x / lambda);
                var slope = Math.Atan(amp * 2 * Math.PI / lambda * Math.Cos(2 * Math.PI * x / lambda));
                var c = new Vec2(x, y);
                yield return new Base(c, Capsule(c, a, a * 0.45), slope);
            }
    }

    private static Polygon2 Capsule(Vec2 c, double length, double width)
    {
        var r = width / 2;
        var half = Math.Max(0, length / 2 - r);
        var pts = new List<Vec2>();
        for (var k = 0; k <= 8; k++)
        {
            var t = -Math.PI / 2 + Math.PI * k / 8;
            pts.Add(c + new Vec2(half + r * Math.Cos(t), r * Math.Sin(t)));
        }
        for (var k = 0; k <= 8; k++)
        {
            var t = Math.PI / 2 + Math.PI * k / 8;
            pts.Add(c + new Vec2(-half + r * Math.Cos(t), r * Math.Sin(t)));
        }
        return new Polygon2(pts);
    }

    public static Polygon2 Star(Vec2 c, double outer, double innerRatio, int points, double rotation)
    {
        var n = Math.Clamp(points, 3, 24);
        var pts = new List<Vec2>(n * 2);
        for (var i = 0; i < n * 2; i++)
        {
            var r = i % 2 == 0 ? outer : outer * Math.Clamp(innerRatio, 0.1, 0.95);
            var t = rotation + Math.PI / 2 + i * Math.PI / n;
            pts.Add(c + new Vec2(r * Math.Cos(t), r * Math.Sin(t)));
        }
        return new Polygon2(pts);
    }

    /// <summary>Estrelas em anéis concêntricos a partir do centro (explosão radial).</summary>
    private static IEnumerable<Base> StarBurst(PixySettings s, Rect2 b, double a)
    {
        var center = new Vec2(b.MinX + b.Width * s.CenterU, b.MinY + b.Height * s.CenterV);
        var maxR = new[] { b.Min, b.Max, new Vec2(b.MinX, b.MaxY), new Vec2(b.MaxX, b.MinY) }.Max(p => p.DistanceTo(center)) + a;
        yield return new Base(center, Star(center, a / 2, s.StarInnerRatio, s.StarPoints, 0), 0);
        for (var k = 1; k * a <= maxR; k++)
        {
            var r = k * a;
            var n = Math.Max(6, (int)Math.Round(2 * Math.PI * r / a));
            for (var i = 0; i < n; i++)
            {
                var t = 2 * Math.PI * i / n + (k % 2) * Math.PI / n;
                var c = center + new Vec2(Math.Cos(t), Math.Sin(t)) * r;
                yield return new Base(c, Star(c, a / 2, s.StarInnerRatio, s.StarPoints, 0), t - Math.PI / 2);
            }
        }
    }

    /// <summary>Diagrama de Voronoi de sementes em grade com perturbação (células por semiplanos).</summary>
    private static IEnumerable<Base> Voronoi(PixySettings s, Rect2 b, double a)
    {
        var pad = a * 2;
        var seeds = new List<Vec2>();
        var nx = (int)Math.Ceiling((b.Width + 2 * pad) / a);
        var ny = (int)Math.Ceiling((b.Height + 2 * pad) / a);
        var jitter = 0.45 + 0.5 * Math.Clamp(s.RandomPosition, 0, 0.5);
        for (var j = 0; j <= ny; j++)
            for (var i = 0; i <= nx; i++)
                seeds.Add(new Vec2(b.MinX - pad + (i + 0.5 + (Rng.Hash(i, j, s.Seed + 101) * 2 - 1) * jitter) * a,
                    b.MinY - pad + (j + 0.5 + (Rng.Hash(i, j, s.Seed + 202) * 2 - 1) * jitter) * a));
        var grid = new Dictionary<(int, int), List<int>>();
        (int, int) Key(Vec2 p) => ((int)Math.Floor((p.X - b.MinX) / a), (int)Math.Floor((p.Y - b.MinY) / a));
        for (var i = 0; i < seeds.Count; i++)
        {
            var key = Key(seeds[i]);
            if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<int>();
            l.Add(i);
        }
        var box = new Rect2(b.MinX - pad, b.MinY - pad, b.MaxX + pad, b.MaxY + pad).ToPolygon();
        for (var i = 0; i < seeds.Count; i++)
        {
            var si = seeds[i];
            if (si.X < b.MinX - a || si.X > b.MaxX + a || si.Y < b.MinY - a || si.Y > b.MaxY + a) continue;
            var cell = box;
            var (gx, gy) = Key(si);
            for (var dx = -3; dx <= 3 && !cell.IsEmpty; dx++)
                for (var dy = -3; dy <= 3 && !cell.IsEmpty; dy++)
                {
                    if (!grid.TryGetValue((gx + dx, gy + dy), out var l)) continue;
                    foreach (var jdx in l)
                    {
                        if (jdx == i) continue;
                        var sj = seeds[jdx];
                        cell = ClipHalfPlane(cell, (si + sj) * 0.5, sj - si);
                        if (cell.IsEmpty) break;
                    }
                }
            if (!cell.IsEmpty) yield return new Base(cell.Centroid, cell, 0);
        }
    }

    /// <summary>Mantém a parte do polígono convexo onde (p − m)·n ≤ 0.</summary>
    public static Polygon2 ClipHalfPlane(Polygon2 poly, Vec2 m, Vec2 n)
    {
        var res = new List<Vec2>();
        var cnt = poly.Count;
        for (var i = 0; i < cnt; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % cnt];
            var dp = (p - m).Dot(n);
            var dq = (q - m).Dot(n);
            if (dp <= 0) res.Add(p);
            if ((dp < 0 && dq > 0) || (dp > 0 && dq < 0))
            {
                var t = dp / (dp - dq);
                res.Add(p + (q - p) * t);
            }
        }
        return res.Count >= 3 ? new Polygon2(res) : Polygon2.Empty;
    }
}
