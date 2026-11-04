using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Structure;

public enum SpaceFrameType
{
    /// <summary>Quadrado sobre quadrado deslocado (malha espacial clássica).</summary>
    SquareOnSquareOffset,
    /// <summary>Quadrado sobre quadrado alinhado (grelha de treliças com montantes).</summary>
    SquareOnSquareAligned,
    /// <summary>Triangular deslocada (tetraédrica).</summary>
    TriangularOffset,
}

public enum DiagonalPattern
{
    /// <summary>Todas as diagonais (no tipo alinhado: contraventamento em X).</summary>
    All,
    /// <summary>Diagonais alternadas (malha mais leve / treliça tipo Warren).</summary>
    Alternate,
}

public enum BarRole
{
    TopChord,
    BottomChord,
    InnerChord,
    Diagonal,
    Vertical,
}

public sealed class SpaceFrameSettings
{
    public SpaceFrameType Type { get; set; } = SpaceFrameType.SquareOnSquareOffset;
    public double ModuleX { get; set; } = U.M(1.5);
    public double ModuleY { get; set; } = U.M(1.5);
    public double Depth { get; set; } = U.M(1.0);
    public int Layers { get; set; } = 2;
    public DiagonalPattern Diagonals { get; set; } = DiagonalPattern.All;
    public double RotationDeg { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double TubeDiameter { get; set; } = U.Mm(60);
    public double NodeDiameter { get; set; } = U.Mm(120);
    public double LengthTolerance { get; set; } = U.Mm(1);
}

public sealed class SpaceNode
{
    public int Id { get; set; }
    public int Layer { get; set; }
    public int I { get; set; }
    public int J { get; set; }
    public Vec2 Plan { get; set; }
    public Vec3 Position { get; set; }
    public int Valence { get; set; }
}

public sealed class SpaceBar
{
    public int Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public BarRole Role { get; set; }
    public Vec3 Start { get; set; }
    public Vec3 End { get; set; }
    public double Length => Start.DistanceTo(End);
}

public sealed class SpaceFrameResult
{
    public List<SpaceNode> Nodes { get; } = new();
    public List<SpaceBar> Bars { get; } = new();
    public double LengthTolerance { get; set; } = U.Mm(1);

    public double TotalBarLength => Bars.Sum(b => b.Length);

    public static string RoleName(BarRole r) => r switch
    {
        BarRole.TopChord => Loc.T("Banzo superior", "Top chord"),
        BarRole.BottomChord => Loc.T("Banzo inferior", "Bottom chord"),
        BarRole.InnerChord => Loc.T("Banzo intermediário", "Inner chord"),
        BarRole.Vertical => Loc.T("Montante", "Vertical"),
        _ => Loc.T("Diagonal", "Diagonal"),
    };

    /// <summary>Barras agrupadas por função e comprimento (lista de corte).</summary>
    public TableData BarSchedule()
    {
        var t = new TableData(Loc.T("Barras do quadro espacial", "Space frame bars"));
        t.AddColumn(Loc.T("Função", "Role"))
         .AddColumn(Loc.T("Comprimento (mm)", "Length (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer)
         .AddColumn(Loc.T("Total (m)", "Total (m)"), ColumnKind.Number);
        var tol = Math.Max(1e-6, LengthTolerance);
        foreach (var g in Bars.GroupBy(b => (b.Role, Key: Math.Round(b.Length / tol)))
                              .OrderBy(g => g.Key.Role).ThenBy(g => g.Key.Key))
        {
            var len = g.Average(b => b.Length);
            t.AddRow(RoleName(g.Key.Role), U.ToMm(len), g.Count(), U.ToM(len * g.Count()));
        }
        t.AddTotalsRow(Loc.T("Total", "Total"), 2, 3);
        return t;
    }

    public TableData NodeSchedule()
    {
        var t = new TableData(Loc.T("Nós do quadro espacial", "Space frame nodes"));
        t.AddColumn(Loc.T("Camada", "Layer"), ColumnKind.Integer)
         .AddColumn(Loc.T("Barras por nó", "Bars per node"), ColumnKind.Integer)
         .AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer);
        foreach (var g in Nodes.GroupBy(n => (n.Layer, n.Valence)).OrderBy(g => g.Key.Layer).ThenBy(g => g.Key.Valence))
            t.AddRow(g.Key.Layer + 1, g.Key.Valence, g.Count());
        t.AddTotalsRow(Loc.T("Total", "Total"), 2);
        return t;
    }
}

public static class SpaceFrameGenerator
{
    /// <param name="footprint">Projeção em planta da estrutura.</param>
    /// <param name="surface">Opcional: superfície (ponto e normal) para estruturas curvas; sem ela, a camada 1 fica em Z = 0.</param>
    public static SpaceFrameResult Generate(SpaceFrameSettings s, Region2 footprint, Func<Vec2, (Vec3 Point, Vec3 Normal)>? surface = null, int hardLimit = 60000)
    {
        if (s.ModuleX <= 0 || s.ModuleY <= 0 || s.Depth <= 0)
            throw new ArgumentException(Loc.T("Módulos e altura devem ser maiores que zero.", "Modules and depth must be greater than zero."));
        var layers = Math.Clamp(s.Layers, 2, 6);
        var result = new SpaceFrameResult { LengthTolerance = s.LengthTolerance };
        var rot = MathUtil.ToRadians(s.RotationDeg);
        var origin = footprint.Bounds.Center + new Vec2(s.OffsetX, s.OffsetY);
        var tri = s.Type == SpaceFrameType.TriangularOffset;
        var mx = s.ModuleX;
        var my = tri ? s.ModuleX * Math.Sqrt(3) / 2 : s.ModuleY;

        var gb = Rect2.FromPoints(footprint.Outer.Select(p => (p - origin).Rotate(-rot)));
        int i0 = (int)Math.Floor(gb.MinX / mx) - 2, i1 = (int)Math.Ceiling(gb.MaxX / mx) + 2;
        int j0 = (int)Math.Floor(gb.MinY / my) - 2, j1 = (int)Math.Ceiling(gb.MaxY / my) + 2;
        if ((long)(i1 - i0 + 1) * (j1 - j0 + 1) * layers > hardLimit * 4L)
            throw new InvalidOperationException(Loc.T("A malha ficaria grande demais. Aumente o módulo.", "The grid would be too large. Increase the module size."));

        Vec3 Place(Vec2 plan, int layer)
        {
            if (surface == null) return new Vec3(plan.X, plan.Y, -layer * s.Depth);
            var (pt, nrm) = surface(plan);
            return pt - nrm.Normalized() * (layer * s.Depth);
        }

        bool IsOffsetLayer(int k) => s.Type != SpaceFrameType.SquareOnSquareAligned && (k % 2 == 1);

        // 1) Nós candidatos por camada.
        var layerNodes = new List<Dictionary<(int, int), SpaceNode>>();
        for (var k = 0; k < layers; k++)
        {
            var dict = new Dictionary<(int, int), SpaceNode>();
            var offset = IsOffsetLayer(k);
            for (var j = j0; j <= j1; j++)
            {
                for (var i = i0; i <= i1; i++)
                {
                    Vec2 q;
                    if (tri)
                    {
                        var rowShift = (j & 1) != 0 ? mx / 2 : 0;
                        q = new Vec2(i * mx + rowShift, j * my);
                        if (offset) q += new Vec2(mx / 2, my / 3);
                    }
                    else
                    {
                        q = new Vec2(i * mx, j * my);
                        if (offset) q += new Vec2(mx / 2, my / 2);
                    }
                    var plan = q.Rotate(rot) + origin;
                    if (!footprint.Contains(plan)) continue;
                    if (offset && s.Diagonals == DiagonalPattern.Alternate && ((i + j) & 1) != 0) continue;
                    dict[(i, j)] = new SpaceNode { Layer = k, I = i, J = j, Plan = plan };
                }
            }
            layerNodes.Add(dict);
        }

        // 2) Nós de camadas inferiores precisam estar apoiados em nós da camada de cima.
        var diagPlan = s.Type switch
        {
            SpaceFrameType.TriangularOffset => mx / Math.Sqrt(3),
            SpaceFrameType.SquareOnSquareAligned => 0.0,
            _ => Math.Sqrt(mx * mx / 4 + my * my / 4),
        };
        var tolPlan = Math.Min(mx, my) * 0.02 + 1e-6;
        for (var k = 1; k < layers; k++)
        {
            var upper = layerNodes[k - 1].Values.Select(n => n.Plan).ToList();
            var need = s.Type switch
            {
                SpaceFrameType.SquareOnSquareAligned => 1,
                SpaceFrameType.TriangularOffset => 3,
                _ => 4,
            };
            var hash = new SpatialHash(upper, Math.Max(diagPlan, tolPlan) + tolPlan);
            foreach (var key in layerNodes[k].Keys.ToList())
            {
                var p = layerNodes[k][key].Plan;
                var count = hash.Query(p, diagPlan + tolPlan).Count(idx => Math.Abs(upper[idx].DistanceTo(p) - diagPlan) <= tolPlan);
                if (count < need) layerNodes[k].Remove(key);
            }
        }

        foreach (var dict in layerNodes)
        {
            foreach (var n in dict.Values)
            {
                n.Id = result.Nodes.Count;
                n.Position = Place(n.Plan, n.Layer);
                result.Nodes.Add(n);
            }
        }
        if (result.Nodes.Count > hardLimit)
            throw new InvalidOperationException(Loc.T("Quantidade de nós excede o limite. Aumente o módulo.", "Node count exceeds the limit. Increase the module size."));

        void AddBar(SpaceNode a, SpaceNode b, BarRole role)
        {
            result.Bars.Add(new SpaceBar { Id = result.Bars.Count, A = a.Id, B = b.Id, Role = role, Start = a.Position, End = b.Position });
            a.Valence++;
            b.Valence++;
        }

        // 3) Banzos: vizinhos na mesma camada à distância do módulo.
        var chordTargets = tri ? new[] { mx } : new[] { mx, my };
        for (var k = 0; k < layers; k++)
        {
            var role = k == 0 ? BarRole.TopChord : k == layers - 1 ? BarRole.BottomChord : BarRole.InnerChord;
            var nodes = layerNodes[k].Values.ToList();
            ConnectByPlanDistance(nodes, nodes, chordTargets, tolPlan, sameSet: true, (a, b) => AddBar(a, b, role));
        }

        // 4) Diagonais e montantes entre camadas.
        for (var k = 0; k < layers - 1; k++)
        {
            var up = layerNodes[k];
            var down = layerNodes[k + 1];
            if (s.Type == SpaceFrameType.SquareOnSquareAligned)
            {
                foreach (var (key, d) in down)
                    if (up.TryGetValue(key, out var u))
                        AddBar(u, d, BarRole.Vertical);

                // Diagonais nos planos verticais entre nós vizinhos.
                foreach (var (key, u) in up)
                {
                    foreach (var nb in new[] { (key.Item1 + 1, key.Item2), (key.Item1, key.Item2 + 1) })
                    {
                        if (!up.TryGetValue(nb, out var u2)) continue;
                        down.TryGetValue(key, out var d1);
                        down.TryGetValue(nb, out var d2);
                        var even = ((key.Item1 + key.Item2 + k) & 1) == 0;
                        if (s.Diagonals == DiagonalPattern.All)
                        {
                            if (d2 != null) AddBar(u, d2, BarRole.Diagonal);
                            if (d1 != null) AddBar(u2, d1, BarRole.Diagonal);
                        }
                        else if (even && d2 != null) AddBar(u, d2, BarRole.Diagonal);
                        else if (!even && d1 != null) AddBar(u2, d1, BarRole.Diagonal);
                    }
                }
            }
            else
            {
                ConnectByPlanDistance(down.Values.ToList(), up.Values.ToList(), new[] { diagPlan }, tolPlan, sameSet: false, (a, b) => AddBar(b, a, BarRole.Diagonal));
            }
        }
        return result;
    }

    private static void ConnectByPlanDistance(List<SpaceNode> a, List<SpaceNode> b, double[] targets, double tol, bool sameSet, Action<SpaceNode, SpaceNode> add)
    {
        var maxD = targets.Max() + tol;
        var hash = new SpatialHash(b.Select(n => n.Plan).ToList(), maxD);
        foreach (var na in a)
        {
            foreach (var idx in hash.Query(na.Plan, maxD))
            {
                var nb = b[idx];
                if (sameSet && nb.Id <= na.Id) continue;
                var d = na.Plan.DistanceTo(nb.Plan);
                if (targets.Any(tg => Math.Abs(d - tg) <= tol))
                    add(na, nb);
            }
        }
    }
}

/// <summary>Índice espacial simples (grade) para buscas de vizinhança.</summary>
public sealed class SpatialHash
{
    private readonly List<Vec2> _pts;
    private readonly double _cell;
    private readonly Dictionary<(int, int), List<int>> _map = new();

    public SpatialHash(List<Vec2> points, double cellSize)
    {
        _pts = points;
        _cell = Math.Max(cellSize, 1e-9);
        for (var i = 0; i < points.Count; i++)
        {
            var k = Key(points[i]);
            if (!_map.TryGetValue(k, out var l)) _map[k] = l = new List<int>();
            l.Add(i);
        }
    }

    private (int, int) Key(Vec2 p) => ((int)Math.Floor(p.X / _cell), (int)Math.Floor(p.Y / _cell));

    public IEnumerable<int> Query(Vec2 p, double radius)
    {
        var r = (int)Math.Ceiling(radius / _cell);
        var (cx, cy) = Key(p);
        for (var y = cy - r; y <= cy + r; y++)
            for (var x = cx - r; x <= cx + r; x++)
                if (_map.TryGetValue((x, y), out var l))
                    foreach (var i in l)
                        if (_pts[i].DistanceTo(p) <= radius) yield return i;
    }

    public int CountWithin(Vec2 p, double radius) => Query(p, radius).Count();
}
