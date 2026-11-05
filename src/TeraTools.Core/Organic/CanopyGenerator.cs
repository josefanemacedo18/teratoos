using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Forma da superfície da cobertura (altura em cada ponto da planta).</summary>
public enum CanopySurface
{
    /// <summary>Plana, com caimento numa direção.</summary>
    Flat,
    /// <summary>Abóbada: arco atravessado ao eixo.</summary>
    Vault,
    /// <summary>Almofada: sobe a partir do contorno, qualquer que seja a forma da planta.</summary>
    Pillow,
    /// <summary>Sela (paraboloide hiperbólico).</summary>
    Saddle,
    /// <summary>Ondas numa ou em duas direções.</summary>
    Waves,
    /// <summary>Guarda-chuvas sobre os apoios (normais ou invertidos, em funil).</summary>
    Umbrellas,
    /// <summary>Membrana tensionada: picos nos mastros, bordas ancoradas.</summary>
    Membrane,
    /// <summary>Bolhas ou depressões em volta de pontos atratores.</summary>
    Attractors,
    /// <summary>Altura por fórmula.</summary>
    Formula,
}

public enum CanopySkin
{
    /// <summary>Pele contínua com espessura.</summary>
    Continuous,
    /// <summary>Painéis com juntas.</summary>
    Panels,
    /// <summary>Lâminas paralelas (brises horizontais que seguem a forma).</summary>
    Slats,
    None,
}

public enum CanopyStructure
{
    None,
    Grid,
    Diagrid,
    /// <summary>Nervuras radiais e anéis em volta de cada apoio.</summary>
    Radial,
}

public enum CanopySupports
{
    None,
    /// <summary>Pilares retos até a cobertura.</summary>
    Straight,
    /// <summary>Árvores: tronco que se abre em galhos (um ou dois níveis).</summary>
    Tree,
}

public enum MemberProfile
{
    Rectangular,
    Tube,
}

/// <summary>Onde a cobertura se liga a colunas existentes.</summary>
public enum ColumnConnection
{
    Top,
    Intermediate,
    Both,
}

/// <summary>Coluna existente (do projeto) a que a cobertura pode se ligar. Cotas relativas à base da cobertura.</summary>
public sealed class CanopyColumn
{
    public Vec2 At { get; set; }
    public double BaseZ { get; set; }
    public double TopZ { get; set; }
}

public static class CanopyRole
{
    public const string Skin = "skin";
    public const string Panel = "panel";
    public const string Slat = "slat";
    public const string Structure = "structure";
    public const string Edge = "edge";
    public const string Support = "support";
    public const string Connector = "connector";

    public static string Label(string role) => role switch
    {
        Skin => Loc.T("Pele", "Skin"),
        Panel => Loc.T("Painéis", "Panels"),
        Slat => Loc.T("Lâminas", "Slats"),
        Structure => Loc.T("Estrutura", "Structure"),
        Edge => Loc.T("Viga de borda", "Edge beam"),
        Support => Loc.T("Apoios (troncos e galhos)", "Supports (trunks and branches)"),
        Connector => Loc.T("Ligações às colunas", "Column connections"),
        _ => role,
    };
}

/// <summary>Parâmetros da cobertura orgânica (pés). A planta está em coordenadas do projeto; as cotas, a partir da base.</summary>
public sealed class CanopySettings
{
    public Polygon2 Footprint { get; set; } = Polygon2.Rectangle(Vec2.Zero, U.M(8), U.M(6));

    // superfície
    public CanopySurface Surface { get; set; } = CanopySurface.Pillow;
    /// <summary>Altura mínima da superfície (beiral) acima da base.</summary>
    public double Height { get; set; } = U.M(3.5);
    /// <summary>Quanto a superfície sobe acima da altura mínima.</summary>
    public double Rise { get; set; } = U.M(1.2);
    public double DirectionDeg { get; set; }
    public double SlopePercent { get; set; } = 3;
    public double WavesU { get; set; } = 2;
    public double WavesV { get; set; }
    public double WavePhaseDeg { get; set; }
    public bool UmbrellaInverted { get; set; } = true;
    /// <summary>Forma do perfil (1 = reto; maior = mais curvo/agudo).</summary>
    public double Sharpness { get; set; } = 1.6;
    public double InfluenceRadius { get; set; } = U.M(4);
    public List<Vec2> Attractors { get; set; } = new();
    public bool AttractorsDown { get; set; }
    public string Formula { get; set; } = "800*sin(180*u)*sin(180*v)";
    /// <summary>Pontos dos apoios próprios (planta).</summary>
    public List<Vec2> Supports { get; set; } = new();

    // pele
    public CanopySkin Skin { get; set; } = CanopySkin.Continuous;
    public double SkinThickness { get; set; } = U.Mm(60);
    public double PanelSizeU { get; set; } = U.Mm(1500);
    public double PanelSizeV { get; set; } = U.Mm(1500);
    public double PanelJoint { get; set; } = U.Mm(20);
    public double PanelRotationDeg { get; set; }
    public double SlatSpacing { get; set; } = U.Mm(300);
    public double SlatDepth { get; set; } = U.Mm(150);
    public double SlatThickness { get; set; } = U.Mm(40);
    public double SlatDirectionDeg { get; set; }
    /// <summary>Tamanho da malha da superfície (0 = automático).</summary>
    public double MeshSize { get; set; }

    // estrutura
    public CanopyStructure Structure { get; set; } = CanopyStructure.Grid;
    public double StructureSpacingU { get; set; } = U.M(1.5);
    public double StructureSpacingV { get; set; } = U.M(1.5);
    public double StructureRotationDeg { get; set; }
    public MemberProfile Profile { get; set; } = MemberProfile.Rectangular;
    public double MemberWidth { get; set; } = U.Mm(80);
    public double MemberHeight { get; set; } = U.Mm(200);
    public bool EdgeBeam { get; set; } = true;
    public int RadialCount { get; set; } = 12;
    public int RadialRings { get; set; } = 3;

    // apoios
    public CanopySupports SupportKind { get; set; } = CanopySupports.Tree;
    public double TrunkDiameter { get; set; } = U.Mm(300);
    public double ForkHeight { get; set; } = U.M(2.4);
    public int Branches { get; set; } = 4;
    public int BranchLevels { get; set; } = 2;
    public double BranchReach { get; set; } = U.M(2.2);
    public double BranchSpreadDeg { get; set; } = 25;
    public double BranchRotationDeg { get; set; } = 45;

    // colunas existentes
    public List<CanopyColumn> Columns { get; set; } = new();
    public ColumnConnection Connection { get; set; } = ColumnConnection.Top;
    public double IntermediateFraction { get; set; } = 0.6;

    public double MinClearance { get; set; } = U.M(2.1);
}

public sealed class CanopyQuantities
{
    public double PlanArea { get; set; }
    public double SkinArea { get; set; }
    public double SkinVolume { get; set; }
    public int PanelCount { get; set; }
    public double SlatLength { get; set; }
    public double StructureLength { get; set; }
    public double EdgeLength { get; set; }
    public double SupportLength { get; set; }
    public int SupportCount { get; set; }
    public int ConnectionCount { get; set; }
    public double MinHeight { get; set; }
    public double MaxHeight { get; set; }
    /// <summary>Menor altura livre embaixo (sob pele e estrutura).</summary>
    public double MinClearance { get; set; }
    public int PondingPoints { get; set; }
    public Dictionary<string, double> VolumeByRole { get; } = new();
}

public sealed class CanopyResult
{
    public List<OrganicMesh> Meshes { get; } = new();
    public List<string> Warnings { get; } = new();
    public CanopyQuantities Quantities { get; } = new();
    /// <summary>Linhas da estrutura em planta (para a pré-visualização).</summary>
    public List<List<Vec2>> PlanLines { get; } = new();
    public int TriangleCount => Meshes.Sum(m => m.Triangles.Count);
}

/// <summary>Altura da superfície em cada ponto da planta (campo de alturas).</summary>
public sealed class CanopyField
{
    private readonly CanopySettings _s;
    private readonly Polygon2 _poly;
    private readonly Vec2 _c;
    private readonly Vec2 _ax;
    private readonly Vec2 _ay;
    private readonly double _u0, _u1, _v0, _v1;
    private readonly double _dmax;
    private readonly List<Vec2> _supports;
    private readonly double _supportReach;
    private readonly Expression? _expr;
    private readonly Dictionary<string, double> _vars = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Warnings { get; } = new();
    public CanopySurface Mode { get; }

    public CanopyField(CanopySettings s)
    {
        _s = s;
        _poly = s.Footprint.EnsureCounterClockwise();
        _c = _poly.Centroid;
        _ax = Vec2.FromAngle(s.DirectionDeg * Math.PI / 180);
        _ay = _ax.Perp;
        _u0 = _v0 = double.MaxValue;
        _u1 = _v1 = double.MinValue;
        foreach (var p in _poly)
        {
            var d = p - _c;
            _u0 = Math.Min(_u0, d.Dot(_ax)); _u1 = Math.Max(_u1, d.Dot(_ax));
            _v0 = Math.Min(_v0, d.Dot(_ay)); _v1 = Math.Max(_v1, d.Dot(_ay));
        }
        // maior distância ao contorno (para a almofada e a membrana)
        var b = _poly.Bounds;
        _dmax = 1e-6;
        for (var i = 0; i <= 40; i++)
            for (var j = 0; j <= 40; j++)
            {
                var p = new Vec2(b.Min.X + b.Width * i / 40, b.Min.Y + b.Height * j / 40);
                if (_poly.Contains(p)) _dmax = Math.Max(_dmax, _poly.DistanceToBoundary(p));
            }
        _supports = s.Supports.Count > 0 ? s.Supports : s.Columns.Select(c => c.At).ToList();
        _supportReach = _supports.Count == 0 ? 1 : Math.Max(U.Mm(100), _poly.Max(p => _supports.Min(q => q.DistanceTo(p))));
        Mode = s.Surface;
        if ((Mode is CanopySurface.Umbrellas or CanopySurface.Membrane) && _supports.Count == 0)
        {
            Warnings.Add(Loc.T("Guarda-chuvas e membrana precisam de apoios: sem eles, usei a almofada.",
                "Umbrellas and membrane need supports: without them, the pillow was used."));
            Mode = CanopySurface.Pillow;
        }
        if (Mode == CanopySurface.Attractors && s.Attractors.Count == 0 && _supports.Count == 0)
        {
            Warnings.Add(Loc.T("Sem pontos atratores: usei a almofada.", "No attractor points: the pillow was used."));
            Mode = CanopySurface.Pillow;
        }
        if (Mode == CanopySurface.Formula)
        {
            if (!Expression.TryParse(s.Formula, out _expr, out var err) || _expr == null)
            {
                Warnings.Add(Loc.F("Fórmula inválida ({0}): usei a superfície plana.", "Invalid formula ({0}): a flat surface was used.", err ?? ""));
                Mode = CanopySurface.Flat;
            }
        }
    }

    public double MaxInset => _dmax;

    /// <summary>Cota da superfície (topo da pele) acima da base.</summary>
    public double Z(Vec2 p)
    {
        var s = _s;
        var d = p - _c;
        var lu = d.Dot(_ax);
        var lv = d.Dot(_ay);
        var u01 = (_u1 - _u0) < 1e-9 ? 0.5 : (lu - _u0) / (_u1 - _u0);
        var v01 = (_v1 - _v0) < 1e-9 ? 0.5 : (lv - _v0) / (_v1 - _v0);
        var uu = 2 * u01 - 1;
        var vv = 2 * v01 - 1;
        var h = s.Height;
        var rise = s.Rise;
        var pw = Math.Clamp(s.Sharpness, 0.3, 6);
        switch (Mode)
        {
            case CanopySurface.Flat:
                return h + s.SlopePercent / 100 * (lu - _u0);
            case CanopySurface.Vault:
                return h + rise * (1 - vv * vv);
            case CanopySurface.Saddle:
                return h + rise * (1 + uu * uu - vv * vv) / 2;
            case CanopySurface.Waves:
            {
                var ph = s.WavePhaseDeg * Math.PI / 180;
                var a = Math.Sin(2 * Math.PI * s.WavesU * u01 + ph);
                var w = s.WavesV > 0 ? a * Math.Sin(2 * Math.PI * s.WavesV * v01 + ph) : a;
                return h + rise * (0.5 + 0.5 * w);
            }
            case CanopySurface.Umbrellas:
            {
                var t = SupportParameter(p);
                return s.UmbrellaInverted ? h + rise * Math.Pow(t, pw) : h + rise * (1 - Math.Pow(t, pw));
            }
            case CanopySurface.Membrane:
            {
                var r = Math.Max(U.Mm(100), s.InfluenceRadius);
                var peak = _supports.Max(q => Math.Pow(Math.Max(0, 1 - q.DistanceTo(p) / r), pw));
                var edge = Math.Min(1, _poly.DistanceToBoundary(p) / Math.Max(U.Mm(100), _dmax * 0.35));
                var anchor = 1 - (1 - edge) * (1 - edge);
                return h + rise * peak * anchor;
            }
            case CanopySurface.Attractors:
            {
                var r = Math.Max(U.Mm(100), s.InfluenceRadius);
                var pts = s.Attractors.Count > 0 ? s.Attractors : _supports;
                var sum = Math.Min(1, pts.Sum(q => Math.Exp(-Math.Pow(q.DistanceTo(p) / r, 2))));
                return s.AttractorsDown ? h + rise * (1 - sum) : h + rise * sum;
            }
            case CanopySurface.Formula:
            {
                _vars["u"] = u01;
                _vars["v"] = v01;
                _vars["x"] = U.ToMm(lu);
                _vars["y"] = U.ToMm(lv);
                _vars["r"] = U.ToMm(d.Length);
                _vars["d"] = U.ToMm(_poly.DistanceToBoundary(p));
                try
                {
                    var v = _expr!.Evaluate(_vars);
                    return double.IsFinite(v) ? h + U.Mm(v) : h;
                }
                catch (ExpressionException)
                {
                    return h;
                }
            }
            default:
            {
                var t = Math.Clamp(_poly.DistanceToBoundary(p) / _dmax, 0, 1);
                if (!_poly.Contains(p)) t = 0;
                return h + rise * (1 - Math.Pow(1 - t, 2));
            }
        }
    }

    /// <summary>0 no apoio mais próximo, 1 na divisa com o vizinho (ou na borda, com um apoio só).</summary>
    private double SupportParameter(Vec2 p)
    {
        double d1 = double.MaxValue, d2 = double.MaxValue;
        foreach (var q in _supports)
        {
            var d = q.DistanceTo(p);
            if (d < d1) { d2 = d1; d1 = d; }
            else if (d < d2) d2 = d;
        }
        if (_supports.Count == 1) return Math.Clamp(d1 / _supportReach, 0, 1);
        return Math.Clamp(2 * d1 / Math.Max(1e-9, d1 + d2), 0, 1);
    }

    /// <summary>Índice do apoio mais próximo (para recortar as nervuras radiais por apoio).</summary>
    public int NearestSupport(Vec2 p)
    {
        var best = -1;
        var bd = double.MaxValue;
        for (var i = 0; i < _supports.Count; i++)
        {
            var d = _supports[i].DistanceTo(p);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public IReadOnlyList<Vec2> SupportPoints => _supports;
}

/// <summary>
/// Gerador de coberturas orgânicas: superfície (campo de alturas sobre qualquer planta), pele
/// (contínua, painéis ou lâminas), estrutura (grelha, diagrid, radial, viga de borda) e apoios
/// (árvores, pilares retos ou ligação a colunas existentes no topo e em pontos intermediários).
/// </summary>
public static class CanopyGenerator
{
    public static CanopyResult Generate(CanopySettings s)
    {
        var r = new CanopyResult();
        var poly = s.Footprint.EnsureCounterClockwise().Simplify(1e-6);
        if (poly.IsEmpty || poly.Area < U.M2ToSqFt(0.5))
        {
            r.Warnings.Add(Loc.T("Contorno da cobertura inválido ou pequeno demais.", "Canopy outline invalid or too small."));
            return r;
        }
        s.Footprint = poly;
        var f = new CanopyField(s);
        r.Warnings.AddRange(f.Warnings);
        var q = r.Quantities;
        q.PlanArea = poly.Area;
        var b = poly.Bounds;
        var extent = Math.Max(b.Width, b.Height);
        var cell = s.MeshSize > 1e-6 ? s.MeshSize : Math.Clamp(extent / 50, U.Mm(150), U.Mm(800));
        var skinThk = s.Skin is CanopySkin.Continuous or CanopySkin.Panels ? Math.Max(U.Mm(5), s.SkinThickness) : 0;
        var structDepth = s.Structure != CanopyStructure.None || s.EdgeBeam ? Math.Max(U.Mm(20), s.MemberHeight) : 0;
        var slatDepth = s.Skin == CanopySkin.Slats ? Math.Max(U.Mm(10), s.SlatDepth) : 0;
        // fundo de tudo que pende da superfície (para os apoios encostarem)
        double Underside(Vec2 p) => f.Z(p) - skinThk - Math.Max(structDepth, slatDepth);

        Skin(s, f, poly, cell, skinThk, r);
        Structure(s, f, poly, skinThk, r);
        Supports(s, f, poly, Underside, r);

        // quantitativos e verificações (amostragem da superfície)
        Sample(s, f, poly, Underside, r);
        foreach (var m in r.Meshes)
            q.VolumeByRole[m.Role] = q.VolumeByRole.GetValueOrDefault(m.Role) + m.Volume;
        q.SkinVolume = q.VolumeByRole.GetValueOrDefault(CanopyRole.Skin) + q.VolumeByRole.GetValueOrDefault(CanopyRole.Panel);
        if (r.TriangleCount > 400_000)
            r.Warnings.Add(Loc.F("Malha pesada ({0} triângulos): aumente o tamanho da malha, dos painéis ou o espaçamento.",
                "Heavy mesh ({0} triangles): increase the mesh size, panel size or spacing.", r.TriangleCount));
        r.Meshes.RemoveAll(m => m.Triangles.Count == 0);
        return r;
    }

    #region Pele

    private static void Skin(CanopySettings s, CanopyField f, Polygon2 poly, double cell, double thk, CanopyResult r)
    {
        var q = r.Quantities;
        switch (s.Skin)
        {
            case CanopySkin.Continuous:
            {
                var m = MeshKit.SurfaceSlab(poly, cell, f.Z, thk, CanopyRole.Skin);
                r.Meshes.Add(m);
                q.SkinArea = TopArea(m);
                break;
            }
            case CanopySkin.Panels:
            {
                var rot = s.PanelRotationDeg * Math.PI / 180;
                var su = Math.Max(U.Mm(100), s.PanelSizeU);
                var sv = Math.Max(U.Mm(100), s.PanelSizeV);
                var joint = Math.Clamp(s.PanelJoint, 0, Math.Min(su, sv) / 2);
                // juntas: a planta e cada célula encolhem meia junta; o recorte é feito por triângulos (exato em plantas côncavas)
                var shrunkOutline = joint > 1e-6 ? poly.Offset(-joint / 2) : poly;
                if (shrunkOutline.IsEmpty) shrunkOutline = poly;
                var pieces = MeshKit.ConvexPieces(shrunkOutline);
                foreach (var cellPoly in Cells(poly, su, sv, rot))
                {
                    var shrunkCell = joint > 1e-6 ? cellPoly.Offset(-joint / 2) : cellPoly;
                    if (shrunkCell.IsEmpty) continue;
                    var parts = pieces.Select(t => t.ClipByConvex(shrunkCell)).Where(t => !t.IsEmpty && t.Area > 1e-8).ToList();
                    if (parts.Sum(t => t.Area) < U.Mm(30) * U.Mm(30)) continue;
                    var m = MeshKit.SurfaceSlab(parts, Math.Min(cell, Math.Max(su, sv) / 2), f.Z, thk, CanopyRole.Panel, rot);
                    if (m.Triangles.Count == 0) continue;
                    r.Meshes.Add(m);
                    q.PanelCount++;
                    q.SkinArea += TopArea(m);
                }
                break;
            }
            case CanopySkin.Slats:
            {
                var t = Math.Max(U.Mm(5), s.SlatThickness);
                var depth = Math.Max(U.Mm(10), s.SlatDepth);
                var profile = Rect(t, depth);
                var dir = s.SlatDirectionDeg * Math.PI / 180;
                foreach (var path in Lines(poly, Math.Max(U.Mm(30), s.SlatSpacing), dir, f, 0, Math.Min(cell, U.Mm(300))))
                {
                    var m = MeshKit.Sweep(path, profile, Vec3.UnitZ, CanopyRole.Slat);
                    if (m.Triangles.Count == 0) continue;
                    r.Meshes.Add(m);
                    q.SlatLength += MeshKit.PathLength(path);
                    r.PlanLines.Add(path.Select(p => p.XY).ToList());
                }
                break;
            }
        }
    }

    private static double TopArea(OrganicMesh m) =>
        m.Triangles.Sum(t =>
        {
            var a = m.Vertices[t.A];
            var n = (m.Vertices[t.B] - a).Cross(m.Vertices[t.C] - a);
            return n.Z > 0 ? n.Length / 2 : 0;
        });

    private static List<Vec2> Rect(double width, double height) =>
        new() { new(-width / 2, -height), new(width / 2, -height), new(width / 2, 0), new(-width / 2, 0) };

    /// <summary>Células retangulares (rotacionadas, convexas) da grade que tocam a planta.</summary>
    private static IEnumerable<Polygon2> Cells(Polygon2 poly, double su, double sv, double rot)
    {
        var c = poly.Centroid;
        var ax = Vec2.FromAngle(rot);
        var ay = ax.Perp;
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var p in poly)
        {
            var d = p - c;
            u0 = Math.Min(u0, d.Dot(ax)); u1 = Math.Max(u1, d.Dot(ax));
            v0 = Math.Min(v0, d.Dot(ay)); v1 = Math.Max(v1, d.Dot(ay));
        }
        // grade centrada: sobras iguais dos dois lados
        var nu = (int)Math.Ceiling((u1 - u0) / su);
        var nv = (int)Math.Ceiling((v1 - v0) / sv);
        var su0 = (u0 + u1) / 2 - nu * su / 2;
        var sv0 = (v0 + v1) / 2 - nv * sv / 2;
        Vec2 W(double u, double v) => c + ax * u + ay * v;
        for (var i = 0; i < nu; i++)
            for (var j = 0; j < nv; j++)
            {
                double a0 = su0 + i * su, a1 = a0 + su, b0 = sv0 + j * sv, b1 = b0 + sv;
                var cellPoly = new Polygon2(new[] { W(a0, b0), W(a1, b0), W(a1, b1), W(a0, b1) });
                if (PolygonOps.Intersects(poly, cellPoly)) yield return cellPoly;
            }
    }

    /// <summary>
    /// Linhas paralelas (direção <paramref name="dir"/>, espaçamento <paramref name="spacing"/>) recortadas pela
    /// planta e projetadas na superfície, rebaixadas de <paramref name="drop"/>.
    /// </summary>
    private static IEnumerable<List<Vec3>> Lines(Polygon2 poly, double spacing, double dir, CanopyField f, double drop, double step)
    {
        var c = poly.Centroid;
        var ax = Vec2.FromAngle(dir);
        var ay = ax.Perp;
        double v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var p in poly)
        {
            var d = (p - c).Dot(ay);
            v0 = Math.Min(v0, d);
            v1 = Math.Max(v1, d);
        }
        var n = (int)Math.Floor((v1 - v0) / spacing);
        var start = (v0 + v1) / 2 - n * spacing / 2;
        for (var k = 0; k <= n; k++)
        {
            var v = start + k * spacing;
            if (v <= v0 + 1e-6 || v >= v1 - 1e-6) continue;
            var origin = c + ay * v;
            foreach (var (a, b) in Clip(poly, origin, ax))
                yield return Sample(a, b, f, drop, step);
        }
    }

    /// <summary>Trechos de uma reta infinita que ficam dentro da planta.</summary>
    private static List<(Vec2 A, Vec2 B)> Clip(Polygon2 poly, Vec2 origin, Vec2 dir)
    {
        var ts = new List<double>();
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var e = b - a;
            var den = dir.Cross(e);
            if (Math.Abs(den) < 1e-12) continue;
            var w = a - origin;
            var t = w.Cross(e) / den;
            var u = w.Cross(dir) / den;
            if (u >= -1e-9 && u < 1 - 1e-9) ts.Add(t);
        }
        ts.Sort();
        var list = new List<(Vec2, Vec2)>();
        for (var i = 0; i + 1 < ts.Count; i += 2)
        {
            var a = origin + dir * ts[i];
            var b = origin + dir * ts[i + 1];
            if (a.DistanceTo(b) > U.Mm(50) && poly.Contains((a + b) / 2)) list.Add((a, b));
        }
        return list;
    }

    private static List<Vec3> Sample(Vec2 a, Vec2 b, CanopyField f, double drop, double step)
    {
        var len = a.DistanceTo(b);
        var n = Math.Max(2, (int)Math.Ceiling(len / Math.Max(U.Mm(50), step)));
        return Enumerable.Range(0, n + 1).Select(i =>
        {
            var p = Vec2.Lerp(a, b, (double)i / n);
            return new Vec3(p.X, p.Y, f.Z(p) - drop);
        }).ToList();
    }

    #endregion

    #region Estrutura

    private static void Structure(CanopySettings s, CanopyField f, Polygon2 poly, double skinThk, CanopyResult r)
    {
        var q = r.Quantities;
        var w = Math.Max(U.Mm(10), s.MemberWidth);
        var h = Math.Max(U.Mm(10), s.MemberHeight);
        var profile = s.Profile == MemberProfile.Tube
            ? Enumerable.Range(0, 10).Select(i => Vec2.FromAngle(2 * Math.PI * i / 10, h / 2) + new Vec2(0, -h / 2)).ToList()
            : Rect(w, h);
        var step = Math.Clamp(Math.Max(poly.Bounds.Width, poly.Bounds.Height) / 60, U.Mm(100), U.Mm(400));
        void Add(List<Vec3> path, string role, bool closed = false)
        {
            var m = MeshKit.Sweep(path, profile, Vec3.UnitZ, role, closed);
            if (m.Triangles.Count == 0) return;
            r.Meshes.Add(m);
            var len = MeshKit.PathLength(path) + (closed ? path[^1].DistanceTo(path[0]) : 0);
            if (role == CanopyRole.Edge) q.EdgeLength += len;
            else q.StructureLength += len;
            r.PlanLines.Add(path.Select(p => p.XY).ToList());
        }
        // as vigas ficam um pouco recuadas da borda quando há viga de borda (para não se sobreporem)
        var inner = s.EdgeBeam ? poly.Offset(-w) : poly;
        if (inner.IsEmpty) inner = poly;
        var rot = s.StructureRotationDeg * Math.PI / 180;
        var su = Math.Max(U.Mm(100), s.StructureSpacingU);
        var sv = Math.Max(U.Mm(100), s.StructureSpacingV);
        switch (s.Structure)
        {
            case CanopyStructure.Grid:
                foreach (var p in Lines(inner, sv, rot, f, skinThk, step)) Add(p, CanopyRole.Structure);
                foreach (var p in Lines(inner, su, rot + Math.PI / 2, f, skinThk, step)) Add(p, CanopyRole.Structure);
                break;
            case CanopyStructure.Diagrid:
                foreach (var p in Lines(inner, sv, rot + Math.PI / 4, f, skinThk, step)) Add(p, CanopyRole.Structure);
                foreach (var p in Lines(inner, su, rot - Math.PI / 4, f, skinThk, step)) Add(p, CanopyRole.Structure);
                break;
            case CanopyStructure.Radial:
                Radial(s, f, inner, skinThk, step, Add, r);
                break;
        }
        if (s.EdgeBeam)
        {
            var edge = poly.Offset(-w / 2);
            if (!edge.IsEmpty)
            {
                var pts = new List<Vec3>();
                for (var i = 0; i < edge.Count; i++)
                {
                    var a = edge[i];
                    var b = edge[(i + 1) % edge.Count];
                    var n = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / step));
                    for (var k = 0; k < n; k++)
                    {
                        var p = Vec2.Lerp(a, b, (double)k / n);
                        pts.Add(new Vec3(p.X, p.Y, f.Z(p) - skinThk));
                    }
                }
                Add(pts, CanopyRole.Edge, closed: true);
            }
        }
    }

    private static void Radial(CanopySettings s, CanopyField f, Polygon2 poly, double drop, double step, Action<List<Vec3>, string, bool> add, CanopyResult r)
    {
        var supports = f.SupportPoints;
        var centers = supports.Count > 0 ? supports.ToList() : new List<Vec2> { poly.Centroid };
        var count = Math.Clamp(s.RadialCount, 3, 72);
        var rings = Math.Clamp(s.RadialRings, 0, 30);
        var reach = Math.Max(poly.Bounds.Width, poly.Bounds.Height);
        bool Mine(Vec2 p, int i) => poly.Contains(p) && (supports.Count == 0 || f.NearestSupport(p) == i);
        for (var i = 0; i < centers.Count; i++)
        {
            var c = centers[i];
            var maxLen = 0.0;
            for (var k = 0; k < count; k++)
            {
                var dir = Vec2.FromAngle(2 * Math.PI * k / count + s.StructureRotationDeg * Math.PI / 180);
                var pts = new List<Vec3>();
                for (var t = 0.0; t <= reach; t += step)
                {
                    var p = c + dir * t;
                    if (!Mine(p, i)) break;
                    pts.Add(new Vec3(p.X, p.Y, f.Z(p) - drop));
                }
                if (pts.Count >= 2)
                {
                    add(pts, CanopyRole.Structure, false);
                    maxLen = Math.Max(maxLen, pts[0].XY.DistanceTo(pts[^1].XY));
                }
            }
            // anéis concêntricos (arcos onde saem da planta ou do domínio do apoio)
            for (var j = 1; j <= rings; j++)
            {
                var rad = maxLen * j / (rings + 1);
                if (rad < U.Mm(200)) continue;
                var samples = Math.Max(24, (int)(2 * Math.PI * rad / step));
                var arc = new List<Vec3>();
                var all = true;
                var arcs = new List<List<Vec3>>();
                for (var k = 0; k < samples; k++)
                {
                    var p = c + Vec2.FromAngle(2 * Math.PI * k / samples, rad);
                    if (Mine(p, i)) arc.Add(new Vec3(p.X, p.Y, f.Z(p) - drop));
                    else
                    {
                        all = false;
                        if (arc.Count >= 2) arcs.Add(arc);
                        arc = new List<Vec3>();
                    }
                }
                if (all)
                {
                    add(arc, CanopyRole.Structure, true);
                    continue;
                }
                if (arc.Count >= 2)
                {
                    // junta o último trecho com o primeiro quando o arco passa pelo ângulo zero
                    if (arcs.Count > 0 && Mine(c + Vec2.FromAngle(0, rad), i)) arcs[0] = arc.Concat(arcs[0]).ToList();
                    else arcs.Add(arc);
                }
                foreach (var a in arcs) add(a, CanopyRole.Structure, false);
            }
        }
    }

    #endregion

    #region Apoios

    private static void Supports(CanopySettings s, CanopyField f, Polygon2 poly, Func<Vec2, double> underside, CanopyResult r)
    {
        var q = r.Quantities;
        var trunkR = Math.Max(U.Mm(20), s.TrunkDiameter / 2);
        void Tube(List<Vec3> path, double radius, string role)
        {
            var m = MeshKit.Tube(path, radius, role, 12);
            if (m.Triangles.Count == 0) return;
            r.Meshes.Add(m);
            q.SupportLength += MeshKit.PathLength(path);
        }
        void Node(Vec3 c, double radius, string role) => r.Meshes.Add(MeshKit.Sphere(c, radius, role, 14));

        // alvo do galho: ponto da superfície em volta do apoio, puxado para dentro da planta se preciso
        Vec3 Target(Vec2 from, double angle, double reach)
        {
            var dir = Vec2.FromAngle(angle);
            for (var k = 1.0; k > 0.15; k -= 0.1)
            {
                var p = from + dir * (reach * k);
                if (poly.Contains(p) && poly.DistanceToBoundary(p) > trunkR) return new Vec3(p.X, p.Y, underside(p));
            }
            return new Vec3(from.X, from.Y, underside(from));
        }

        // galhos a partir de um ponto (forquilha) até a superfície; um ou dois níveis
        void Branches(Vec3 fork, Vec2 at, double radius, string role)
        {
            var n = Math.Clamp(s.Branches, 1, 12);
            var levels = Math.Clamp(s.BranchLevels, 1, 2);
            var reach = Math.Max(U.Mm(200), s.BranchReach);
            var rot = s.BranchRotationDeg * Math.PI / 180;
            var spread = Math.Clamp(s.BranchSpreadDeg, 0, 80) * Math.PI / 180;
            Node(fork, radius * 1.25, role);
            for (var i = 0; i < n; i++)
            {
                var a = rot + 2 * Math.PI * i / n;
                if (levels == 1)
                {
                    var t = Target(at, a, reach);
                    if (t.Z <= fork.Z + U.Mm(50)) continue;
                    var ctrl = new Vec3(fork.X, fork.Y, fork.Z + (t.Z - fork.Z) * 0.6);
                    Tube(MeshKit.Bezier(fork, ctrl, t, 14), radius * 0.6, role);
                    continue;
                }
                var t1 = Target(at, a, reach * 0.5);
                var split = new Vec3((fork.X + t1.X) / 2, (fork.Y + t1.Y) / 2, fork.Z + (t1.Z - fork.Z) * 0.6);
                if (split.Z <= fork.Z + U.Mm(50)) continue;
                var c1 = new Vec3(fork.X, fork.Y, fork.Z + (split.Z - fork.Z) * 0.6);
                Tube(MeshKit.Bezier(fork, c1, split, 10), radius * 0.6, role);
                Node(split, radius * 0.6 * 1.2, role);
                foreach (var side in new[] { -1, 1 })
                {
                    var t2 = Target(at, a + side * spread, reach);
                    if (t2.Z <= split.Z + U.Mm(30)) continue;
                    var c2 = split + (split - fork).Normalized() * (split.DistanceTo(fork) * 0.35);
                    c2 = new Vec3(c2.X, c2.Y, Math.Min(c2.Z, t2.Z - U.Mm(20)));
                    Tube(MeshKit.Bezier(split, c2, t2, 10), radius * 0.4, role);
                }
            }
        }

        if (s.SupportKind != CanopySupports.None)
            foreach (var p in s.Supports)
            {
                if (!poly.Contains(p))
                {
                    r.Warnings.Add(Loc.T("Um apoio está fora da planta da cobertura e foi ignorado.", "A support is outside the canopy outline and was ignored."));
                    continue;
                }
                var top = underside(p);
                if (top < U.Mm(300)) continue;
                q.SupportCount++;
                if (s.SupportKind == CanopySupports.Straight)
                {
                    Tube(new List<Vec3> { new(p.X, p.Y, 0), new(p.X, p.Y, top) }, trunkR, CanopyRole.Support);
                    continue;
                }
                var forkZ = Math.Clamp(s.ForkHeight, U.Mm(300), Math.Max(U.Mm(300), top - U.Mm(300)));
                var fork = new Vec3(p.X, p.Y, forkZ);
                Tube(new List<Vec3> { new(p.X, p.Y, 0), fork }, trunkR, CanopyRole.Support);
                Branches(fork, p, trunkR, CanopyRole.Support);
            }

        foreach (var col in s.Columns)
        {
            var p = col.At;
            if (!poly.Contains(p))
            {
                r.Warnings.Add(Loc.T("Uma coluna escolhida está fora da planta da cobertura: não foi ligada.", "A picked column is outside the canopy outline: not connected."));
                continue;
            }
            var under = underside(p);
            var connectorR = Math.Max(U.Mm(20), trunkR * 0.5);
            if (s.Connection is ColumnConnection.Top or ColumnConnection.Both)
            {
                var gap = under - col.TopZ;
                if (gap > U.Mm(5))
                {
                    Tube(new List<Vec3> { new(p.X, p.Y, col.TopZ), new(p.X, p.Y, under) }, connectorR, CanopyRole.Connector);
                    q.ConnectionCount++;
                }
                else if (gap < -U.Mm(5))
                    r.Warnings.Add(Loc.F("Uma coluna passa {0} mm acima da face inferior da cobertura.", "A column rises {0} mm above the canopy underside.", Math.Round(U.ToMm(-gap))));
                else q.ConnectionCount++;
            }
            if (s.Connection is ColumnConnection.Intermediate or ColumnConnection.Both)
            {
                var z = col.BaseZ + (col.TopZ - col.BaseZ) * Math.Clamp(s.IntermediateFraction, 0.05, 0.98);
                if (z < under - U.Mm(300))
                {
                    Branches(new Vec3(p.X, p.Y, z), p, connectorR, CanopyRole.Connector);
                    q.ConnectionCount++;
                }
            }
        }
    }

    #endregion

    #region Verificações

    private static void Sample(CanopySettings s, CanopyField f, Polygon2 poly, Func<Vec2, double> underside, CanopyResult r)
    {
        var q = r.Quantities;
        var b = poly.Bounds;
        const int n = 48;
        var dx = b.Width / n;
        var dy = b.Height / n;
        var grid = new double?[n + 1, n + 1];
        q.MinHeight = double.MaxValue;
        q.MaxHeight = double.MinValue;
        q.MinClearance = double.MaxValue;
        var maxSlope = 0.0;
        for (var i = 0; i <= n; i++)
            for (var j = 0; j <= n; j++)
            {
                var p = new Vec2(b.Min.X + dx * i, b.Min.Y + dy * j);
                if (!poly.Contains(p)) continue;
                var z = f.Z(p);
                grid[i, j] = z;
                q.MinHeight = Math.Min(q.MinHeight, z);
                q.MaxHeight = Math.Max(q.MaxHeight, z);
                q.MinClearance = Math.Min(q.MinClearance, underside(p));
            }
        // empoçamento: ponto interno mais baixo que os 8 vizinhos
        for (var i = 1; i < n; i++)
            for (var j = 1; j < n; j++)
            {
                if (grid[i, j] is not { } z) continue;
                var lowest = true;
                var complete = true;
                for (var a = -1; a <= 1 && lowest; a++)
                    for (var c = -1; c <= 1; c++)
                    {
                        if (a == 0 && c == 0) continue;
                        if (grid[i + a, j + c] is not { } zn) { complete = false; break; }
                        if (zn <= z + U.Mm(1)) { lowest = false; break; }
                    }
                if (lowest && complete) q.PondingPoints++;
            }
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                if (grid[i, j] is { } z0 && grid[i + 1, j] is { } z1 && grid[i, j + 1] is { } z2)
                    maxSlope = Math.Max(maxSlope, Math.Max(Math.Abs(z1 - z0) / dx, Math.Abs(z2 - z0) / dy));
        if (q.MinHeight == double.MaxValue) q.MinHeight = q.MaxHeight = q.MinClearance = 0;

        var holdsWater = s.Skin is CanopySkin.Continuous or CanopySkin.Panels;
        if (!holdsWater) q.PondingPoints = 0;
        if (q.PondingPoints > 0)
            r.Warnings.Add(Loc.F("A água pode empoçar em {0} ponto(s) da superfície: preveja ralos ou mude a forma.",
                "Water may pond at {0} point(s): plan drains or change the shape.", q.PondingPoints));
        if (holdsWater && maxSlope < 0.01)
            r.Warnings.Add(Loc.T("Superfície quase plana (caimento < 1%): preveja inclinação para a água escoar.",
                "Almost flat surface (slope < 1%): plan a slope for drainage."));
        if (q.MinClearance < s.MinClearance)
            r.Warnings.Add(Loc.F("Altura livre mínima embaixo da cobertura: {0} mm (menor que {1} mm).",
                "Minimum clear height under the canopy: {0} mm (less than {1} mm).", Math.Round(U.ToMm(q.MinClearance)), Math.Round(U.ToMm(s.MinClearance))));
        foreach (var w in f.Warnings.Where(w => !r.Warnings.Contains(w))) r.Warnings.Add(w);
    }

    #endregion
}
