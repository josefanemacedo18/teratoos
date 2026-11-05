using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Eixo da coluna (o caminho que o centro das seções percorre).</summary>
public enum ColumnSpine
{
    Straight,
    /// <summary>Reta inclinada (ângulo e direção).</summary>
    Inclined,
    /// <summary>Arco: sai e volta ao prumo (barriga lateral).</summary>
    Bow,
    /// <summary>Em S: o topo desloca para o lado, com as pontas no prumo.</summary>
    SCurve,
    /// <summary>Linha desenhada pelo usuário.</summary>
    Drawn,
}

/// <summary>Silhueta: como o raio varia ao longo da altura.</summary>
public enum ColumnSilhouette
{
    Cylinder,
    /// <summary>Variação linear do raio da base ao do topo (cone ou cone invertido).</summary>
    Cone,
    /// <summary>Abre no alto, como um trompete ou leque (expoente controla a curvatura).</summary>
    Trumpet,
    /// <summary>Mais larga no meio.</summary>
    Belly,
    /// <summary>Mais estreita no meio.</summary>
    Hourglass,
    /// <summary>Fuste reto que abre bruscamente no alto, como um cogumelo.</summary>
    Mushroom,
    /// <summary>Raio por fórmula (mm), com t de 0 a 1, z e h em mm.</summary>
    Formula,
    /// <summary>Perfil desenhado (raio × altura).</summary>
    Drawn,
}

/// <summary>Como a coluna é construída: maciça, oca ou vazada por barras, lâminas ou pétalas.</summary>
public enum ColumnBuild
{
    Solid,
    /// <summary>Casca oca com espessura.</summary>
    Shell,
    /// <summary>Nervuras verticais (seguem a torção) com anéis nas pontas.</summary>
    Ribs,
    /// <summary>Treliça em hélices cruzadas (diagrid).</summary>
    Lattice,
    /// <summary>Grelha: nervuras verticais e anéis horizontais.</summary>
    Grid,
    /// <summary>Lâminas radiais (aletas) em volta de um núcleo.</summary>
    Lamellas,
    /// <summary>Tronco maciço que se abre em pétalas (tulipa).</summary>
    Petals,
}

public enum ColumnEnd
{
    None,
    Disc,
    Square,
    /// <summary>Prato: abre a partir do fuste até o diâmetro indicado.</summary>
    Plate,
}

/// <summary>Funções das peças (cada uma pode ter um material).</summary>
public static class ColumnRole
{
    public const string Shaft = "shaft";
    public const string Member = "member";
    public const string Ring = "ring";
    public const string Core = "core";
    public const string Base = "base";
    public const string Capital = "capital";

    public static string Label(string role) => role switch
    {
        Shaft => Loc.T("Fuste", "Shaft"),
        Member => Loc.T("Nervuras, lâminas e pétalas", "Ribs, fins and petals"),
        Ring => Loc.T("Anéis", "Rings"),
        Core => Loc.T("Núcleo", "Core"),
        Base => Loc.T("Base", "Base"),
        Capital => Loc.T("Capitel", "Capital"),
        _ => role,
    };
}

/// <summary>Parâmetros de uma coluna orgânica (comprimentos em pés, unidade interna do Revit).</summary>
public sealed class ColumnSettings
{
    public double Height { get; set; } = U.M(3);

    // silhueta
    public ColumnSilhouette Silhouette { get; set; } = ColumnSilhouette.Cylinder;
    public double BaseRadius { get; set; } = U.Mm(200);
    public double TopRadius { get; set; } = U.Mm(200);
    public double MidRadius { get; set; } = U.Mm(260);
    public double FlarePower { get; set; } = 3;
    public double MushroomStart { get; set; } = 0.7;
    public string RadiusFormula { get; set; } = "200 + 80*sin(180*t)";
    /// <summary>Perfil desenhado: X = raio, Y = altura (a partir da base). Esticado para a altura da coluna.</summary>
    public List<Vec2> DrawnSilhouette { get; set; } = new();

    // seção
    public ColumnSection Section { get; set; } = ColumnSection.Circle;
    public int Sides { get; set; } = 6;
    public int Lobes { get; set; } = 5;
    public double LobeDepth { get; set; } = 0.25;
    public double StarInner { get; set; } = 0.55;
    public double EllipseRatio { get; set; } = 0.6;
    public double SectionRotationDeg { get; set; }
    /// <summary>Contorno desenhado (qualquer tamanho; é centrado e normalizado).</summary>
    public List<Vec2> DrawnSection { get; set; } = new();

    // eixo
    public ColumnSpine Spine { get; set; } = ColumnSpine.Straight;
    public double LeanDeg { get; set; } = 10;
    public double LeanDirectionDeg { get; set; }
    public double BendOffset { get; set; } = U.Mm(300);
    /// <summary>Eixo desenhado (pontos a partir da base). O comprimento do eixo define a altura.</summary>
    public List<Vec3> DrawnSpine { get; set; } = new();

    // modificadores
    public double TwistDeg { get; set; }
    public int Flutes { get; set; }
    public double FluteDepth { get; set; } = 0.08;
    public double WaveAmplitude { get; set; }
    public double WaveCount { get; set; } = 3;
    public int WaveLobes { get; set; }

    // construção
    public ColumnBuild Build { get; set; } = ColumnBuild.Solid;
    public double ShellThickness { get; set; } = U.Mm(40);
    public int MemberCount { get; set; } = 12;
    public double MemberSize { get; set; } = U.Mm(60);
    public double MemberDepth { get; set; } = U.Mm(120);
    public int RingCount { get; set; } = 2;
    public double LatticeTurns { get; set; } = 0.5;
    public double CoreRadius { get; set; }
    public double PetalStart { get; set; } = 0.55;
    public double PetalGap { get; set; } = 0.12;
    public double PetalTaper { get; set; } = 0.5;

    // base e capitel
    public ColumnEnd Base { get; set; } = ColumnEnd.None;
    public double BaseSize { get; set; } = U.Mm(600);
    public double BaseThickness { get; set; } = U.Mm(50);
    public ColumnEnd Capital { get; set; } = ColumnEnd.None;
    public double CapitalSize { get; set; } = U.Mm(900);
    public double CapitalThickness { get; set; } = U.Mm(60);

    // resolução
    public int Resolution { get; set; } = 48;
    /// <summary>Seções ao longo da altura (0 = automático).</summary>
    public int Levels { get; set; }

    public ColumnSettings Clone()
    {
        var c = (ColumnSettings)MemberwiseClone();
        c.DrawnSilhouette = new List<Vec2>(DrawnSilhouette);
        c.DrawnSection = new List<Vec2>(DrawnSection);
        c.DrawnSpine = new List<Vec3>(DrawnSpine);
        return c;
    }
}

/// <summary>Quantitativos de uma coluna (unidades internas: pés, pés², pés³).</summary>
public sealed class ColumnQuantities
{
    public Dictionary<string, double> VolumeByRole { get; } = new();
    public double Volume => VolumeByRole.Values.Sum();
    public double SurfaceArea { get; set; }
    /// <summary>Comprimento total das barras (nervuras, hélices e anéis).</summary>
    public double MemberLength { get; set; }
    public int MemberCount { get; set; }
    public double MinRadius { get; set; }
    public double MaxRadius { get; set; }
    public double Height { get; set; }
    /// <summary>Índice de esbeltez indicativo (λ = L / i, apoio simples). 0 quando não se aplica.</summary>
    public double Slenderness { get; set; }
    /// <summary>Esbeltez da barra mais crítica (entre anéis/nós), para construções em barras.</summary>
    public double MemberSlenderness { get; set; }

    /// <summary>Peso (kg) com a densidade informada (kg/m³).</summary>
    public double Weight(double densityKgM3) => U.CuFtToM3(Volume) * densityKgM3;
}

public sealed class ColumnResult
{
    public List<OrganicMesh> Meshes { get; } = new();
    public List<string> Warnings { get; } = new();
    public ColumnQuantities Quantities { get; } = new();
    public int TriangleCount => Meshes.Sum(m => m.Triangles.Count);
}

/// <summary>
/// Gerador de colunas orgânicas: combina eixo, silhueta, seção, modificadores (torção, caneluras, ondas)
/// e construção (maciça, casca, nervuras, treliça, grelha, lâminas, pétalas). Cada peça sai como malha
/// fechada, pronta para virar forma livre no Revit.
/// </summary>
public static class ColumnGenerator
{
    private sealed class Ctx
    {
        public ColumnSettings S = null!;
        public double Length;          // comprimento do eixo (altura para eixos verticais)
        public List<Vec3> Path = new(); // eixo desenhado reamostrado
        public List<(Vec3 N, Vec3 B)> Frames = new();
        public bool Perpendicular;
        public Func<double, double> Radius = _ => 1;
        public List<Vec2> Unit = new();
    }

    public static ColumnResult Generate(ColumnSettings s)
    {
        var r = new ColumnResult();
        var c = Prepare(s, r.Warnings);
        var n = Math.Clamp(s.Resolution, 12, 128);
        c.Unit = OrganicSection.Unit(s.Section, n, s.Sides, s.Lobes, s.LobeDepth, s.StarInner, s.EllipseRatio, s.DrawnSection);
        if (s.Section == ColumnSection.Drawn && s.DrawnSection.Count < 3)
            r.Warnings.Add(Loc.T("Seção desenhada sem contorno: usei um círculo.", "Drawn section without outline: a circle was used."));

        var baseT = s.Base == ColumnEnd.None ? 0 : Math.Clamp(s.BaseThickness, U.Mm(5), c.Length * 0.3);
        var capT = s.Capital == ColumnEnd.None ? 0 : Math.Clamp(s.CapitalThickness, U.Mm(5), c.Length * 0.3);
        var s0 = baseT / c.Length;
        var s1 = 1 - capT / c.Length;
        var levels = s.Levels > 0 ? Math.Clamp(s.Levels, 4, 400) : AutoLevels(s, c.Length);
        var ss = Enumerable.Range(0, levels).Select(k => s0 + (s1 - s0) * k / (levels - 1)).ToList();

        // anéis do envelope em cada nível
        var envelope = ss.Select(t => Ring(c, t, 1)).ToList();
        var radii = ss.Select(c.Radius).ToList();
        r.Quantities.MinRadius = radii.Min();
        r.Quantities.MaxRadius = radii.Max();
        r.Quantities.Height = c.Length;

        switch (s.Build)
        {
            case ColumnBuild.Solid:
                r.Meshes.Add(MeshKit.Loft(envelope, ColumnRole.Shaft));
                break;
            case ColumnBuild.Shell:
                Shell(c, ss, envelope, r);
                break;
            case ColumnBuild.Ribs:
            case ColumnBuild.Grid:
            case ColumnBuild.Lattice:
                Bars(c, ss, r);
                break;
            case ColumnBuild.Lamellas:
                Lamellas(c, ss, r);
                break;
            case ColumnBuild.Petals:
                Petals(c, s0, s1, levels, r);
                break;
        }
        if ((s.Build is ColumnBuild.Ribs or ColumnBuild.Grid or ColumnBuild.Lattice or ColumnBuild.Lamellas) && s.CoreRadius > 1e-6)
        {
            var core = ss.Select(t => Circle(c, t, s.CoreRadius, 24)).ToList();
            r.Meshes.Add(MeshKit.Loft(core, ColumnRole.Core));
        }

        if (s.Base != ColumnEnd.None) r.Meshes.Add(End(c, s.Base, 0, s0, s.BaseSize, c.Radius(s0), ColumnRole.Base, top: false));
        if (s.Capital != ColumnEnd.None) r.Meshes.Add(End(c, s.Capital, s1, 1, s.CapitalSize, c.Radius(s1), ColumnRole.Capital, top: true));
        r.Meshes.RemoveAll(m => m.Triangles.Count == 0);

        Quantify(c, ss, r);
        Check(c, r);
        return r;
    }

    private static int AutoLevels(ColumnSettings s, double length)
    {
        var baseLevels = (int)Math.Ceiling(length / U.Mm(50));
        if (Math.Abs(s.TwistDeg) > 1 || s.WaveAmplitude > 0 || s.Build is ColumnBuild.Lattice) baseLevels = (int)(baseLevels * 1.5);
        return Math.Clamp(baseLevels, 24, 160);
    }

    #region Eixo, silhueta e seções

    private static Ctx Prepare(ColumnSettings s, List<string> warnings)
    {
        var c = new Ctx { S = s };
        if (s.Spine == ColumnSpine.Drawn && s.DrawnSpine.Count >= 2 && MeshKit.PathLength(s.DrawnSpine) > U.Mm(100))
        {
            c.Length = MeshKit.PathLength(s.DrawnSpine);
            c.Path = ResamplePath(s.DrawnSpine, 240);
            // seções horizontais enquanto o eixo sobe sempre e não deita demais; senão, perpendiculares
            var monotonic = true;
            for (var i = 1; i < c.Path.Count; i++)
            {
                var d = c.Path[i] - c.Path[i - 1];
                if (d.Z <= 1e-9 || d.Z < d.Length * Math.Sin(30 * Math.PI / 180)) { monotonic = false; break; }
            }
            c.Perpendicular = !monotonic;
            if (c.Perpendicular) c.Frames = MeshKit.Frames(c.Path);
        }
        else
        {
            if (s.Spine == ColumnSpine.Drawn)
                warnings.Add(Loc.T("Eixo desenhado sem linha válida: usei um eixo reto.", "Drawn spine without a valid line: a straight spine was used."));
            c.Length = Math.Max(U.Mm(200), s.Height);
        }
        c.Radius = RadiusFunction(s, c.Length, warnings);
        return c;
    }

    /// <summary>Raio do envelope no parâmetro t (0 = base, 1 = topo), em pés.</summary>
    private static Func<double, double> RadiusFunction(ColumnSettings s, double length, List<string> warnings)
    {
        var min = U.Mm(5);
        double rb = Math.Max(min, s.BaseRadius), rt = Math.Max(min, s.TopRadius), rm = Math.Max(min, s.MidRadius);
        Func<double, double> f;
        switch (s.Silhouette)
        {
            case ColumnSilhouette.Cone:
                f = t => rb + (rt - rb) * t;
                break;
            case ColumnSilhouette.Trumpet:
            {
                var p = Math.Clamp(s.FlarePower, 1, 12);
                f = t => rb + (rt - rb) * Math.Pow(t, p);
                break;
            }
            case ColumnSilhouette.Belly:
            case ColumnSilhouette.Hourglass:
                f = t => rb + (rt - rb) * t + (rm - (rb + rt) / 2) * Math.Sin(Math.PI * t);
                break;
            case ColumnSilhouette.Mushroom:
            {
                var t0 = Math.Clamp(s.MushroomStart, 0.05, 0.95);
                f = t =>
                {
                    if (t <= t0) return rb;
                    var u = (t - t0) / (1 - t0);
                    // abre rápido e termina horizontal (cúpula invertida)
                    return rb + (rt - rb) * Math.Sin(u * Math.PI / 2);
                };
                break;
            }
            case ColumnSilhouette.Formula:
            {
                if (!Expression.TryParse(s.RadiusFormula, out var e, out var err) || e == null)
                {
                    warnings.Add(Loc.F("Fórmula do raio inválida ({0}): usei o raio da base.", "Invalid radius formula ({0}): the base radius was used.", err ?? ""));
                    f = _ => rb;
                    break;
                }
                var hMm = U.ToMm(length);
                var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var failed = false;
                f = t =>
                {
                    vars["t"] = t;
                    vars["z"] = t * hMm;
                    vars["h"] = hMm;
                    try
                    {
                        var v = e.Evaluate(vars);
                        return double.IsFinite(v) ? U.Mm(v) : rb;
                    }
                    catch (ExpressionException)
                    {
                        failed = true;
                        return rb;
                    }
                };
                _ = f(0.5);
                if (failed) warnings.Add(Loc.T("A fórmula do raio usa uma variável desconhecida (use t, z ou h).", "The radius formula uses an unknown variable (use t, z or h)."));
                break;
            }
            case ColumnSilhouette.Drawn when s.DrawnSilhouette.Count >= 2:
            {
                var pts = s.DrawnSilhouette.OrderBy(p => p.Y).ToList();
                double z0 = pts[0].Y, z1 = pts[^1].Y;
                if (z1 - z0 < U.Mm(10))
                {
                    warnings.Add(Loc.T("Perfil desenhado sem altura: usei o raio da base.", "Drawn profile has no height: the base radius was used."));
                    f = _ => rb;
                    break;
                }
                f = t =>
                {
                    var z = z0 + (z1 - z0) * t;
                    for (var i = 1; i < pts.Count; i++)
                    {
                        if (z > pts[i].Y && i < pts.Count - 1) continue;
                        var a = pts[i - 1];
                        var b = pts[i];
                        var u = b.Y - a.Y < 1e-9 ? 0 : (z - a.Y) / (b.Y - a.Y);
                        return Math.Abs(a.X + (b.X - a.X) * Math.Clamp(u, 0, 1));
                    }
                    return Math.Abs(pts[^1].X);
                };
                break;
            }
            case ColumnSilhouette.Drawn:
                warnings.Add(Loc.T("Perfil desenhado sem linha válida: usei um cilindro.", "Drawn profile without a valid line: a cylinder was used."));
                f = _ => rb;
                break;
            default:
                f = _ => rb;
                break;
        }
        var clamped = false;
        double R(double t)
        {
            var v = f(Math.Clamp(t, 0, 1));
            if (v >= min) return v;
            clamped = true;
            return min;
        }
        // amostragem para avisar uma única vez
        for (var i = 0; i <= 20; i++) R(i / 20.0);
        if (clamped) warnings.Add(Loc.T("Em algum trecho o raio ficou zero ou negativo: limitei a 5 mm.", "Somewhere the radius became zero or negative: it was limited to 5 mm."));
        return R;
    }

    /// <summary>Centro e eixos (u, v) da seção no parâmetro t.</summary>
    private static (Vec3 C, Vec3 U, Vec3 V) Station(Ctx c, double t)
    {
        var s = c.S;
        t = Math.Clamp(t, 0, 1);
        if (c.Path.Count >= 2)
        {
            var f = t * (c.Path.Count - 1);
            var i = Math.Min((int)Math.Floor(f), c.Path.Count - 2);
            var u = f - i;
            var p = c.Path[i] + (c.Path[i + 1] - c.Path[i]) * u;
            if (!c.Perpendicular) return (p, Vec3.UnitX, Vec3.UnitY);
            var n = (c.Frames[i].N * (1 - u) + c.Frames[i + 1].N * u).Normalized();
            var b = (c.Frames[i].B * (1 - u) + c.Frames[i + 1].B * u).Normalized();
            return (p, n, b);
        }
        var z = t * c.Length;
        var dir = Vec2.FromAngle(s.LeanDirectionDeg * Math.PI / 180);
        var off = s.Spine switch
        {
            ColumnSpine.Inclined => z * Math.Tan(Math.Clamp(s.LeanDeg, -60, 60) * Math.PI / 180),
            ColumnSpine.Bow => s.BendOffset * Math.Sin(Math.PI * t),
            ColumnSpine.SCurve => s.BendOffset * (3 * t * t - 2 * t * t * t),
            _ => 0,
        };
        return (new Vec3(dir.X * off, dir.Y * off, z), Vec3.UnitX, Vec3.UnitY);
    }

    /// <summary>Ponto 2D da seção modulada (sem escala de raio) na amostra i do nível t.</summary>
    private static Vec2 Modulated(Ctx c, int i, double t)
    {
        var s = c.S;
        var p = c.Unit[i];
        var ang = p.Angle;
        var m = 1.0;
        if (s.Flutes > 0 && s.FluteDepth > 0)
            m *= 1 - Math.Clamp(s.FluteDepth, 0, 0.5) * Math.Pow(0.5 + 0.5 * Math.Cos(s.Flutes * ang), 2);
        if (Math.Abs(s.WaveAmplitude) > 1e-9)
            m *= 1 + Math.Clamp(s.WaveAmplitude, -0.6, 0.6) * Math.Sin(2 * Math.PI * s.WaveCount * t + s.WaveLobes * ang);
        var rot = (s.SectionRotationDeg + s.TwistDeg * t) * Math.PI / 180;
        return (p * m).Rotate(rot);
    }

    /// <summary>Anel do envelope no nível t, com o raio multiplicado por <paramref name="scale"/>.</summary>
    private static List<Vec3> Ring(Ctx c, double t, double scale, double inset = 0)
    {
        var (o, u, v) = Station(c, t);
        var r = c.Radius(t);
        var k = Math.Max(0.02, (r * scale - inset) / r);
        var list = new List<Vec3>(c.Unit.Count);
        for (var i = 0; i < c.Unit.Count; i++)
        {
            var p = Modulated(c, i, t) * (r * k);
            list.Add(o + u * p.X + v * p.Y);
        }
        return list;
    }

    private static List<Vec3> Circle(Ctx c, double t, double radius, int n)
    {
        var (o, u, v) = Station(c, t);
        return Enumerable.Range(0, n).Select(i =>
        {
            var a = 2 * Math.PI * i / n;
            return o + u * (radius * Math.Cos(a)) + v * (radius * Math.Sin(a));
        }).ToList();
    }

    /// <summary>Ponto do envelope no nível t, na posição fracionária f (0 a 1 em volta da seção), recuado de <paramref name="inset"/>.</summary>
    private static Vec3 SurfacePoint(Ctx c, double t, double f, double inset)
    {
        var n = c.Unit.Count;
        var x = ((f % 1) + 1) % 1 * n;
        var i = (int)Math.Floor(x) % n;
        var j = (i + 1) % n;
        var u = x - Math.Floor(x);
        var (o, ax, ay) = Station(c, t);
        var r = c.Radius(t);
        var p = Modulated(c, i, t) * (1 - u) + Modulated(c, j, t) * u;
        var len = p.Length * r;
        var scaled = len < 1e-9 ? p : p * (Math.Max(len * 0.05, len - inset) / len * r);
        return o + ax * scaled.X + ay * scaled.Y;
    }

    private static List<Vec3> ResamplePath(IReadOnlyList<Vec3> path, int n)
    {
        var total = MeshKit.PathLength(path);
        var result = new List<Vec3>(n);
        var seg = 0;
        var acc = 0.0;
        for (var k = 0; k < n; k++)
        {
            var target = total * k / (n - 1);
            while (seg < path.Count - 2 && acc + path[seg].DistanceTo(path[seg + 1]) < target)
            {
                acc += path[seg].DistanceTo(path[seg + 1]);
                seg++;
            }
            var l = path[seg].DistanceTo(path[seg + 1]);
            var u = l < 1e-12 ? 0 : Math.Clamp((target - acc) / l, 0, 1);
            result.Add(path[seg] + (path[seg + 1] - path[seg]) * u);
        }
        return result;
    }

    #endregion

    #region Construções

    private static void Shell(Ctx c, List<double> ss, List<List<Vec3>> envelope, ColumnResult r)
    {
        var thk = Math.Max(U.Mm(5), c.S.ShellThickness);
        var inner = ss.Select(t => Ring(c, t, 1, thk)).ToList();
        if (thk >= r.Quantities.MinRadius * 0.95)
            r.Warnings.Add(Loc.T("A espessura da casca é maior que o raio em algum trecho: lá ela fica praticamente maciça.",
                "The shell thickness exceeds the radius somewhere: it is practically solid there."));
        r.Meshes.Add(MeshKit.HollowLoft(envelope, inner, ColumnRole.Shaft));
    }

    private static void Bars(Ctx c, List<double> levels, ColumnResult r)
    {
        var s = c.S;
        var count = Math.Clamp(s.MemberCount, 3, 96);
        var size = Math.Max(U.Mm(10), s.MemberSize);
        var rad = size / 2;
        // as barras param meia espessura antes das pontas, para não atravessar o piso nem o teto
        double lo = levels[0] + rad / c.Length, hi = levels[^1] - rad / c.Length;
        if (hi <= lo) (lo, hi) = (levels[0], levels[^1]);
        var ss = levels.Select(t => lo + (hi - lo) * (t - levels[0]) / (levels[^1] - levels[0])).ToList();
        var q = r.Quantities;
        void Add(List<Vec3> path, string role, bool closed)
        {
            var mesh = MeshKit.Tube(path, rad, role, 8, closed);
            if (mesh.Triangles.Count == 0) return;
            r.Meshes.Add(mesh);
            q.MemberLength += MeshKit.PathLength(path) + (closed ? path[^1].DistanceTo(path[0]) : 0);
            q.MemberCount++;
        }

        if (s.Build is ColumnBuild.Ribs or ColumnBuild.Grid)
        {
            for (var i = 0; i < count; i++)
                Add(ss.Select(t => SurfacePoint(c, t, (double)i / count, rad)).ToList(), ColumnRole.Member, false);
        }
        else
        {
            var turns = s.LatticeTurns;
            foreach (var sign in new[] { 1, -1 })
                for (var i = 0; i < count; i++)
                {
                    var path = ss.Select((t, k) =>
                    {
                        var u = (double)k / (ss.Count - 1);
                        return SurfacePoint(c, t, (double)i / count + sign * turns * u, rad);
                    }).ToList();
                    Add(path, ColumnRole.Member, false);
                }
        }

        // anéis: sempre nas pontas; na grelha, distribuídos pela altura
        var rings = Math.Clamp(s.RingCount, 2, Math.Max(2, ss.Count));
        var samples = Math.Max(24, c.Unit.Count);
        for (var k = 0; k < rings; k++)
        {
            var idx = (int)Math.Round((ss.Count - 1) * (double)k / (rings - 1));
            var t = ss[idx];
            Add(Enumerable.Range(0, samples).Select(i => SurfacePoint(c, t, (double)i / samples, rad)).ToList(), ColumnRole.Ring, true);
        }
        // esbeltez da barra: trecho entre anéis (nervuras) ou entre cruzamentos (treliça)
        var span = s.Build == ColumnBuild.Lattice && Math.Abs(s.LatticeTurns) > 1e-6
            ? q.MemberLength / Math.Max(1, q.MemberCount) / Math.Max(1, Math.Abs(s.LatticeTurns) * count)
            : c.Length / Math.Max(1, rings - 1);
        q.MemberSlenderness = span / (size / 4);
    }

    private static void Lamellas(Ctx c, List<double> ss, ColumnResult r)
    {
        var s = c.S;
        var count = Math.Clamp(s.MemberCount, 3, 96);
        var thk = Math.Max(U.Mm(5), s.MemberSize);
        for (var i = 0; i < count; i++)
        {
            var f = (double)i / count;
            var rings = new List<IReadOnlyList<Vec3>>();
            foreach (var t in ss)
            {
                var (o, _, _) = Station(c, t);
                var outer = SurfacePoint(c, t, f, 0);
                var radial = outer - o;
                var len = radial.Length;
                if (len < 1e-9) continue;
                var dir = radial / len;
                var depth = s.CoreRadius > 1e-6 ? Math.Max(U.Mm(5), len - s.CoreRadius * 0.9) : Math.Min(s.MemberDepth, len * 0.98);
                var inner = outer - dir * depth;
                // direção tangente no plano da seção
                var (_, ax, ay) = Station(c, t);
                var nrm = ax.Cross(ay);
                var tan = nrm.Cross(dir).Normalized() * (thk / 2);
                rings.Add(new[] { outer - tan, outer + tan, inner + tan, inner - tan });
            }
            var mesh = MeshKit.Loft(rings, ColumnRole.Member);
            if (mesh.Triangles.Count == 0) continue;
            r.Meshes.Add(mesh);
            r.Quantities.MemberCount++;
        }
    }

    private static void Petals(Ctx c, double s0, double s1, int levels, ColumnResult r)
    {
        var s = c.S;
        var split = s0 + (s1 - s0) * Math.Clamp(s.PetalStart, 0.05, 0.95);
        var lowLevels = Math.Max(4, (int)(levels * (split - s0) / (s1 - s0)));
        var highLevels = Math.Max(6, levels - lowLevels + 1);
        var low = Enumerable.Range(0, lowLevels).Select(k => s0 + (split - s0) * k / (lowLevels - 1)).ToList();
        r.Meshes.Add(MeshKit.Loft(low.Select(t => Ring(c, t, 1)).ToList(), ColumnRole.Shaft));

        var count = Math.Clamp(s.MemberCount, 2, 36);
        var thk = Math.Max(U.Mm(5), s.ShellThickness);
        var gap = Math.Clamp(s.PetalGap, 0, 0.9);
        var q = Math.Max(4, c.Unit.Count / count + 1);
        var high = Enumerable.Range(0, highLevels).Select(k => split + (s1 - split) * k / (highLevels - 1)).ToList();
        for (var i = 0; i < count; i++)
        {
            var center = (double)i / count;
            var rings = new List<IReadOnlyList<Vec3>>();
            foreach (var (t, k) in high.Select((t, k) => (t, k)))
            {
                var u = (double)k / (high.Count - 1);
                var width = (1.0 / count) * (1 - gap) * Math.Max(0.08, 1 - Math.Clamp(s.PetalTaper, 0, 0.95) * u);
                var outer = Enumerable.Range(0, q).Select(j => SurfacePoint(c, t, center - width / 2 + width * j / (q - 1), 0)).ToList();
                var inner = Enumerable.Range(0, q).Select(j => SurfacePoint(c, t, center + width / 2 - width * j / (q - 1), thk)).ToList();
                rings.Add(outer.Concat(inner).ToList());
            }
            var mesh = MeshKit.Loft(rings, ColumnRole.Member);
            if (mesh.Triangles.Count == 0) continue;
            r.Meshes.Add(mesh);
            r.Quantities.MemberCount++;
        }
    }

    /// <summary>Base ou capitel: disco, placa quadrada ou prato (abre a partir do fuste).</summary>
    private static OrganicMesh End(Ctx c, ColumnEnd kind, double ta, double tb, double size, double shaftRadius, string role, bool top)
    {
        var half = Math.Max(U.Mm(20), size / 2);
        List<Vec3> Shape(double t, double radius, bool square)
        {
            var (o, u, v) = Station(c, t);
            if (square)
            {
                var rot = c.S.SectionRotationDeg * Math.PI / 180;
                return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                    .Select(p => new Vec2(p.Item1 * radius, p.Item2 * radius).Rotate(rot))
                    .Select(p => o + u * p.X + v * p.Y).ToList();
            }
            return Enumerable.Range(0, 48).Select(i =>
            {
                var a = 2 * Math.PI * i / 48;
                return o + u * (radius * Math.Cos(a)) + v * (radius * Math.Sin(a));
            }).ToList();
        }
        switch (kind)
        {
            case ColumnEnd.Square:
                return MeshKit.Loft(new[] { Shape(ta, half, true), Shape(tb, half, true) }, role);
            case ColumnEnd.Plate:
            {
                // prato: 70% da espessura é a curva de abertura, 30% é a borda
                var start = Math.Min(shaftRadius, half);
                var rim = ta + (tb - ta) * (top ? 0.7 : 0.3);
                var rings = top
                    ? new[] { Shape(ta, start, false), Shape(rim, half, false), Shape(tb, half, false) }
                    : new[] { Shape(ta, half, false), Shape(rim, half, false), Shape(tb, start, false) };
                return MeshKit.Loft(rings, role);
            }
            default:
                return MeshKit.Loft(new[] { Shape(ta, half, false), Shape(tb, half, false) }, role);
        }
    }

    #endregion

    #region Quantitativos e verificações

    private static void Quantify(Ctx c, List<double> ss, ColumnResult r)
    {
        var q = r.Quantities;
        foreach (var m in r.Meshes)
        {
            q.VolumeByRole[m.Role] = q.VolumeByRole.GetValueOrDefault(m.Role) + m.Volume;
            q.SurfaceArea += m.Area;
        }
        if (c.S.Build is ColumnBuild.Solid or ColumnBuild.Shell or ColumnBuild.Petals)
        {
            // seção mais fraca ao longo do fuste (coluna biapoiada, comprimento = altura)
            var minI = double.MaxValue;
            var area = 0.0;
            foreach (var t in ss)
            {
                var (o, u, v) = Station(c, t);
                var outer = Ring(c, t, 1).Select(p => new Vec2((p - o).Dot(u), (p - o).Dot(v))).ToList();
                var (ao, io) = OrganicSection.Properties(outer);
                if (c.S.Build == ColumnBuild.Shell)
                {
                    var inner = Ring(c, t, 1, Math.Max(U.Mm(5), c.S.ShellThickness)).Select(p => new Vec2((p - o).Dot(u), (p - o).Dot(v))).ToList();
                    var (ai, ii) = OrganicSection.Properties(inner);
                    ao -= ai;
                    io -= ii;
                }
                if (io < minI)
                {
                    minI = io;
                    area = ao;
                }
            }
            if (area > 1e-12 && minI > 0) q.Slenderness = c.Length / Math.Sqrt(minI / area);
        }
    }

    private static void Check(Ctx c, ColumnResult r)
    {
        var q = r.Quantities;
        var s = c.S;
        if (q.Slenderness > 140)
            r.Warnings.Add(Loc.F("Coluna muito esbelta (λ ≈ {0}). Aumente o raio na parte mais fina ou consulte o engenheiro.",
                "Very slender column (λ ≈ {0}). Increase the radius at the thinnest part or consult the engineer.", Math.Round(q.Slenderness)));
        else if (q.Slenderness > 90)
            r.Warnings.Add(Loc.F("Coluna esbelta (λ ≈ {0}): confirme o dimensionamento com o engenheiro.",
                "Slender column (λ ≈ {0}): confirm the sizing with the engineer.", Math.Round(q.Slenderness)));
        if (q.MemberSlenderness > 200)
            r.Warnings.Add(Loc.F("Barras muito esbeltas entre anéis (λ ≈ {0}): aumente a espessura da barra ou a quantidade de anéis.",
                "Very slender bars between rings (λ ≈ {0}): increase the bar size or the number of rings.", Math.Round(q.MemberSlenderness)));
        if (s.Build == ColumnBuild.Solid && q.MinRadius < U.Mm(50))
            r.Warnings.Add(Loc.F("Trecho com raio de {0} mm: muito fino para uma coluna maciça.",
                "Segment with a {0} mm radius: too thin for a solid column.", Math.Round(U.ToMm(q.MinRadius))));
        if (s.Build is ColumnBuild.Ribs or ColumnBuild.Grid or ColumnBuild.Lattice)
        {
            var spacing = 2 * Math.PI * q.MinRadius / Math.Max(1, s.MemberCount);
            if (spacing < s.MemberSize)
                r.Warnings.Add(Loc.T("As barras se tocam na parte mais estreita (barras demais ou grossas demais para o raio).",
                    "The bars touch at the narrowest part (too many or too thick for the radius)."));
        }
        if (s.Build == ColumnBuild.Lamellas && s.CoreRadius > 1e-6 && s.CoreRadius >= q.MinRadius)
            r.Warnings.Add(Loc.T("O núcleo é maior que o envelope em algum trecho: as lâminas somem lá.", "The core is larger than the envelope somewhere: the fins vanish there."));
        if (r.TriangleCount > 200_000)
            r.Warnings.Add(Loc.F("Malha pesada ({0} triângulos): reduza a resolução ou a quantidade de barras.",
                "Heavy mesh ({0} triangles): reduce the resolution or the number of bars.", r.TriangleCount));
    }

    #endregion
}
