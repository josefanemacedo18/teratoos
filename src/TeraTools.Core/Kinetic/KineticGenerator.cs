using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Kinetic;

public enum HingeAxis
{
    Vertical,
    Horizontal,
}

public enum HingePosition
{
    Center,
    Start,
    End,
}

public enum KineticMode
{
    /// <summary>Todos os painéis com a mesma abertura.</summary>
    Uniform,
    /// <summary>Abertura escalonada ao longo de uma direção (efeito dominó).</summary>
    Stagger,
    /// <summary>Onda senoidal que percorre a fachada conforme o estado.</summary>
    Wave,
    /// <summary>Variação aleatória controlada (semente).</summary>
    Random,
    /// <summary>Abertura conforme a distância a um ponto atrator.</summary>
    Attractor,
    /// <summary>Fórmula livre (variáveis i, j, u, v, t, r, d, cols, rows).</summary>
    Formula,
}

public sealed class KineticSettings
{
    public int Columns { get; set; } = 8;
    public int Rows { get; set; } = 3;
    public double PanelWidth { get; set; } = U.Mm(600);
    public double PanelHeight { get; set; } = U.Mm(1000);
    public double Gap { get; set; } = U.Mm(20);
    public double Thickness { get; set; } = U.Mm(30);
    public double OffsetFromFacade { get; set; } = U.Mm(150);
    public HingeAxis Axis { get; set; } = HingeAxis.Vertical;
    public HingePosition Hinge { get; set; } = HingePosition.Center;
    public double ClosedAngleDeg { get; set; }
    public double OpenAngleDeg { get; set; } = 80;

    /// <summary>Estado global de abertura (0 = fechado, 1 = aberto).</summary>
    public double Opening { get; set; } = 0.5;

    public KineticMode Mode { get; set; } = KineticMode.Uniform;
    public double DirectionDeg { get; set; }
    public double StaggerAmount { get; set; } = 0.6;
    public double WavePeriods { get; set; } = 1;
    public double WavePhaseDeg { get; set; }
    public double AttractorU { get; set; } = 0.5;
    public double AttractorV { get; set; } = 0.5;
    public double AttractorRadius { get; set; } = 0.6;
    public bool InvertAttractor { get; set; }
    public string Formula { get; set; } = "t";
    public int Seed { get; set; } = 5;
    public double RandomSpread { get; set; } = 0.5;
}

public sealed class KineticPanel
{
    public int I { get; set; }
    public int J { get; set; }
    public double OpeningFraction { get; set; }
    public double AngleDeg { get; set; }
    public Vec3 HingePoint { get; set; }
    public Vec3 HingeDirection { get; set; }
    public OrientedBox ClosedBox { get; set; }
    public OrientedBox Box { get; set; }

    /// <summary>Quatro cantos do plano médio do painel (sentido anti-horário).</summary>
    public Vec3[] Corners { get; set; } = Array.Empty<Vec3>();
}

/// <summary>Painéis móveis paramétricos: calcula a posição de cada painel para um estado de abertura.</summary>
public static class KineticGenerator
{
    /// <param name="facade">
    /// Sistema da fachada: origem no canto inferior esquerdo do conjunto, X ao longo da fachada,
    /// Y para cima no plano da fachada e Z para fora (normal).
    /// </param>
    /// <param name="state">Estado global 0..1 (substitui <see cref="KineticSettings.Opening"/> quando informado).</param>
    public static List<KineticPanel> Evaluate(KineticSettings s, Frame3 facade, double? state = null)
    {
        if (s.Columns < 1 || s.Rows < 1)
            throw new ArgumentException(Loc.T("Informe ao menos 1 coluna e 1 linha.", "At least 1 column and 1 row are required."));
        var t = MathUtil.Clamp01(state ?? s.Opening);
        Expression? formula = s.Mode == KineticMode.Formula ? Expression.Parse(s.Formula) : null;
        var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var dir = Vec2.FromAngle(MathUtil.ToRadians(s.DirectionDeg));
        var corners = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1), new Vec2(1, 1) };
        var dMin = corners.Min(c => c.Dot(dir));
        var dMax = corners.Max(c => c.Dot(dir));
        var list = new List<KineticPanel>(s.Columns * s.Rows);

        for (var j = 0; j < s.Rows; j++)
        {
            for (var i = 0; i < s.Columns; i++)
            {
                var u = s.Columns == 1 ? 0.5 : (double)i / (s.Columns - 1);
                var v = s.Rows == 1 ? 0.5 : (double)j / (s.Rows - 1);
                var d = MathUtil.InverseLerp(dMin, dMax, new Vec2(u, v).Dot(dir));
                double f;
                switch (s.Mode)
                {
                    case KineticMode.Stagger:
                        var k = MathUtil.Clamp01(s.StaggerAmount);
                        f = k >= 1 ? (t >= d ? 1 : 0) : MathUtil.Clamp01((t - d * k) / (1 - k));
                        break;
                    case KineticMode.Wave:
                        f = t * (0.5 + 0.5 * Math.Sin(2 * Math.PI * (s.WavePeriods * d - t) + MathUtil.ToRadians(s.WavePhaseDeg)));
                        break;
                    case KineticMode.Random:
                        f = MathUtil.Clamp01(t + (Rng.Hash(i, j, s.Seed) - 0.5) * 2 * s.RandomSpread * t);
                        break;
                    case KineticMode.Attractor:
                        var dist = new Vec2(u, v).DistanceTo(new Vec2(s.AttractorU, s.AttractorV));
                        var w = 1 - MathUtil.Clamp01(dist / Math.Max(1e-6, s.AttractorRadius));
                        if (s.InvertAttractor) w = 1 - w;
                        f = t * w;
                        break;
                    case KineticMode.Formula:
                        vars["i"] = i;
                        vars["j"] = j;
                        vars["u"] = u;
                        vars["v"] = v;
                        vars["t"] = t;
                        vars["d"] = d;
                        vars["r"] = Rng.Hash(i, j, s.Seed);
                        vars["cols"] = s.Columns;
                        vars["rows"] = s.Rows;
                        f = formula!.Evaluate(vars);
                        break;
                    default:
                        f = t;
                        break;
                }
                f = MathUtil.Clamp01(double.IsNaN(f) ? 0 : f);
                var angle = MathUtil.Lerp(s.ClosedAngleDeg, s.OpenAngleDeg, f);
                list.Add(BuildPanel(s, facade, i, j, f, angle));
            }
        }
        return list;
    }

    private static KineticPanel BuildPanel(KineticSettings s, Frame3 facade, int i, int j, double f, double angleDeg)
    {
        var x0 = i * (s.PanelWidth + s.Gap);
        var y0 = j * (s.PanelHeight + s.Gap);
        var z = s.OffsetFromFacade;

        // Caixa fechada: no plano da fachada, centrada na espessura em z.
        var closedOrigin = facade.ToWorld(x0, y0, z - s.Thickness / 2);
        var closed = new OrientedBox(new Frame3(closedOrigin, facade.XAxis, facade.YAxis, facade.ZAxis), s.PanelWidth, s.PanelHeight, s.Thickness);

        Vec3 hingePoint;
        Vec3 hingeDir;
        if (s.Axis == HingeAxis.Vertical)
        {
            var hx = s.Hinge switch { HingePosition.Start => 0, HingePosition.End => s.PanelWidth, _ => s.PanelWidth / 2 };
            hingePoint = facade.ToWorld(x0 + hx, y0, z);
            hingeDir = facade.YAxis;
        }
        else
        {
            var hy = s.Hinge switch { HingePosition.Start => 0, HingePosition.End => s.PanelHeight, _ => s.PanelHeight / 2 };
            hingePoint = facade.ToWorld(x0, y0 + hy, z);
            hingeDir = facade.XAxis;
        }

        // Sentido de abertura: a borda livre se afasta da fachada (+Z).
        var sign = s.Axis == HingeAxis.Vertical
            ? (s.Hinge == HingePosition.End ? 1 : -1)
            : (s.Hinge == HingePosition.End ? -1 : 1);
        var rad = MathUtil.ToRadians(angleDeg) * sign;
        var frame = closed.Frame.RotatedAround(hingePoint, hingeDir, rad);
        var box = closed with { Frame = frame };

        var mid = new[]
        {
            frame.ToWorld(0, 0, s.Thickness / 2),
            frame.ToWorld(s.PanelWidth, 0, s.Thickness / 2),
            frame.ToWorld(s.PanelWidth, s.PanelHeight, s.Thickness / 2),
            frame.ToWorld(0, s.PanelHeight, s.Thickness / 2),
        };

        return new KineticPanel
        {
            I = i,
            J = j,
            OpeningFraction = f,
            AngleDeg = angleDeg,
            HingePoint = hingePoint,
            HingeDirection = hingeDir,
            ClosedBox = closed,
            Box = box,
            Corners = mid,
        };
    }
}
