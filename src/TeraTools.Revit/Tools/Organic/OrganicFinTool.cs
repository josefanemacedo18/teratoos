using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Facade;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Organic;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Organic;

public sealed class OrganicFinOptions
{
    private const string GDist = "Distribuição e altura|Distribution and height";
    private const string GSec = "Seção da lâmina|Fin section";
    private const string GRot = "Rotação, torção e estado cinético|Rotation, twist and kinetic state";
    private const string GTrim = "Recortes da base e do topo|Bottom and top trims";
    private const string GBand = "Faixas horizontais|Horizontal bands";
    private const string GOut = "Materiais e saída|Materials and output";

    [Opt("Espaçamento entre lâminas (eixo a eixo)", "Fin spacing (center to center)", Group = GDist, Kind = OptKind.Length, Min = 0.02, VisibleIf = "Count=0")]
    public double Spacing { get; set; } = U.Mm(400);

    [Opt("Quantidade fixa (0 = pelo espaçamento)", "Fixed count (0 = by spacing)", Group = GDist, Kind = OptKind.Integer, Min = 0, Max = 5000)]
    public int Count { get; set; }

    [Opt("Margem no início", "Start margin", Group = GDist, Kind = OptKind.Length, Min = 0)]
    public double StartMargin { get; set; } = U.Mm(200);

    [Opt("Margem no fim", "End margin", Group = GDist, Kind = OptKind.Length, Min = 0)]
    public double EndMargin { get; set; } = U.Mm(200);

    [Opt("Base das lâminas (acima da base da parede/linha)", "Fin bottom (above the wall/line base)", Group = GDist, Kind = OptKind.Length)]
    public double BottomZ { get; set; }

    [Opt("Topo das lâminas", "Fin top", Group = GDist, Kind = OptKind.Length, Min = 0.1)]
    public double TopZ { get; set; } = U.M(6);

    [Opt("Lado", "Side", Group = GDist, Tip = "Na parede, o lado externo já vem escolhido; troque se as lâminas aparecerem do lado de dentro.|On a wall the exterior side is preselected; switch if the fins show up inside.")]
    public FinSide Side { get; set; } = FinSide.Left;

    [Opt("Afastamento da fachada", "Distance from the facade", Group = GDist, Kind = OptKind.Length, Min = 0)]
    public double Offset { get; set; } = U.Mm(150);

    [Opt("Seção", "Section", Group = GSec)]
    public FinProfile Profile { get; set; } = FinProfile.Plate;

    [Opt("Profundidade (ou diâmetro do tubo)", "Depth (or tube diameter)", Group = GSec, Kind = OptKind.Length, Min = 0.005)]
    public double Depth { get; set; } = U.Mm(350);

    [Opt("Espessura", "Thickness", Group = GSec, Kind = OptKind.Length, Min = 0.002, VisibleIf = "Profile!=Round")]
    public double Thickness { get; set; } = U.Mm(30);

    [Opt("Profundidade no topo (% da base)", "Depth at the top (% of the bottom)", Group = GSec, Kind = OptKind.Percent, Min = 5, Max = 500)]
    public double TopDepth { get; set; } = 100;

    [Opt("Eixo de giro", "Pivot", Group = GSec)]
    public FinPivot Pivot { get; set; } = FinPivot.Center;

    [Opt("Ângulo base (0 = perpendicular à fachada)", "Base angle (0 = perpendicular to the facade)", Group = GRot, Kind = OptKind.Angle)]
    public double BaseAngle { get; set; }

    [Opt("Variação ao longo da fachada", "Variation along the facade", Group = GRot)]
    public VariationMode Variation { get; set; } = VariationMode.Sine;

    [Opt("Amplitude da rotação", "Rotation amplitude", Group = GRot, Kind = OptKind.Angle, Min = -180, Max = 180, VisibleIf = "Variation!=None&Variation!=Formula")]
    public double Amplitude { get; set; } = 45;

    [Opt("Ondas ao longo da fachada", "Waves along the facade", Group = GRot, Kind = OptKind.Number, Min = 0.1, Max = 50, VisibleIf = "Variation=Sine")]
    public double Periods { get; set; } = 1.5;

    [Opt("Fase", "Phase", Group = GRot, Kind = OptKind.Angle, VisibleIf = "Variation=Sine")]
    public double Phase { get; set; }

    [Opt("Posição do atrator (% da fachada)", "Attractor position (% of the facade)", Group = GRot, Kind = OptKind.Percent, Min = 0, Max = 100, VisibleIf = "Variation=Attractor")]
    public double AttractorPosition { get; set; } = 50;

    [Opt("Alcance do atrator (% da fachada)", "Attractor reach (% of the facade)", Group = GRot, Kind = OptKind.Percent, Min = 1, Max = 100, VisibleIf = "Variation=Attractor")]
    public double AttractorRadius { get; set; } = 30;

    [Opt("Semente", "Seed", Group = GRot, Kind = OptKind.Integer, Min = 0, Max = 99999, VisibleIf = "Variation=Random")]
    public int Seed { get; set; } = 7;

    [Opt("Fórmula do ângulo (graus)", "Angle formula (degrees)", Group = GRot, Kind = OptKind.Formula, VisibleIf = "Variation=Formula",
        Tip = "Variáveis: u (0–1 ao longo da fachada), h (0–1 na altura da lâmina), i (nº), n (total), x e z (m). Ex.: 45*sin(360*u + 90*h)|Variables: u, h, i, n, x and z (m). E.g. 45*sin(360*u + 90*h)")]
    public string Formula { get; set; } = "45*sin(360*u + 90*h)";

    [Opt("Torção da base ao topo", "Twist from bottom to top", Group = GRot, Kind = OptKind.Angle, Min = -720, Max = 720, VisibleIf = "Variation!=Formula")]
    public double Twist { get; set; }

    [Opt("A torção acompanha a variação", "Twist follows the variation", Group = GRot, VisibleIf = "Variation!=None&Variation!=Formula")]
    public bool TwistFollowsVariation { get; set; }

    [Opt("Estado de abertura (cinético)", "Opening state (kinetic)", Group = GRot, Kind = OptKind.Percent, Min = 0, Max = 100,
        Tip = "0% = todas no ângulo base (fechadas); 100% = variação completa. Gere estados diferentes para estudos e renders.|0% = all at the base angle (closed); 100% = full variation.")]
    public double Opening { get; set; } = 100;

    [Opt("Variação da profundidade (%)", "Depth variation (%)", Group = GRot, Kind = OptKind.Percent, Min = 0, Max = 95, VisibleIf = "Variation!=None&Variation!=Formula")]
    public double DepthVariation { get; set; }

    [Opt("Base das lâminas", "Fin bottom", Group = GTrim)]
    public FinTrim BottomTrim { get; set; } = FinTrim.Flat;

    [Opt("Variação da base (desnível ou altura da onda)", "Bottom variation (rise or wave height)", Group = GTrim, Kind = OptKind.Length, VisibleIf = "BottomTrim=Slope,Wave")]
    public double BottomAmount { get; set; } = U.M(1);

    [Opt("Topo das lâminas", "Fin top", Group = GTrim)]
    public FinTrim TopTrim { get; set; } = FinTrim.Flat;

    [Opt("Variação do topo (desnível, onda ou flecha do arco)", "Top variation (rise, wave or arch rise)", Group = GTrim, Kind = OptKind.Length, VisibleIf = "TopTrim=Slope,Wave,Arch")]
    public double TopAmount { get; set; } = U.M(1);

    [Opt("Ondas no recorte", "Waves in the trim", Group = GTrim, Kind = OptKind.Number, Min = 0.1, Max = 30, VisibleIf = "BottomTrim=Wave|TopTrim=Wave")]
    public double TrimWaves { get; set; } = 2;

    [Opt("Centro do arco (% da fachada)", "Arch center (% of the facade)", Group = GTrim, Kind = OptKind.Percent, Min = 0, Max = 100, VisibleIf = "BottomTrim=Arch|TopTrim=Arch")]
    public double ArchCenter { get; set; } = 50;

    [Opt("Largura do arco", "Arch width", Group = GTrim, Kind = OptKind.Length, Min = 0.2, VisibleIf = "BottomTrim=Arch|TopTrim=Arch")]
    public double ArchWidth { get; set; } = U.M(4);

    [Opt("Altura do nascimento do arco", "Arch springing height", Group = GTrim, Kind = OptKind.Length, Min = 0, VisibleIf = "BottomTrim=Arch")]
    public double ArchSpring { get; set; } = U.M(2.2);

    [Opt("Altura do fecho do arco", "Arch crown height", Group = GTrim, Kind = OptKind.Length, Min = 0, VisibleIf = "BottomTrim=Arch")]
    public double ArchCrown { get; set; } = U.M(3.6);

    [Opt("Faixas (quantidade, distribuídas na altura)", "Bands (count, spread along the height)", Group = GBand, Kind = OptKind.Integer, Min = 0, Max = 50)]
    public int BandCount { get; set; }

    [Opt("Alturas das faixas em mm (ex.: 3000; 6000)", "Band heights in mm (e.g. 3000; 6000)", Group = GBand, Kind = OptKind.Text,
        Tip = "Se preenchido, vale no lugar da quantidade.|When filled, replaces the count.")]
    public string BandHeights { get; set; } = "";

    [Opt("Profundidade da faixa", "Band depth", Group = GBand, Kind = OptKind.Length, Min = 0.01, VisibleIf = "BandCount!=0|BandHeights!=")]
    public double BandDepth { get; set; } = U.Mm(500);

    [Opt("Altura da faixa", "Band height", Group = GBand, Kind = OptKind.Length, Min = 0.01, VisibleIf = "BandCount!=0|BandHeights!=")]
    public double BandThickness { get; set; } = U.Mm(150);

    [Opt("Afastamento da faixa", "Band distance", Group = GBand, Kind = OptKind.Length, Min = 0, VisibleIf = "BandCount!=0|BandHeights!=")]
    public double BandOffset { get; set; }

    [Opt("Material do conjunto 1", "Set 1 material", Group = GOut, Kind = OptKind.Material)]
    public string Material1 { get; set; } = "";

    [Opt("Material do conjunto 2", "Set 2 material", Group = GOut, Kind = OptKind.Material)]
    public string Material2 { get; set; } = "";

    [Opt("Material do conjunto 3", "Set 3 material", Group = GOut, Kind = OptKind.Material)]
    public string Material3 { get; set; } = "";

    [Opt("Sequência de conjuntos (ex.: 1,1,2)", "Set sequence (e.g. 1,1,2)", Group = GOut, Kind = OptKind.Text)]
    public string MaterialSequence { get; set; } = "1";

    [Opt("Material das faixas", "Band material", Group = GOut, Kind = OptKind.Material)]
    public string BandMaterial { get; set; } = "";

    [Opt("Categoria no Revit", "Revit category", Group = GOut)]
    public OrganicCategory Category { get; set; } = OrganicCategory.GenericModel;

    [Opt("Escolher de novo as linhas de recorte", "Pick the trim lines again", Group = GOut, VisibleIf = "BottomTrim=Drawn|TopTrim=Drawn")]
    public bool RepickDrawn { get; set; }

    public FinFacadeSettings ToCore(FinContext c) => new()
    {
        Spacing = Spacing,
        Count = Count,
        StartMargin = StartMargin,
        EndMargin = EndMargin,
        BottomZ = BottomZ,
        TopZ = TopZ,
        Side = Side,
        Offset = Offset + c.FacadeOffset,
        Profile = Profile,
        Depth = Depth,
        Thickness = Thickness,
        TopDepthFactor = TopDepth / 100,
        Pivot = Pivot,
        BaseAngleDeg = BaseAngle,
        Variation = Variation,
        AmplitudeDeg = Amplitude,
        Periods = Periods,
        PhaseDeg = Phase,
        AttractorPosition = AttractorPosition / 100,
        AttractorRadius = AttractorRadius / 100,
        Seed = Seed,
        Formula = Formula,
        TwistDeg = Twist,
        TwistFollowsVariation = TwistFollowsVariation,
        Opening = Opening / 100,
        DepthVariation = DepthVariation / 100,
        BottomTrim = BottomTrim,
        TopTrim = TopTrim,
        BottomAmount = BottomAmount,
        TopAmount = TopAmount,
        TrimWaves = TrimWaves,
        ArchCenter = ArchCenter / 100,
        ArchWidth = ArchWidth,
        ArchSpring = ArchSpring,
        ArchCrown = ArchCrown,
        BottomCurve = OrganicGeo.Unpack3(c.BottomCurve).Select(p => new Vec3(p.X, p.Y, p.Z - c.BaseZ)).ToList(),
        TopCurve = OrganicGeo.Unpack3(c.TopCurve).Select(p => new Vec3(p.X, p.Y, p.Z - c.BaseZ)).ToList(),
        BandCount = BandCount,
        BandHeights = BandHeights,
        BandDepth = BandDepth,
        BandThickness = BandThickness,
        BandOffset = BandOffset + c.FacadeOffset,
        MaterialSequence = MaterialSequence,
    };

    public void ApplyStyle(FinFacadeSettings s)
    {
        Profile = s.Profile;
        Depth = s.Depth;
        Thickness = s.Thickness;
        Spacing = s.Spacing;
        Count = 0;
        Variation = s.Variation;
        Amplitude = s.AmplitudeDeg;
        Periods = s.Periods;
        Phase = s.PhaseDeg;
        AttractorPosition = s.AttractorPosition * 100;
        AttractorRadius = s.AttractorRadius * 100;
        Seed = s.Seed;
        BaseAngle = s.BaseAngleDeg;
        Twist = s.TwistDeg;
        TwistFollowsVariation = s.TwistFollowsVariation;
        DepthVariation = s.DepthVariation * 100;
        BottomTrim = s.BottomTrim;
        TopTrim = s.TopTrim;
        BottomAmount = s.BottomAmount;
        TopAmount = s.TopAmount;
        TrimWaves = s.TrimWaves;
        ArchCenter = s.ArchCenter * 100;
        ArchWidth = s.ArchWidth;
        ArchSpring = s.ArchSpring;
        ArchCrown = s.ArchCrown;
        BandCount = s.BandCount;
        BandHeights = s.BandHeights;
        BandDepth = s.BandDepth;
        BandThickness = s.BandThickness;
        BandOffset = s.BandOffset;
        Opening = 100;
    }
}

/// <summary>Caminho da fachada (planta), base e linhas de recorte (coordenadas do projeto).</summary>
public sealed class FinContext
{
    public List<double[]> Path { get; set; } = new();
    public double BaseZ { get; set; }
    /// <summary>Meia espessura da parede (as lâminas contam o afastamento a partir da face externa).</summary>
    public double FacadeOffset { get; set; }
    public long WallId { get; set; } = -1;
    public List<double[]>? BottomCurve { get; set; }
    public List<double[]>? TopCurve { get; set; }
    public string Style { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("org.fins", Modules.Organic, "Lâminas de fachada", "Facade fins", Order = 30, Large = true,
    DescPt = "Fachadas de lâminas personalizadas — chapa, tubo, losango, elipse ou gota — que giram em onda, por atrator, aleatórias ou por fórmula, torcem na altura, abrem em arco sobre entradas e têm faixas horizontais e estado cinético.",
    DescEn = "Custom fin facades — plate, tube, diamond, ellipse or airfoil — rotating by wave, attractor, random or formula, twisting along the height, opening as arches over entrances, with bands and a kinetic state.",
    HelpPt = "1. Escolha um estilo pronto (ou suas últimas opções).\n2. Escolha a parede (as lâminas ficam do lado externo) ou as linhas do caminho em planta (retas ou curvas).\n3. Ajuste seção, rotação, torção, recortes da base e do topo (inclinado, onda, arco de entrada ou linha desenhada em elevação), faixas e materiais com a pré-visualização 3D.\n4. Gere. Cada lâmina vira uma forma livre numerada; o relatório traz a lista de corte (comprimentos iguais agrupados) e a rotação de cada lâmina.\n\nO \"Estado de abertura\" de 0% a 100% gira tudo do ângulo base até a variação completa: gere estados diferentes para estudos de insolação e renders.\nPara alterar depois: Editar sistema gerado.",
    HelpEn = "Pick a style and a wall or path, adjust section, rotation, twist, trims (slope, wave, entrance arch or drawn line), bands and materials with live 3D preview. Each fin is a numbered free form; the report has the cut list and rotations.",
    Keywords = "laminas aletas brise cinetico fachada torcida cobre ondas arco entrada parametrico fins louvers kinetic facade twist copper")]
public sealed class OrganicFinCommand : ToolCommand
{
    private const string ToolId = "org.fins";

    public override void Run(ToolContext ctx)
    {
        var last = new FinStyle("", Loc.T("Minhas últimas opções", "My last options"), Loc.T("Abre o diálogo com as opções da última vez.", "Opens the dialog with your last options."), _ => { });
        var style = Pick.One(new[] { last }.Concat(FinStyles.All), s => s.Name, ctx.Tool.Name,
            Loc.T("Escolha um ponto de partida. Tudo pode ser ajustado depois.", "Pick a starting point. Everything can be adjusted next."), ctx.Owner, s => s.Description);
        if (style == null) ctx.Cancel();
        var c = new FinContext { Style = style!.Id };
        var o = ctx.LoadOptions<OrganicFinOptions>();
        if (style.Id != "")
        {
            var s = new FinFacadeSettings();
            style.Apply(s);
            o.ApplyStyle(s);
        }

        var i = Msg.Choose(Loc.T("Onde ficam as lâminas?", "Where do the fins go?"), ctx.Tool.Name,
            Loc.T("Na face externa de uma parede", "On the exterior face of a wall"), Loc.T("Ao longo de linhas em planta", "Along lines in plan"));
        if (i < 0) ctx.Cancel();
        if (i == 0)
        {
            var wall = (Wall)ctx.PickElement("Escolha a parede", "Pick the wall", e => e is Wall);
            var (path, _, z0, z1, ext) = GenHost.WallFacade(wall);
            c.Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList();
            c.BaseZ = z0;
            c.WallId = wall.Id.Value;
            c.FacadeOffset = wall.Width / 2;
            o.Side = path.TangentAt(0).Perp.Dot(ext) >= 0 ? FinSide.Left : FinSide.Right;
            o.BottomZ = 0;
            o.TopZ = Math.Max(U.M(1), z1 - z0);
        }
        else
        {
            var curves = OrganicGeo.PickCurves(ctx, "Selecione as linhas do caminho (em planta)", "Select the path lines (in plan)");
            var path = GenHost.PathFromCurves(curves);
            c.Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList();
            c.BaseZ = curves[0].GetEndPoint(0).Z;
        }
        if (c.Path.Count < 2) ToolContext.Fail("Não foi possível ler o caminho.", "Could not read the path.");
        o = AskWithDrawings(ctx, o, c, Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static OrganicFinOptions AskWithDrawings(ToolContext ctx, OrganicFinOptions o, FinContext c, string okText)
    {
        for (var round = 0; round < 6; round++)
        {
            o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: okText, key: ToolId);
            var picked = false;
            if (o.BottomTrim == FinTrim.Drawn && (o.RepickDrawn || c.BottomCurve is not { Count: >= 2 }))
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione a linha da BASE das lâminas (desenhada numa elevação da fachada)", "Select the fin BOTTOM line (drawn in a facade elevation)");
                c.BottomCurve = OrganicGeo.Pack(OrganicGeo.ReadOpenPath(curves));
                picked = true;
            }
            if (o.TopTrim == FinTrim.Drawn && (o.RepickDrawn || c.TopCurve is not { Count: >= 2 }))
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione a linha do TOPO das lâminas (desenhada numa elevação da fachada)", "Select the fin TOP line (drawn in a facade elevation)");
                c.TopCurve = OrganicGeo.Pack(OrganicGeo.ReadOpenPath(curves));
                picked = true;
            }
            o.RepickDrawn = false;
            if (!picked) return o;
        }
        return o;
    }

    private static FinFacadeResult Compute(ToolContext ctx, OrganicFinOptions o, FinContext c)
    {
        var path = new PolylinePath(c.Path.Select(p => new Vec2(p[0], p[1])));
        return FinFacadeGenerator.Generate(o.ToCore(c), path, Math.Max(100, ctx.Settings.ElementHardLimit));
    }

    private static readonly string[] SetColors = { "#B5652B", "#7A8C99", "#C9A66B" };

    private static PreviewModel Preview(ToolContext ctx, OrganicFinOptions o, FinContext c)
    {
        var r = Compute(ctx, o, c);
        var pm = new PreviewModel();
        var pts = c.Path.Select(p => new Vec2(p[0], p[1])).ToList();
        for (var k = 0; k + 1 < pts.Count; k++) pm.Line(pts[k], pts[k + 1], "#5F6B7A", 2);
        foreach (var f in r.Fins.Take(3000))
            if (f.Footprint.Count >= 3) pm.Polygon(new Polygon2(f.Footprint), SetColors[Math.Clamp(f.Material - 1, 0, 2)], "#6B4A33", 0.4);
        var center = pts.Aggregate(Vec2.Zero, (a, b) => a + b) / pts.Count;
        var off = new Vec3(-center.X, -center.Y, 0);
        var sc = pm.S3;
        foreach (var f in r.Fins.Take(4000))
            sc.Triangles(f.Mesh.Vertices.Select(v => v + off).ToList(), f.Mesh.Triangles, SetColors[Math.Clamp(f.Material - 1, 0, 2)]);
        foreach (var b in r.Bands)
            sc.Triangles(b.Vertices.Select(v => v + off).ToList(), b.Triangles, "#8A9099");
        // fachada (semitransparente) para referência
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var a = new Vec3(pts[k].X, pts[k].Y, 0) + off;
            var b = new Vec3(pts[k + 1].X, pts[k + 1].Y, 0) + off;
            var dir = b - a;
            if (dir.Length < 1e-6) continue;
            var fr = new Frame3(a, dir.Normalized(), Vec3.UnitZ, dir.Normalized().Cross(Vec3.UnitZ));
            sc.Face(fr, Polygon2.Rectangle(Vec2.Zero, dir.Length, Math.Max(o.TopZ, U.M(1))), "#D9D4CC", 0.25);
        }
        var totalLen = r.Fins.Sum(f => f.Length);
        var info = Loc.F("{0} lâminas · {1} lineares", "{0} fins · {1} linear", r.Fins.Count, U.FormatM(totalLen));
        if (r.Fins.Count > 0)
            info += Loc.F(" · rotação de {0}° a {1}°", " · rotation {0}° to {1}°",
                Math.Round(r.Fins.Min(f => Math.Min(f.AngleBottom, f.AngleTop))), Math.Round(r.Fins.Max(f => Math.Max(f.AngleBottom, f.AngleTop))));
        if (r.BandLength > 0) info += Loc.F(" · faixas {0}", " · bands {0}", U.FormatM(r.BandLength));
        if (r.Warnings.Count > 0) info += "\n⚠ " + string.Join("\n⚠ ", r.Warnings.Take(3));
        pm.Info = info;
        return pm;
    }

    internal static void Generate(ToolContext ctx, OrganicFinOptions o, FinContext c, string systemId)
    {
        var doc = ctx.Doc;
        var r = Compute(ctx, o, c);
        if (r.Fins.Count == 0 && r.Bands.Count == 0)
            ToolContext.Fail("Nenhuma lâmina cabe com essas opções (confira altura, recortes e margens).", "No fin fits with these options (check height, trims and margins).");
        ctx.CheckCount(r.Fins.Count + r.Bands.Count, "elementos", "elements");
        foreach (var w in r.Warnings) ctx.Report.Warn(w);
        var created = new List<ElementId>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var setNames = new[] { o.Material1, o.Material2, o.Material3 };
            var mats = new Dictionary<string, ElementId>();
            ElementId Mat(string? name)
            {
                name ??= "";
                if (!mats.TryGetValue(name, out var id)) mats[name] = id = Lookup.Material(doc, name)?.Id ?? ElementId.InvalidElementId;
                return id;
            }
            var offset = new Vec3(0, 0, c.BaseZ);
            var category = OrganicGeo.Category(o.Category);
            var failedFins = 0;
            string? firstError = null;
            foreach (var f in r.Fins)
            {
                var mat = Mat(setNames[Math.Clamp(f.Material - 1, 0, 2)]);
                var geo = OrganicGeo.Build(new[] { f.Mesh }, offset, _ => mat, out var failed, out var err);
                if (failed > 0 || geo.Count == 0)
                {
                    failedFins++;
                    firstError ??= err;
                    continue;
                }
                try
                {
                    var ds = Shapes.Create(doc, geo, category, Loc.F("TT Lâmina {0}", "TT Fin {0}", f.Index + 1));
                    GenSystem.Mark(ds, systemId, ToolId, f.Index, Loc.F("Lâmina {0} · conjunto {1}", "Fin {0} · set {1}", f.Index + 1, f.Material));
                    created.Add(ds.Id);
                }
                catch (Exception ex)
                {
                    failedFins++;
                    firstError ??= ex.Message;
                }
            }
            if (failedFins > 0) ctx.Report.ItemsFailed(Loc.T("Lâminas", "Fins"), failedFins, firstError ?? Loc.T("geometria inválida", "invalid geometry"));
            var bandMat = Mat(o.BandMaterial);
            var bi = 0;
            foreach (var b in r.Bands)
            {
                var geo = OrganicGeo.Build(new[] { b }, offset, _ => bandMat, out var failed, out var err);
                if (geo.Count == 0)
                {
                    ctx.Report.ItemsFailed(Loc.T("Faixas", "Bands"), 1, err ?? Loc.T("geometria inválida", "invalid geometry"));
                    continue;
                }
                try
                {
                    var ds = Shapes.Create(doc, geo, category, Loc.F("TT Faixa {0}", "TT Band {0}", ++bi));
                    GenSystem.Mark(ds, systemId, ToolId, 100000 + bi, Loc.T("Faixa", "Band"));
                    created.Add(ds.Id);
                }
                catch (Exception ex)
                {
                    ctx.ItemFailed("Faixas", "Bands", ex);
                }
            }
            ctx.RequireCreated(created.Count, "lâminas", "fins");
            GenSystem.Register(doc, systemId, ToolId, o, c, Loc.T("Lâminas de fachada", "Facade fins"));
            ctx.Report.Created(created.Count);
            foreach (var id in created) ctx.Report.AddResult(id.Value);
        });
        ctx.Report.Tables.Add(r.CutList());
        var sched = r.Schedule();
        if (sched.RowCount > 2000) ctx.Report.Info(Loc.F("A tabela de rotações mostra só as primeiras 2000 lâminas de {0}.", "The rotation table shows only the first 2000 of {0} fins.", sched.RowCount));
        while (sched.Rows.Count > 2000) sched.Rows.RemoveAt(sched.Rows.Count - 1);
        ctx.Report.Tables.Add(sched);
        if (r.BandLength > 0)
            ctx.Report.Info(Loc.F("Faixas horizontais: {0} trechos, {1} no total.", "Horizontal bands: {0} runs, {1} in total.", r.Bands.Count, U.FormatM(r.BandLength)));
        ctx.Report.ForceShow = true;
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = new OrganicFinOptions();
        Json.PopulateObject(o, optionsJson);
        var c = Json.Deserialize<FinContext>(contextJson) ?? throw new ToolException(Loc.T("Registro das lâminas inválido.", "Invalid fin record."));
        o = AskWithDrawings(ctx, o, c, Loc.T("Regenerar", "Regenerate"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover lâminas anteriores", "Remove previous fins"), () => GenSystem.Delete(ctx.Doc, systemId));
            Generate(ctx, o, c, systemId);
        });
    }
}
