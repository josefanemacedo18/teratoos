using System.Globalization;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Facade;

public enum FinOrientation
{
    Vertical,
    Horizontal,
}

public enum FinDistribution
{
    /// <summary>Espaçamento fixo, distribuição centralizada.</summary>
    BySpacing,
    /// <summary>Quantidade fixa, espaçamento calculado.</summary>
    ByCount,
    /// <summary>Espaçamento aproximado, ajustado para fechar exatamente nas margens.</summary>
    FitSpacing,
}

public enum FinSide
{
    Left,
    Right,
}

public enum FinPivot
{
    Center,
    FacadeEdge,
    OuterEdge,
}

public enum VariationMode
{
    None,
    Linear,
    Sine,
    Random,
    Attractor,
    Formula,
}

/// <summary>Parâmetros do gerador de aletas e brises (pés e graus).</summary>
public sealed class BriseSettings
{
    public FinOrientation Orientation { get; set; } = FinOrientation.Vertical;
    public double FinDepth { get; set; } = U.Mm(300);
    public double FinThickness { get; set; } = U.Mm(40);
    public FinDistribution Distribution { get; set; } = FinDistribution.BySpacing;
    public double Spacing { get; set; } = U.Mm(300);
    public int Count { get; set; } = 20;
    public double StartMargin { get; set; }
    public double EndMargin { get; set; }
    public double BottomElevation { get; set; }
    public double TopElevation { get; set; } = U.M(3);
    public double DistanceFromFacade { get; set; } = U.Mm(100);
    public FinSide Side { get; set; } = FinSide.Left;
    public FinPivot Pivot { get; set; } = FinPivot.Center;
    public double RotationDeg { get; set; }
    public VariationMode Variation { get; set; } = VariationMode.None;
    public double VariationMinDeg { get; set; } = -45;
    public double VariationMaxDeg { get; set; } = 45;
    public double SinePeriods { get; set; } = 2;
    public double AttractorPosition { get; set; } = 0.5;
    public double AttractorRadius { get; set; } = 0.3;
    public string Formula { get; set; } = "0.5 + 0.5*sin(t*360)";
    public int Seed { get; set; } = 3;
    public bool VaryDepth { get; set; }
    public double DepthMin { get; set; } = U.Mm(150);
    public double DepthMax { get; set; } = U.Mm(400);

    /// <summary>Sequência de materiais por aleta, ex.: "1,1,2" (cada número é um conjunto de material).</summary>
    public string MaterialSequence { get; set; } = "1";
}

public sealed class FinPlacement
{
    public int Index { get; set; }
    public double Position { get; set; }
    public double RotationDeg { get; set; }
    public double Depth { get; set; }
    public double Thickness { get; set; }
    public double Length { get; set; }
    public int MaterialIndex { get; set; }
    public double VariationValue { get; set; }
    public OrientedBox Box { get; set; }
}

public sealed class BriseResult
{
    public List<FinPlacement> Fins { get; } = new();
    public double TotalLength => Fins.Sum(f => f.Length);
    public double TotalVolume => Fins.Sum(f => f.Length * f.Depth * f.Thickness);

    public TableData Quantities()
    {
        var t = new TableData(Loc.T("Quantitativo de aletas", "Fin quantities"));
        t.AddColumn(Loc.T("Conjunto", "Set"), ColumnKind.Integer)
         .AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer)
         .AddColumn(Loc.T("Comprimento total (m)", "Total length (m)"), ColumnKind.Number)
         .AddColumn(Loc.T("Área de face (m²)", "Face area (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3);
        foreach (var g in Fins.GroupBy(f => f.MaterialIndex).OrderBy(g => g.Key))
        {
            t.AddRow(g.Key + 1, g.Count(),
                U.ToM(g.Sum(f => f.Length)),
                U.SqFtToM2(g.Sum(f => f.Length * f.Depth)),
                U.CuFtToM3(g.Sum(f => f.Length * f.Depth * f.Thickness)));
        }
        return t;
    }
}

public static class BriseGenerator
{
    /// <summary>
    /// Gera aletas ao longo de um caminho em planta.
    /// </summary>
    /// <param name="path">Linha da fachada em planta (ex.: linha de locação da parede).</param>
    /// <param name="facadeRegion">Opcional: contorno da fachada em coordenadas (s, z) para ajustar as aletas aos limites.</param>
    public static BriseResult Generate(BriseSettings s, IPath2 path, Region2? facadeRegion = null, int hardLimit = 20000)
    {
        if (s.FinDepth <= 0 || s.FinThickness <= 0)
            throw new ArgumentException(Loc.T("Largura e espessura da aleta devem ser maiores que zero.", "Fin depth and thickness must be greater than zero."));
        var result = new BriseResult();
        var materials = ParseSequence(s.MaterialSequence);
        Expression? formula = s.Variation == VariationMode.Formula ? Expression.Parse(s.Formula) : null;
        var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (s.Orientation == FinOrientation.Vertical)
        {
            var positions = Distribute(s.StartMargin, path.Length - s.EndMargin, s);
            var n = positions.Count;
            for (var k = 0; k < n && result.Fins.Count < hardLimit; k++)
            {
                var sPos = positions[k];
                var tNorm = n <= 1 ? 0.5 : (double)k / (n - 1);
                var v = Variation(s, k, n, tNorm, U.ToM(sPos), formula, vars);
                var rot = s.RotationDeg + (s.Variation == VariationMode.None ? 0 : MathUtil.Lerp(s.VariationMinDeg, s.VariationMaxDeg, v));
                var depth = s.VaryDepth ? MathUtil.Lerp(s.DepthMin, s.DepthMax, v) : s.FinDepth;

                var spans = facadeRegion == null
                    ? new List<(double Z0, double Z1)> { (s.BottomElevation, s.TopElevation) }
                    : facadeRegion.ClipSegment(new Segment2(new Vec2(sPos, s.BottomElevation), new Vec2(sPos, s.TopElevation)))
                        .Select(seg => (Math.Min(seg.A.Y, seg.B.Y), Math.Max(seg.A.Y, seg.B.Y))).ToList();

                foreach (var (z0, z1) in spans)
                {
                    if (z1 - z0 < Tolerance.ShortCurve) continue;
                    var p = path.PointAt(sPos);
                    var t = path.TangentAt(sPos);
                    var nrm = s.Side == FinSide.Left ? t.Perp : -t.Perp;
                    var innerDist = s.DistanceFromFacade;
                    var pivotDist = s.Pivot switch
                    {
                        FinPivot.FacadeEdge => innerDist,
                        FinPivot.OuterEdge => innerDist + depth,
                        _ => innerDist + depth / 2,
                    };
                    var pivot = p + nrm * pivotDist;
                    var c0 = p + nrm * (innerDist + depth / 2);
                    var a = MathUtil.ToRadians(rot);
                    var c = c0.RotateAround(pivot, a);
                    var depthDir = nrm.Rotate(a);
                    var thickDir = t.Rotate(a);
                    var center = new Vec3(c.X, c.Y, (z0 + z1) / 2);
                    var box = OrientedBox.UprightFromCenter(center, depthDir.ToVec3(), depth, thickDir.ToVec3(), s.FinThickness, z1 - z0);
                    result.Fins.Add(new FinPlacement
                    {
                        Index = result.Fins.Count,
                        Position = sPos,
                        RotationDeg = rot,
                        Depth = depth,
                        Thickness = s.FinThickness,
                        Length = z1 - z0,
                        MaterialIndex = materials[k % materials.Count],
                        VariationValue = v,
                        Box = box,
                    });
                }
            }
        }
        else
        {
            var heights = Distribute(s.BottomElevation, s.TopElevation, s);
            var n = heights.Count;
            var spansAlong = (path as PolylinePath)?.StraightSpans().ToList() ?? new List<(double, double)> { (0, path.Length) };
            for (var k = 0; k < n && result.Fins.Count < hardLimit; k++)
            {
                var z = heights[k];
                var tNorm = n <= 1 ? 0.5 : (double)k / (n - 1);
                var v = Variation(s, k, n, tNorm, U.ToM(z), formula, vars);
                var rot = s.RotationDeg + (s.Variation == VariationMode.None ? 0 : MathUtil.Lerp(s.VariationMinDeg, s.VariationMaxDeg, v));
                var depth = s.VaryDepth ? MathUtil.Lerp(s.DepthMin, s.DepthMax, v) : s.FinDepth;

                var ranges = new List<(double A, double B)>();
                if (facadeRegion == null)
                    ranges.Add((s.StartMargin, path.Length - s.EndMargin));
                else
                    ranges.AddRange(facadeRegion.ClipSegment(new Segment2(new Vec2(s.StartMargin, z), new Vec2(path.Length - s.EndMargin, z)))
                        .Select(seg => (Math.Min(seg.A.X, seg.B.X), Math.Max(seg.A.X, seg.B.X))));

                foreach (var (ra, rb) in ranges)
                {
                    foreach (var (sa0, sb0) in spansAlong)
                    {
                        var sa = Math.Max(sa0, ra);
                        var sb = Math.Min(sb0, rb);
                        if (sb - sa < Tolerance.ShortCurve) continue;
                        var pa = path.PointAt(sa);
                        var t = path.TangentAt((sa + sb) / 2);
                        var nrm = s.Side == FinSide.Left ? t.Perp : -t.Perp;
                        var t3 = t.ToVec3();
                        var n3 = nrm.ToVec3();
                        var up = Vec3.UnitZ;
                        var a = MathUtil.ToRadians(rot);
                        var innerDist = s.DistanceFromFacade;
                        var pivotDist = s.Pivot switch
                        {
                            FinPivot.FacadeEdge => innerDist,
                            FinPivot.OuterEdge => innerDist + depth,
                            _ => innerDist + depth / 2,
                        };
                        // Seção no plano (n, z): gira o centro em torno do pivô.
                        var c0 = new Vec2(innerDist + depth / 2, z);
                        var pv = new Vec2(pivotDist, z);
                        var c = c0.RotateAround(pv, a);
                        var depthDir = n3 * Math.Cos(a) + up * Math.Sin(a);
                        var mid = pa.ToVec3() + t3 * ((sb - sa) / 2) + n3 * c.X + up * c.Y;
                        var box = OrientedBox.FromCenter(mid, t3, depthDir, sb - sa, depth, s.FinThickness);
                        result.Fins.Add(new FinPlacement
                        {
                            Index = result.Fins.Count,
                            Position = z,
                            RotationDeg = rot,
                            Depth = depth,
                            Thickness = s.FinThickness,
                            Length = sb - sa,
                            MaterialIndex = materials[k % materials.Count],
                            VariationValue = v,
                            Box = box,
                        });
                    }
                }
            }
        }
        return result;
    }

    /// <summary>Distribui posições entre a e b conforme o modo de distribuição.</summary>
    public static List<double> Distribute(double a, double b, BriseSettings s)
    {
        var list = new List<double>();
        var avail = b - a;
        if (avail < -1e-9) return list;
        switch (s.Distribution)
        {
            case FinDistribution.ByCount:
                var n = Math.Max(1, s.Count);
                if (n == 1) list.Add((a + b) / 2);
                else for (var i = 0; i < n; i++) list.Add(a + avail * i / (n - 1));
                break;
            case FinDistribution.FitSpacing:
                var m = Math.Max(1, (int)Math.Round(avail / Math.Max(1e-9, s.Spacing)));
                for (var i = 0; i <= m; i++) list.Add(a + avail * i / m);
                break;
            default:
                var sp = Math.Max(1e-9, s.Spacing);
                var k = (int)Math.Floor(avail / sp + 1e-9) + 1;
                var start = a + (avail - (k - 1) * sp) / 2;
                for (var i = 0; i < k; i++) list.Add(start + i * sp);
                break;
        }
        return list;
    }

    private static double Variation(BriseSettings s, int i, int n, double t, double posMeters, Expression? formula, Dictionary<string, double> vars)
    {
        double v = s.Variation switch
        {
            VariationMode.Linear => t,
            VariationMode.Sine => 0.5 + 0.5 * Math.Sin(2 * Math.PI * s.SinePeriods * t),
            VariationMode.Random => Rng.Hash(i, 0, s.Seed),
            VariationMode.Attractor => 1 - MathUtil.Clamp01(Math.Abs(t - s.AttractorPosition) / Math.Max(1e-6, s.AttractorRadius)),
            VariationMode.Formula => EvalFormula(formula!, vars, i, n, t, posMeters, s.Seed),
            _ => 0,
        };
        return MathUtil.Clamp01(double.IsNaN(v) ? 0 : v);
    }

    private static double EvalFormula(Expression e, Dictionary<string, double> vars, int i, int n, double t, double pos, int seed)
    {
        vars["t"] = t;
        vars["i"] = i;
        vars["n"] = n;
        vars["s"] = pos;
        vars["r"] = Rng.Hash(i, 0, seed);
        return e.Evaluate(vars);
    }

    public static List<int> ParseSequence(string? seq)
    {
        var list = new List<int>();
        foreach (var part in (seq ?? "1").Split(new[] { ',', ';', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 1)
                list.Add(v - 1);
        if (list.Count == 0) list.Add(0);
        return list;
    }
}
