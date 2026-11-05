using System.Globalization;
using TeraTools.Core.Export;
using TeraTools.Core.Facade;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Seção da lâmina (vista de cima).</summary>
public enum FinProfile
{
    Plate,
    Round,
    Rhombus,
    Ellipse,
    /// <summary>Gota / aerofólio.</summary>
    Airfoil,
}

/// <summary>Como a base ou o topo das lâminas acompanham a fachada.</summary>
public enum FinTrim
{
    Flat,
    Slope,
    Wave,
    /// <summary>Arco (na base: abertura de passagem; no topo: coroamento em arco).</summary>
    Arch,
    /// <summary>Linha desenhada (a altura vem do ponto mais próximo em planta).</summary>
    Drawn,
}

/// <summary>Parâmetros da fachada de lâminas (pés e graus; cotas a partir da base).</summary>
public sealed class FinFacadeSettings
{
    // distribuição
    public double Spacing { get; set; } = U.Mm(400);
    /// <summary>Quantidade fixa (0 = pelo espaçamento).</summary>
    public int Count { get; set; }
    public double StartMargin { get; set; } = U.Mm(200);
    public double EndMargin { get; set; } = U.Mm(200);
    public double BottomZ { get; set; }
    public double TopZ { get; set; } = U.M(6);
    public FinSide Side { get; set; } = FinSide.Left;
    /// <summary>Distância da linha da fachada até a borda interna da lâmina (sem rotação).</summary>
    public double Offset { get; set; } = U.Mm(150);

    // seção
    public FinProfile Profile { get; set; } = FinProfile.Plate;
    public double Depth { get; set; } = U.Mm(350);
    public double Thickness { get; set; } = U.Mm(30);
    /// <summary>Profundidade no topo em relação à base (1 = igual).</summary>
    public double TopDepthFactor { get; set; } = 1;
    public FinPivot Pivot { get; set; } = FinPivot.Center;

    // rotação
    public double BaseAngleDeg { get; set; }
    public VariationMode Variation { get; set; } = VariationMode.Sine;
    public double AmplitudeDeg { get; set; } = 45;
    public double Periods { get; set; } = 1.5;
    public double PhaseDeg { get; set; }
    public double AttractorPosition { get; set; } = 0.5;
    public double AttractorRadius { get; set; } = 0.3;
    public int Seed { get; set; } = 7;
    /// <summary>Fórmula em graus. Variáveis: u (0–1 ao longo), h (0–1 na altura), i, n, x e z (m).</summary>
    public string Formula { get; set; } = "45*sin(360*u + 90*h)";
    /// <summary>Torção extra da base ao topo de cada lâmina.</summary>
    public double TwistDeg { get; set; }
    /// <summary>A torção acompanha a variação (lâminas do meio torcem mais, por exemplo).</summary>
    public bool TwistFollowsVariation { get; set; }
    /// <summary>Estado cinético: 0 = fechado (ângulo base), 1 = aberto (variação completa).</summary>
    public double Opening { get; set; } = 1;
    /// <summary>Quanto a profundidade acompanha a variação (0 = constante; 0,5 = ±50%).</summary>
    public double DepthVariation { get; set; }

    // recortes
    public FinTrim BottomTrim { get; set; } = FinTrim.Flat;
    public FinTrim TopTrim { get; set; } = FinTrim.Flat;
    public double BottomAmount { get; set; } = U.M(1);
    public double TopAmount { get; set; } = U.M(1);
    public double TrimWaves { get; set; } = 2;
    public double ArchCenter { get; set; } = 0.5;
    public double ArchWidth { get; set; } = U.M(4);
    public double ArchSpring { get; set; } = U.M(2.2);
    public double ArchCrown { get; set; } = U.M(3.6);
    public List<Vec3> BottomCurve { get; set; } = new();
    public List<Vec3> TopCurve { get; set; } = new();

    // faixas horizontais
    public int BandCount { get; set; }
    /// <summary>Alturas das faixas em mm, separadas por ";" (tem prioridade sobre a quantidade).</summary>
    public string BandHeights { get; set; } = "";
    public double BandDepth { get; set; } = U.Mm(500);
    public double BandThickness { get; set; } = U.Mm(150);
    /// <summary>Distância da fachada até a borda interna da faixa.</summary>
    public double BandOffset { get; set; }

    /// <summary>Sequência de materiais por lâmina, ex.: "1,1,2".</summary>
    public string MaterialSequence { get; set; } = "1";
    /// <summary>Seções ao longo da altura (0 = automático).</summary>
    public int Levels { get; set; }
}

public sealed class FinInfo
{
    public int Index { get; set; }
    public double Position { get; set; }
    public double Bottom { get; set; }
    public double Top { get; set; }
    public double Length => Top - Bottom;
    public double AngleBottom { get; set; }
    public double AngleTop { get; set; }
    public double Depth { get; set; }
    public int Material { get; set; }
    public OrganicMesh Mesh { get; set; } = new();
    /// <summary>Seção na base (para a planta da pré-visualização).</summary>
    public List<Vec2> Footprint { get; set; } = new();
}

public sealed class FinFacadeResult
{
    public List<FinInfo> Fins { get; } = new();
    public List<OrganicMesh> Bands { get; } = new();
    public List<string> Warnings { get; } = new();
    public double BandLength { get; set; }
    public int TriangleCount => Fins.Sum(f => f.Mesh.Triangles.Count) + Bands.Sum(b => b.Triangles.Count);

    /// <summary>Lista de corte: lâminas agrupadas por comprimento (arredondado ao mm) e conjunto de material.</summary>
    public TableData CutList()
    {
        var t = new TableData(Loc.T("Lista de corte das lâminas", "Fin cut list"));
        t.AddColumn(Loc.T("Conjunto", "Set"), ColumnKind.Integer)
         .AddColumn(Loc.T("Comprimento (mm)", "Length (mm)"), ColumnKind.Number, 0)
         .AddColumn(Loc.T("Profundidade (mm)", "Depth (mm)"), ColumnKind.Number, 0)
         .AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer)
         .AddColumn(Loc.T("Total (m)", "Total (m)"), ColumnKind.Number, 2);
        foreach (var g in Fins.GroupBy(f => (f.Material, L: Math.Round(U.ToMm(f.Length)), D: Math.Round(U.ToMm(f.Depth))))
                     .OrderBy(g => g.Key.Material).ThenByDescending(g => g.Key.L))
            t.AddRow(g.Key.Material, g.Key.L, g.Key.D, g.Count(), U.ToM(g.Sum(f => f.Length)));
        t.AddTotalsRow(Loc.T("Total", "Total"), 3, 4);
        return t;
    }

    public TableData Schedule()
    {
        var t = new TableData(Loc.T("Lâminas (posição, altura e rotação)", "Fins (position, height and rotation)"));
        t.AddColumn(Loc.T("Nº", "No."), ColumnKind.Integer)
         .AddColumn(Loc.T("Posição (m)", "Position (m)"), ColumnKind.Number, 3)
         .AddColumn(Loc.T("Base (m)", "Bottom (m)"), ColumnKind.Number, 3)
         .AddColumn(Loc.T("Topo (m)", "Top (m)"), ColumnKind.Number, 3)
         .AddColumn(Loc.T("Rotação na base (°)", "Bottom rotation (°)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Rotação no topo (°)", "Top rotation (°)"), ColumnKind.Number, 1);
        foreach (var f in Fins)
            t.AddRow(f.Index + 1, U.ToM(f.Position), U.ToM(f.Bottom), U.ToM(f.Top), f.AngleBottom, f.AngleTop);
        return t;
    }
}

/// <summary>
/// Fachada de lâminas personalizadas: cada lâmina gira conforme a posição na fachada (onda, atrator,
/// aleatório, fórmula) e pode torcer ao longo da altura; base e topo seguem recortes (inclinado, onda,
/// arco de passagem, linha desenhada); faixas horizontais opcionais. Estado cinético de 0 a 1.
/// </summary>
public static class FinFacadeGenerator
{
    public static FinFacadeResult Generate(FinFacadeSettings s, PolylinePath path, int hardLimit = 5000)
    {
        var r = new FinFacadeResult();
        if (path.Length < U.Mm(100))
        {
            r.Warnings.Add(Loc.T("Caminho da fachada curto demais.", "Facade path too short."));
            return r;
        }
        var depth0 = Math.Max(U.Mm(5), s.Depth);
        var thick = Math.Max(U.Mm(2), s.Thickness);
        Expression? formula = null;
        if (s.Variation == VariationMode.Formula && !Expression.TryParse(s.Formula, out formula, out var err))
            r.Warnings.Add(Loc.F("Fórmula inválida ({0}): sem variação.", "Invalid formula ({0}): no variation.", err ?? ""));
        var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var materials = ParseSequence(s.MaterialSequence);
        var positions = Positions(s, path.Length);
        if (positions.Count > hardLimit)
        {
            r.Warnings.Add(Loc.F("Lâminas demais ({0}): limitei a {1}. Aumente o espaçamento.", "Too many fins ({0}): limited to {1}. Increase the spacing.", positions.Count, hardLimit));
            positions = positions.Take(hardLimit).ToList();
        }
        var n = positions.Count;
        var rnd = new Random(s.Seed);
        var randoms = Enumerable.Range(0, n).Select(_ => rnd.NextDouble() * 2 - 1).ToList();
        var opening = Math.Clamp(s.Opening, 0, 1);
        var profile = Profile(s.Profile, depth0, thick);

        for (var k = 0; k < n; k++)
        {
            var pos = positions[k];
            var u = n <= 1 ? 0.5 : (double)k / (n - 1);
            var p = path.PointAt(pos);
            var bottom = Trim(s, s.BottomTrim, true, pos, path.Length, p);
            var top = Trim(s, s.TopTrim, false, pos, path.Length, p);
            if (top - bottom < U.Mm(50)) continue;

            // valor da variação (−1 a 1, ou 0 a 1 no atrator) e ângulo em cada altura
            var m = s.Variation switch
            {
                VariationMode.Linear => 2 * u - 1,
                VariationMode.Sine => Math.Sin(2 * Math.PI * s.Periods * u + s.PhaseDeg * Math.PI / 180),
                VariationMode.Random => randoms[k],
                VariationMode.Attractor => 1 - Math.Min(1, Math.Abs(u - s.AttractorPosition) / Math.Max(1e-6, s.AttractorRadius)),
                _ => 0,
            };
            double Angle(double h)
            {
                if (s.Variation == VariationMode.Formula && formula != null)
                {
                    vars["u"] = u;
                    vars["h"] = h;
                    vars["i"] = k;
                    vars["n"] = n;
                    vars["x"] = U.ToM(pos);
                    vars["z"] = U.ToM(bottom + (top - bottom) * h);
                    double f;
                    try
                    {
                        f = formula.Evaluate(vars);
                    }
                    catch (ExpressionException)
                    {
                        f = 0;
                    }
                    return s.BaseAngleDeg + opening * (double.IsFinite(f) ? f : 0);
                }
                var twist = s.TwistDeg * (s.TwistFollowsVariation ? m : 1);
                return s.BaseAngleDeg + opening * (s.AmplitudeDeg * m + twist * h);
            }
            var depthK = depth0 * Math.Max(0.1, 1 + s.DepthVariation * m);

            var t2 = path.TangentAt(pos);
            var nrm = s.Side == FinSide.Left ? t2.Perp : -t2.Perp;
            var twisting = Math.Abs(Angle(1) - Angle(0)) > 0.01 || Math.Abs(s.TopDepthFactor - 1) > 1e-6 || s.Variation == VariationMode.Formula;
            var levels = s.Levels > 0 ? Math.Clamp(s.Levels, 2, 200) : twisting ? Math.Clamp((int)Math.Ceiling((top - bottom) / U.Mm(150)), 8, 80) : 2;
            var rings = new List<IReadOnlyList<Vec3>>(levels);
            for (var j = 0; j < levels; j++)
            {
                var h = (double)j / (levels - 1);
                var z = bottom + (top - bottom) * h;
                var d = depthK * (1 + (s.TopDepthFactor - 1) * h);
                var a = Angle(h) * Math.PI / 180;
                rings.Add(Section(profile, d / depth0, s, d, a, p, nrm, t2, z));
            }
            var mesh = MeshKit.Loft(rings, "fin");
            if (mesh.Triangles.Count == 0) continue;
            r.Fins.Add(new FinInfo
            {
                Index = r.Fins.Count,
                Position = pos,
                Bottom = bottom,
                Top = top,
                AngleBottom = Math.Round(Angle(0), 2),
                AngleTop = Math.Round(Angle(1), 2),
                Depth = depthK,
                Material = materials[k % materials.Count],
                Mesh = mesh,
                Footprint = rings[0].Select(v => v.XY).ToList(),
            });
        }
        if (r.Fins.Count == 0)
            r.Warnings.Add(Loc.T("Nenhuma lâmina cabe com essas opções (confira altura, recortes e margens).", "No fin fits with these options (check height, trims and margins)."));
        Bands(s, path, r);
        Check(s, r);
        return r;
    }

    /// <summary>Posições ao longo do caminho (centralizadas).</summary>
    public static List<double> Positions(FinFacadeSettings s, double length)
    {
        var a = Math.Max(0, s.StartMargin);
        var b = length - Math.Max(0, s.EndMargin);
        var list = new List<double>();
        if (b <= a) return list;
        if (s.Count > 0)
        {
            if (s.Count == 1) return new List<double> { (a + b) / 2 };
            for (var i = 0; i < s.Count; i++) list.Add(a + (b - a) * i / (s.Count - 1));
            return list;
        }
        var sp = Math.Max(U.Mm(20), s.Spacing);
        var count = (int)Math.Floor((b - a) / sp + 1e-9) + 1;
        var start = (a + b) / 2 - (count - 1) * sp / 2;
        for (var i = 0; i < count; i++) list.Add(start + i * sp);
        return list;
    }

    /// <summary>Perfil unitário: X de 0 a 1 (profundidade, da fachada para fora), Y = espessura real (pés).</summary>
    private static List<Vec2> Profile(FinProfile kind, double depth, double thick)
    {
        var t = thick / 2;
        switch (kind)
        {
            case FinProfile.Round:
                return Enumerable.Range(0, 20).Select(i =>
                {
                    var a = 2 * Math.PI * i / 20;
                    return new Vec2(0.5 + 0.5 * Math.Cos(a), depth / 2 * Math.Sin(a));
                }).ToList();
            case FinProfile.Rhombus:
                return new List<Vec2> { new(0, 0), new(0.5, -t), new(1, 0), new(0.5, t) };
            case FinProfile.Ellipse:
                return Enumerable.Range(0, 20).Select(i =>
                {
                    var a = 2 * Math.PI * i / 20;
                    return new Vec2(0.5 + 0.5 * Math.Cos(a), t * Math.Sin(a));
                }).ToList();
            case FinProfile.Airfoil:
            {
                // NACA 00xx (bordo de fuga fechado): bordo de ataque junto à fachada
                double Y(double x) => 5 * (2 * t / depth) * (0.2969 * Math.Sqrt(x) - 0.1260 * x - 0.3516 * x * x + 0.2843 * x * x * x - 0.1036 * x * x * x * x) * depth;
                var xs = Enumerable.Range(0, 13).Select(i => Math.Pow((double)i / 12, 1.6)).ToList();
                var upper = xs.Select(x => new Vec2(x, Y(x)));
                var lower = xs.AsEnumerable().Reverse().Skip(1).Take(xs.Count - 2).Select(x => new Vec2(x, -Y(x)));
                return lower.Concat(upper.Skip(0)).Distinct().ToList();
            }
            default:
                return new List<Vec2> { new(0, -t), new(1, -t), new(1, t), new(0, t) };
        }
    }

    /// <summary>Seção da lâmina na altura z: perfil escalado, girado em torno do pivô e levado ao caminho.</summary>
    private static List<Vec3> Section(List<Vec2> profile, double depthScale, FinFacadeSettings s, double depth, double angle,
        Vec2 at, Vec2 nrm, Vec2 tan, double z)
    {
        var pivotX = s.Pivot switch
        {
            FinPivot.FacadeEdge => 0,
            FinPivot.OuterEdge => depth,
            _ => depth / 2,
        };
        var c = Math.Cos(angle);
        var sn = Math.Sin(angle);
        var round = s.Profile == FinProfile.Round;
        return profile.Select(q =>
        {
            // X do perfil é fração da profundidade; no tubo redondo a "espessura" também acompanha
            var x = q.X * depth - pivotX;
            var y = round ? q.Y * depthScale : q.Y;
            var rx = x * c - y * sn + pivotX + s.Offset;
            var ry = x * sn + y * c;
            var w = at + nrm * rx + tan * ry;
            return new Vec3(w.X, w.Y, z);
        }).ToList();
    }

    private static double Trim(FinFacadeSettings s, FinTrim trim, bool bottom, double pos, double length, Vec2 at)
    {
        var baseZ = bottom ? s.BottomZ : s.TopZ;
        var amount = bottom ? s.BottomAmount : s.TopAmount;
        var u = length <= 0 ? 0 : pos / length;
        switch (trim)
        {
            case FinTrim.Slope:
                return baseZ + amount * u;
            case FinTrim.Wave:
                return baseZ + amount * 0.5 * (1 - Math.Cos(2 * Math.PI * s.TrimWaves * u));
            case FinTrim.Arch:
            {
                var xc = Math.Clamp(s.ArchCenter, 0, 1) * length;
                var half = Math.Max(U.Mm(100), s.ArchWidth / 2);
                var d = Math.Abs(pos - xc) / half;
                if (d >= 1) return baseZ;
                var k = Math.Sqrt(1 - d * d);
                if (bottom)
                {
                    var arch = s.ArchSpring + (s.ArchCrown - s.ArchSpring) * k;
                    // na passagem a lâmina começa no arco; nos pés do arco, no mínimo na altura da base
                    return Math.Max(baseZ, d < 1 ? Math.Max(arch, s.ArchSpring) : baseZ);
                }
                return baseZ + amount * k;
            }
            case FinTrim.Drawn:
            {
                var curve = bottom ? s.BottomCurve : s.TopCurve;
                return curve.Count >= 2 ? HeightFromCurve(curve, at) : baseZ;
            }
            default:
                return baseZ;
        }
    }

    /// <summary>Altura (Z) do ponto da curva desenhada mais próximo em planta.</summary>
    public static double HeightFromCurve(IReadOnlyList<Vec3> curve, Vec2 at)
    {
        var best = double.MaxValue;
        var z = curve[0].Z;
        for (var i = 1; i < curve.Count; i++)
        {
            var a = curve[i - 1];
            var b = curve[i];
            var ab = b.XY - a.XY;
            var len2 = ab.LengthSquared;
            var t = len2 < 1e-12 ? 0 : Math.Clamp((at - a.XY).Dot(ab) / len2, 0, 1);
            var q = a.XY + ab * t;
            var d = q.DistanceTo(at);
            if (d < best)
            {
                best = d;
                z = a.Z + (b.Z - a.Z) * t;
            }
        }
        return z;
    }

    private static void Bands(FinFacadeSettings s, PolylinePath path, FinFacadeResult r)
    {
        var heights = new List<double>();
        if (!string.IsNullOrWhiteSpace(s.BandHeights))
        {
            foreach (var part in s.BandHeights.Split(new[] { ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(part.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var mm)) heights.Add(U.Mm(mm));
        }
        else if (s.BandCount > 0)
        {
            for (var i = 1; i <= s.BandCount; i++) heights.Add(s.BottomZ + (s.TopZ - s.BottomZ) * i / (s.BandCount + 1));
        }
        if (heights.Count == 0) return;
        var bd = Math.Max(U.Mm(10), s.BandDepth);
        var bt = Math.Max(U.Mm(10), s.BandThickness);
        var profile = new List<Vec2> { new(s.BandOffset, -bt / 2), new(s.BandOffset + bd, -bt / 2), new(s.BandOffset + bd, bt / 2), new(s.BandOffset, bt / 2) };
        // amostras ao longo do caminho: vértices + a cada 200 mm
        var stations = new SortedSet<double> { 0, path.Length };
        for (var x = 0.0; x < path.Length; x += U.Mm(200)) stations.Add(x);
        var cum = 0.0;
        for (var i = 1; i < path.Points.Count; i++)
        {
            cum += path.Points[i].DistanceTo(path.Points[i - 1]);
            stations.Add(Math.Min(cum, path.Length));
        }
        foreach (var z in heights)
        {
            // a faixa é interrompida onde o recorte de base passa acima dela (abertura em arco, por exemplo)
            var run = new List<Vec3>();
            void Flush()
            {
                if (run.Count >= 2)
                {
                    var m = Band(run, profile, path, s);
                    if (m.Triangles.Count > 0)
                    {
                        r.Bands.Add(m);
                        r.BandLength += MeshKit.PathLength(run);
                    }
                }
                run = new List<Vec3>();
            }
            foreach (var x in stations)
            {
                var p = path.PointAt(x);
                var open = Trim(s, s.BottomTrim, true, x, path.Length, p) > z - bt / 2 || Trim(s, s.TopTrim, false, x, path.Length, p) < z + bt / 2;
                if (open) Flush();
                else run.Add(new Vec3(x, 0, z));
            }
            Flush();
        }
    }

    /// <summary>Faixa: perfil no plano (normal, vertical) varrido pelas estações do caminho.</summary>
    private static OrganicMesh Band(List<Vec3> stations, List<Vec2> profile, PolylinePath path, FinFacadeSettings s)
    {
        var rings = new List<IReadOnlyList<Vec3>>();
        foreach (var st in stations)
        {
            var p = path.PointAt(st.X);
            var nrm = MiterNormal(path, st.X, s.Side);
            rings.Add(profile.Select(q =>
            {
                var w = p + nrm * q.X;
                return new Vec3(w.X, w.Y, st.Z + q.Y);
            }).ToList());
        }
        return MeshKit.Loft(rings, "band");
    }

    /// <summary>Normal da fachada; nos cantos do caminho, a normal de meia-esquadria (faixa sem estrangular).</summary>
    private static Vec2 MiterNormal(PolylinePath path, double x, FinSide side)
    {
        Vec2 N(Vec2 t) => side == FinSide.Left ? t.Perp : -t.Perp;
        var pts = path.Points;
        var cum = 0.0;
        for (var i = 1; i < pts.Count - 1; i++)
        {
            cum += pts[i].DistanceTo(pts[i - 1]);
            if (Math.Abs(cum - x) > 1e-6) continue;
            var n1 = N((pts[i] - pts[i - 1]).Normalized());
            var n2 = N((pts[i + 1] - pts[i]).Normalized());
            var bis = (n1 + n2).Normalized();
            var cos = bis.Dot(n1);
            return cos < 0.2 ? n1 : bis / cos;
        }
        return N(path.TangentAt(Math.Clamp(x, 1e-6, path.Length - 1e-6)));
    }

    private static void Check(FinFacadeSettings s, FinFacadeResult r)
    {
        // lâminas vizinhas que se tocam (na base e no topo): compara as seções em planta
        var hits = 0;
        for (var i = 1; i < r.Fins.Count; i++)
        {
            var a = new Polygon2(r.Fins[i - 1].Footprint);
            var b = new Polygon2(r.Fins[i].Footprint);
            if (a.IsEmpty || b.IsEmpty) continue;
            if (Math.Abs(r.Fins[i - 1].Bottom - r.Fins[i].Bottom) > U.M(2)) continue;
            if (PolygonOps.Intersects(a, b, U.Mm(1) * U.Mm(1))) hits++;
        }
        if (hits > 0)
            r.Warnings.Add(Loc.F("{0} par(es) de lâminas vizinhas se tocam na base: aumente o espaçamento ou reduza a rotação/profundidade.",
                "{0} pair(s) of neighboring fins touch at the bottom: increase the spacing or reduce rotation/depth.", hits));
        if (r.TriangleCount > 600_000)
            r.Warnings.Add(Loc.F("Malha pesada ({0} triângulos): reduza a quantidade de lâminas ou de seções.", "Heavy mesh ({0} triangles).", r.TriangleCount));
    }

    private static List<int> ParseSequence(string text)
    {
        var list = new List<int>();
        foreach (var part in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out var v) && v >= 1 && v <= 9) list.Add(v);
        return list.Count == 0 ? new List<int> { 1 } : list;
    }
}
