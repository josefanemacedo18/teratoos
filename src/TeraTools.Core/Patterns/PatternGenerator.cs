using TeraTools.Core.Geometry;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Patterns;

public enum PatternLayout
{
    Linear,
    Rectangular,
    Staggered,
    Diamond,
    Triangular,
    Hexagonal,
    Radial,
    Spiral,
    Random,
}

public enum ModuleShape
{
    Rectangle,
    Circle,
    Triangle,
    Hexagon,
    Diamond,
}

public enum GradientMode
{
    None,
    Linear,
    Radial,
    Noise,
    Formula,
    Image,
}

public enum ClipMode
{
    /// <summary>Centro do módulo dentro da região.</summary>
    CenterInside,
    /// <summary>Módulo inteiro dentro da região.</summary>
    FullyInside,
    /// <summary>Qualquer parte do módulo dentro da região.</summary>
    Touching,
}

public enum PatternAnchor
{
    RegionCenter,
    RegionCorner,
    Origin,
}

/// <summary>Parâmetros do gerador de padrões (todas as medidas em pés; ângulos em graus).</summary>
public sealed class PatternSettings
{
    public PatternLayout Layout { get; set; } = PatternLayout.Rectangular;
    public ModuleShape Shape { get; set; } = ModuleShape.Rectangle;
    public double SpacingX { get; set; } = U.Mm(300);
    public double SpacingY { get; set; } = U.Mm(300);
    public double ModuleWidth { get; set; } = U.Mm(250);
    public double ModuleHeight { get; set; } = U.Mm(250);
    public double Depth { get; set; } = U.Mm(20);
    public double GridRotationDeg { get; set; }
    public double ModuleRotationDeg { get; set; }
    public PatternAnchor Anchor { get; set; } = PatternAnchor.RegionCenter;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public int MaxCountX { get; set; }
    public int MaxCountY { get; set; }

    // Radial / espiral
    public int RadialRings { get; set; } = 5;
    public double RingSpacing { get; set; } = U.Mm(300);
    public double InnerRadius { get; set; }
    public int ModulesPerRing { get; set; }
    public double StartAngleDeg { get; set; }
    public double SweepAngleDeg { get; set; } = 360;
    public bool AlignTangent { get; set; } = true;
    public int SpiralCount { get; set; } = 400;

    // Variação aleatória controlada
    public int Seed { get; set; } = 1;
    public double JitterPosition { get; set; }
    public double JitterRotationDeg { get; set; }
    public double JitterScale { get; set; }
    public double RemoveProbability { get; set; }

    // Gradiente / atrator
    public GradientMode Gradient { get; set; } = GradientMode.None;
    public double GradientAngleDeg { get; set; }
    public double AttractorX { get; set; }
    public double AttractorY { get; set; }
    public double AttractorRadius { get; set; } = U.M(3);
    public double NoiseScale { get; set; } = U.M(2);
    public string Formula { get; set; } = "u";
    public bool InvertGradient { get; set; }

    public bool ApplyToScale { get; set; } = true;
    public double ScaleMin { get; set; } = 0.3;
    public double ScaleMax { get; set; } = 1.0;
    public bool ApplyToRotation { get; set; }
    public double RotationMinDeg { get; set; }
    public double RotationMaxDeg { get; set; } = 90;
    public bool ApplyToDepth { get; set; }
    public double DepthMin { get; set; } = U.Mm(10);
    public double DepthMax { get; set; } = U.Mm(100);
    public bool ApplyToOpening { get; set; }
    public double OpeningMin { get; set; }
    public double OpeningMax { get; set; } = 0.8;
    public bool ApplyToDensity { get; set; }
    public double DensityMin { get; set; } = 0.2;
    public double DensityMax { get; set; } = 1.0;

    // Recorte
    public ClipMode Clip { get; set; } = ClipMode.FullyInside;
    public double EdgeMargin { get; set; }
}

/// <summary>Um módulo gerado do padrão.</summary>
public sealed class PatternCell
{
    public int Index { get; set; }
    public int I { get; set; }
    public int J { get; set; }
    public Vec2 Center { get; set; }
    public double RotationRad { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }
    public double Opening { get; set; }
    public double Value { get; set; }
    public bool Flipped { get; set; }
    public ModuleShape Shape { get; set; }
    public Polygon2 Outline { get; set; } = Polygon2.Empty;
}

public static class PatternGenerator
{
    /// <summary>
    /// Gera os módulos do padrão dentro de uma região 2D (coordenadas locais da superfície).
    /// </summary>
    /// <param name="region">Região de aplicação; se nula, usa MaxCountX × MaxCountY a partir da origem.</param>
    /// <param name="imageValue">Para gradiente por imagem: brilho (0..1) em (u, v).</param>
    /// <param name="hardLimit">Interrompe a geração ao atingir este número de módulos.</param>
    public static List<PatternCell> Generate(PatternSettings s, Region2? region, Func<double, double, double>? imageValue = null, int hardLimit = 100000)
    {
        if (s.SpacingX <= 1e-6 || s.SpacingY <= 1e-6)
            throw new ArgumentException(Localization.Loc.T("O espaçamento deve ser maior que zero.", "Spacing must be greater than zero."));

        region ??= DefaultRegion(s);
        var bounds = region.Bounds;
        var origin = s.Anchor switch
        {
            PatternAnchor.RegionCenter => bounds.Center,
            PatternAnchor.RegionCorner => bounds.Min,
            _ => Vec2.Zero,
        } + new Vec2(s.OffsetX, s.OffsetY);

        var gridRot = MathUtil.ToRadians(s.GridRotationDeg);
        var candidates = s.Layout switch
        {
            PatternLayout.Radial => RadialCandidates(s, origin, gridRot),
            PatternLayout.Spiral => SpiralCandidates(s, origin, bounds),
            PatternLayout.Random => PoissonCandidates(s, bounds),
            _ => GridCandidates(s, region, origin, gridRot),
        };

        Expression? formula = null;
        if (s.Gradient == GradientMode.Formula)
            formula = Expression.Parse(string.IsNullOrWhiteSpace(s.Formula) ? "u" : s.Formula);
        var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var attractor = new Vec2(s.AttractorX, s.AttractorY);
        var inset = s.EdgeMargin > 1e-9 ? region.Offset(-s.EdgeMargin) : region;
        var linDir = Vec2.FromAngle(MathUtil.ToRadians(s.GradientAngleDeg));
        var (projMin, projMax) = ProjectionRange(bounds, linDir);

        var cells = new List<PatternCell>();
        var index = 0;
        foreach (var c in candidates)
        {
            if (cells.Count >= hardLimit) break;
            var p = c.Center;

            if (s.JitterPosition > 0)
            {
                var jx = (Rng.Hash(c.I, c.J, s.Seed + 11) * 2 - 1) * s.JitterPosition * s.SpacingX;
                var jy = (Rng.Hash(c.I, c.J, s.Seed + 12) * 2 - 1) * s.JitterPosition * s.SpacingY;
                p += new Vec2(jx, jy).Rotate(gridRot);
            }

            var u = bounds.Width > 1e-12 ? (p.X - bounds.MinX) / bounds.Width : 0.5;
            var v = bounds.Height > 1e-12 ? (p.Y - bounds.MinY) / bounds.Height : 0.5;

            double g;
            switch (s.Gradient)
            {
                case GradientMode.Linear:
                    g = MathUtil.InverseLerp(projMin, projMax, p.Dot(linDir));
                    break;
                case GradientMode.Radial:
                    g = 1 - MathUtil.Clamp01(p.DistanceTo(attractor) / Math.Max(1e-9, s.AttractorRadius));
                    break;
                case GradientMode.Noise:
                    g = Rng.ValueNoise(p.X / Math.Max(1e-9, s.NoiseScale), p.Y / Math.Max(1e-9, s.NoiseScale), s.Seed);
                    break;
                case GradientMode.Formula:
                    vars["x"] = U.ToM(p.X);
                    vars["y"] = U.ToM(p.Y);
                    vars["u"] = u;
                    vars["v"] = v;
                    vars["i"] = c.I;
                    vars["j"] = c.J;
                    vars["n"] = index;
                    vars["d"] = U.ToM(p.DistanceTo(attractor));
                    vars["r"] = Rng.Hash(c.I, c.J, s.Seed + 21);
                    vars["w"] = U.ToM(bounds.Width);
                    vars["h"] = U.ToM(bounds.Height);
                    g = formula!.Evaluate(vars);
                    break;
                case GradientMode.Image:
                    g = imageValue?.Invoke(MathUtil.Clamp01(u), MathUtil.Clamp01(v)) ?? 1;
                    break;
                default:
                    g = 1;
                    break;
            }
            if (double.IsNaN(g)) g = 0;
            g = MathUtil.Clamp01(g);
            if (s.InvertGradient && s.Gradient != GradientMode.None) g = 1 - g;

            // Remoções (aleatória e por densidade)
            if (s.RemoveProbability > 0 && Rng.Hash(c.I, c.J, s.Seed + 31) < s.RemoveProbability) continue;
            if (s.ApplyToDensity && Rng.Hash(c.I, c.J, s.Seed + 32) > MathUtil.Lerp(s.DensityMin, s.DensityMax, g)) continue;

            var scale = s.ApplyToScale ? MathUtil.Lerp(s.ScaleMin, s.ScaleMax, g) : 1.0;
            if (s.JitterScale > 0)
                scale *= 1 + (Rng.Hash(c.I, c.J, s.Seed + 13) * 2 - 1) * s.JitterScale;
            if (scale <= 1e-6) continue;

            var rot = c.BaseRotation + MathUtil.ToRadians(s.ModuleRotationDeg);
            if (s.ApplyToRotation)
                rot += MathUtil.ToRadians(MathUtil.Lerp(s.RotationMinDeg, s.RotationMaxDeg, g));
            if (s.JitterRotationDeg > 0)
                rot += MathUtil.ToRadians((Rng.Hash(c.I, c.J, s.Seed + 14) * 2 - 1) * s.JitterRotationDeg);

            var w = s.ModuleWidth * scale;
            var hgt = s.ModuleHeight * scale;
            var outline = ShapeOutline(s.Shape, p, w, hgt, rot);

            if (!PassesClip(s.Clip, inset, p, outline)) continue;

            cells.Add(new PatternCell
            {
                Index = index++,
                I = c.I,
                J = c.J,
                Center = p,
                RotationRad = rot,
                Width = w,
                Height = hgt,
                Depth = s.ApplyToDepth ? MathUtil.Lerp(s.DepthMin, s.DepthMax, g) : s.Depth,
                Opening = s.ApplyToOpening ? MathUtil.Lerp(s.OpeningMin, s.OpeningMax, g) : 0,
                Value = g,
                Flipped = c.Flipped,
                Shape = s.Shape,
                Outline = outline,
            });
        }
        return cells;
    }

    private static Region2 DefaultRegion(PatternSettings s)
    {
        var nx = Math.Max(1, s.MaxCountX == 0 ? 10 : s.MaxCountX);
        var ny = Math.Max(1, s.MaxCountY == 0 ? (s.Layout == PatternLayout.Linear ? 1 : 10) : s.MaxCountY);
        return new Region2(Polygon2.Rectangle(Vec2.Zero, nx * s.SpacingX, ny * s.SpacingY));
    }

    private static (double Min, double Max) ProjectionRange(Rect2 b, Vec2 dir)
    {
        var pts = new[] { b.Min, new Vec2(b.MaxX, b.MinY), b.Max, new Vec2(b.MinX, b.MaxY) };
        var proj = pts.Select(p => p.Dot(dir)).ToArray();
        return (proj.Min(), proj.Max());
    }

    private readonly record struct Candidate(int I, int J, Vec2 Center, double BaseRotation, bool Flipped);

    private static IEnumerable<Candidate> GridCandidates(PatternSettings s, Region2 region, Vec2 origin, double rot)
    {
        var sx = s.SpacingX;
        var sy = s.SpacingY;
        if (s.Layout == PatternLayout.Hexagonal && Math.Abs(sy - sx) < 1e-9)
            sy = sx * Math.Sqrt(3) / 2; // grade hexagonal regular
        if (s.Layout == PatternLayout.Diamond)
            sy /= 2;

        // Limites da região no sistema da grade (sem rotação).
        var gridPts = region.Outer.Select(p => (p - origin).Rotate(-rot)).ToList();
        var gb = Rect2.FromPoints(gridPts);
        var margin = Math.Max(s.ModuleWidth, s.ModuleHeight);
        var stepX = s.Layout == PatternLayout.Triangular ? sx / 2 : sx;

        int i0 = (int)Math.Floor((gb.MinX - margin) / stepX) - 1, i1 = (int)Math.Ceiling((gb.MaxX + margin) / stepX) + 1;
        int j0 = (int)Math.Floor((gb.MinY - margin) / sy) - 1, j1 = (int)Math.Ceiling((gb.MaxY + margin) / sy) + 1;

        if (s.MaxCountX > 0) { i0 = 0; i1 = s.MaxCountX - 1; }
        if (s.MaxCountY > 0) { j0 = 0; j1 = s.MaxCountY - 1; }
        if (s.Layout == PatternLayout.Linear && s.MaxCountY == 0) { j0 = 0; j1 = 0; }

        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                Vec2 q;
                var flipped = false;
                var baseRot = rot;
                switch (s.Layout)
                {
                    case PatternLayout.Staggered:
                    case PatternLayout.Diamond:
                    case PatternLayout.Hexagonal:
                        q = new Vec2(i * sx + ((j & 1) != 0 ? sx / 2 : 0), j * sy);
                        break;
                    case PatternLayout.Triangular:
                        flipped = ((i + j) & 1) != 0;
                        var yb = j * sy - sy / 2;
                        var yt = j * sy + sy / 2;
                        q = new Vec2(i * sx / 2, flipped ? yt - sy / 3 : yb + sy / 3);
                        if (flipped) baseRot += Math.PI;
                        break;
                    default:
                        q = new Vec2(i * sx, j * sy);
                        break;
                }
                yield return new Candidate(i, j, q.Rotate(rot) + origin, baseRot, flipped);
            }
        }
    }

    private static IEnumerable<Candidate> RadialCandidates(PatternSettings s, Vec2 center, double rot)
    {
        var sweep = MathUtil.ToRadians(Math.Clamp(s.SweepAngleDeg, 1, 360));
        var start = MathUtil.ToRadians(s.StartAngleDeg) + rot;
        var full = s.SweepAngleDeg >= 359.999;
        for (var k = 0; k < Math.Max(1, s.RadialRings); k++)
        {
            var r = s.InnerRadius + k * s.RingSpacing;
            if (r < 1e-9)
            {
                yield return new Candidate(0, k, center, rot, false);
                continue;
            }
            var n = s.ModulesPerRing > 0
                ? s.ModulesPerRing
                : Math.Max(1, (int)Math.Round(r * sweep / s.SpacingX));
            for (var m = 0; m < n; m++)
            {
                var t = full ? (double)m / n : (n == 1 ? 0.5 : (double)m / (n - 1));
                var a = start + t * sweep;
                var p = center + Vec2.FromAngle(a, r);
                var baseRot = s.AlignTangent ? a + Math.PI / 2 : rot;
                yield return new Candidate(m, k, p, baseRot, false);
            }
        }
    }

    private static IEnumerable<Candidate> SpiralCandidates(PatternSettings s, Vec2 center, Rect2 bounds)
    {
        // Espiral de Fermat (filotaxia): distribuição orgânica de densidade quase constante.
        var c = s.SpacingX * Math.Sqrt(Math.Sqrt(3) / (2 * Math.PI));
        var maxR = new[] { bounds.Min, bounds.Max, new Vec2(bounds.MinX, bounds.MaxY), new Vec2(bounds.MaxX, bounds.MinY) }
            .Max(p => p.DistanceTo(center)) + s.SpacingX;
        var golden = MathUtil.ToRadians(137.50776405);
        var count = s.SpiralCount > 0 ? s.SpiralCount : int.MaxValue;
        for (var k = 0; k < count; k++)
        {
            var r = c * Math.Sqrt(k + 0.5);
            if (r > maxR) yield break;
            var a = k * golden;
            yield return new Candidate(k, 0, center + Vec2.FromAngle(a, r), s.AlignTangent ? a + Math.PI / 2 : 0, false);
        }
    }

    private static IEnumerable<Candidate> PoissonCandidates(PatternSettings s, Rect2 bounds)
    {
        // Amostragem de Poisson (Bridson): pontos aleatórios com distância mínima = espaçamento.
        var r = s.SpacingX;
        var cell = r / Math.Sqrt(2);
        var gw = Math.Max(1, (int)Math.Ceiling(bounds.Width / cell));
        var gh = Math.Max(1, (int)Math.Ceiling(bounds.Height / cell));
        var grid = new int[gw * gh];
        Array.Fill(grid, -1);
        var pts = new List<Vec2>();
        var active = new List<int>();
        var rng = new Rng(s.Seed);

        void Add(Vec2 p)
        {
            pts.Add(p);
            active.Add(pts.Count - 1);
            var gx = Math.Clamp((int)((p.X - bounds.MinX) / cell), 0, gw - 1);
            var gy = Math.Clamp((int)((p.Y - bounds.MinY) / cell), 0, gh - 1);
            grid[gy * gw + gx] = pts.Count - 1;
        }

        bool Fits(Vec2 p)
        {
            if (!bounds.Contains(p)) return false;
            var gx = (int)((p.X - bounds.MinX) / cell);
            var gy = (int)((p.Y - bounds.MinY) / cell);
            for (var y = Math.Max(0, gy - 2); y <= Math.Min(gh - 1, gy + 2); y++)
                for (var x = Math.Max(0, gx - 2); x <= Math.Min(gw - 1, gx + 2); x++)
                {
                    var idx = grid[y * gw + x];
                    if (idx >= 0 && pts[idx].DistanceTo(p) < r) return false;
                }
            return true;
        }

        Add(new Vec2(rng.Range(bounds.MinX, bounds.MaxX), rng.Range(bounds.MinY, bounds.MaxY)));
        while (active.Count > 0 && pts.Count < 200000)
        {
            var ai = rng.Next(active.Count);
            var basePt = pts[active[ai]];
            var found = false;
            for (var k = 0; k < 30; k++)
            {
                var a = rng.Range(0, 2 * Math.PI);
                var d = rng.Range(r, 2 * r);
                var p = basePt + Vec2.FromAngle(a, d);
                if (Fits(p))
                {
                    Add(p);
                    found = true;
                    break;
                }
            }
            if (!found) active.RemoveAt(ai);
        }
        for (var k = 0; k < pts.Count; k++)
            yield return new Candidate(k, 0, pts[k], MathUtil.ToRadians(s.GridRotationDeg), false);
    }

    public static Polygon2 ShapeOutline(ModuleShape shape, Vec2 center, double w, double h, double rot)
    {
        switch (shape)
        {
            case ModuleShape.Circle:
                return new Polygon2(Enumerable.Range(0, 24).Select(k =>
                {
                    var a = k * 2 * Math.PI / 24;
                    return center + new Vec2(Math.Cos(a) * w / 2, Math.Sin(a) * h / 2).Rotate(rot);
                }));
            case ModuleShape.Triangle:
                // Triângulo com base horizontal; centro = baricentro.
                return new Polygon2(new[]
                {
                    new Vec2(-w / 2, -h / 3), new Vec2(w / 2, -h / 3), new Vec2(0, 2 * h / 3),
                }.Select(p => center + p.Rotate(rot)));
            case ModuleShape.Hexagon:
                return new Polygon2(Enumerable.Range(0, 6).Select(k =>
                {
                    var a = MathUtil.ToRadians(90 + 60 * k);
                    var rx = w / Math.Sqrt(3);
                    var ry = h / 2;
                    return center + new Vec2(Math.Cos(a) * rx, Math.Sin(a) * ry).Rotate(rot);
                }));
            case ModuleShape.Diamond:
                return new Polygon2(new[]
                {
                    new Vec2(0, -h / 2), new Vec2(w / 2, 0), new Vec2(0, h / 2), new Vec2(-w / 2, 0),
                }.Select(p => center + p.Rotate(rot)));
            default:
                return Polygon2.Rectangle(center, w, h, rot);
        }
    }

    private static bool PassesClip(ClipMode mode, Region2 region, Vec2 center, Polygon2 outline)
    {
        if (region.IsEmpty) return false;
        switch (mode)
        {
            case ClipMode.CenterInside:
                return region.Contains(center);
            case ClipMode.Touching:
                return region.Bounds.Intersects(outline.Bounds) && region.IntersectionArea(outline) > 1e-9;
            default:
                if (!region.Bounds.Contains(outline.Bounds, 1e-6)) return false;
                var a = outline.Area;
                return a > 0 && region.IntersectionArea(outline) >= a * (1 - 1e-6);
        }
    }

    /// <summary>Resumo para pré-visualização: contagem, tamanhos mínimo/máximo.</summary>
    public static (int Count, double MinScale, double MaxScale) Summary(IReadOnlyList<PatternCell> cells, PatternSettings s)
    {
        if (cells.Count == 0) return (0, 0, 0);
        return (cells.Count, cells.Min(c => c.Width / s.ModuleWidth), cells.Max(c => c.Width / s.ModuleWidth));
    }
}
