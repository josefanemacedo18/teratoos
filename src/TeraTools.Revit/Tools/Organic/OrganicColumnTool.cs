using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Organic;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Organic;

public enum ColumnHeightMode
{
    [Text("Altura fixa", "Fixed height")] Fixed,
    [Text("Até o nível acima", "Up to the level above")] LevelAbove,
}

/// <summary>Opções da coluna orgânica (o diálogo trabalha com diâmetros; o núcleo com raios).</summary>
public sealed class OrganicColumnOptions
{
    private const string GPos = "Altura e posição|Height and position";
    private const string GSil = "Silhueta (como o diâmetro varia na altura)|Silhouette (how the diameter varies along the height)";
    private const string GSec = "Seção (corte horizontal)|Section (horizontal cut)";
    private const string GAxis = "Eixo|Spine";
    private const string GMod = "Torção, caneluras e ondas|Twist, flutes and waves";
    private const string GBuild = "Construção (cheia, oca ou vazada)|Construction (solid, hollow or open)";
    private const string GEnds = "Base e capitel|Base and capital";
    private const string GMat = "Materiais e saída|Materials and output";

    [Opt("Altura", "Height", Group = GPos)]
    public ColumnHeightMode HeightMode { get; set; } = ColumnHeightMode.Fixed;

    [Opt("Altura da coluna", "Column height", Group = GPos, Kind = OptKind.Length, Min = 0.3, VisibleIf = "HeightMode=Fixed")]
    public double Height { get; set; } = U.M(3);

    [Opt("Ajuste no topo", "Top adjustment", Group = GPos, Kind = OptKind.Length, VisibleIf = "HeightMode=LevelAbove",
        Tip = "Negativo para parar abaixo do nível (ex.: −150 mm para a laje).|Negative to stop below the level (e.g. −150 mm for the slab).")]
    public double TopOffset { get; set; }

    [Opt("Rotação", "Rotation", Group = GPos, Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Silhueta", "Silhouette", Group = GSil)]
    public ColumnSilhouette Silhouette { get; set; } = ColumnSilhouette.Cylinder;

    [Opt("Diâmetro na base", "Base diameter", Group = GSil, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Silhouette!=Formula&Silhouette!=Drawn")]
    public double BaseDiameter { get; set; } = U.Mm(400);

    [Opt("Diâmetro no topo", "Top diameter", Group = GSil, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Silhouette=Cone,Trumpet,Belly,Hourglass,Mushroom")]
    public double TopDiameter { get; set; } = U.Mm(400);

    [Opt("Diâmetro no meio", "Middle diameter", Group = GSil, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Silhouette=Belly,Hourglass")]
    public double MidDiameter { get; set; } = U.Mm(520);

    [Opt("Curvatura da abertura", "Flare curvature", Group = GSil, Kind = OptKind.Number, Min = 1, Max = 12, VisibleIf = "Silhouette=Trumpet",
        Tip = "1 = cone; valores maiores abrem só no alto, como um trompete.|1 = cone; larger values flare only near the top, like a trumpet.")]
    public double FlarePower { get; set; } = 3;

    [Opt("Onde o cogumelo começa a abrir", "Where the mushroom starts opening", Group = GSil, Kind = OptKind.Percent, Min = 5, Max = 95, VisibleIf = "Silhouette=Mushroom")]
    public double MushroomStart { get; set; } = 70;

    [Opt("Fórmula do diâmetro (mm)", "Diameter formula (mm)", Group = GSil, Kind = OptKind.Formula, VisibleIf = "Silhouette=Formula",
        Tip = "Variáveis: t (0 na base, 1 no topo), z (altura em mm), h (altura total em mm). Ângulos em graus. Ex.: 400 + 160*sin(180*t)|Variables: t (0 at base, 1 at top), z (height in mm), h (total height in mm). Degrees. E.g. 400 + 160*sin(180*t)")]
    public string DiameterFormula { get; set; } = "400 + 160*sin(180*t)";

    [Opt("Seção", "Section", Group = GSec)]
    public ColumnSection Section { get; set; } = ColumnSection.Circle;

    [Opt("Lados / pontas", "Sides / points", Group = GSec, Kind = OptKind.Integer, Min = 3, Max = 24, VisibleIf = "Section=Polygon,Star")]
    public int Sides { get; set; } = 6;

    [Opt("Lóbulos", "Lobes", Group = GSec, Kind = OptKind.Integer, Min = 2, Max = 24, VisibleIf = "Section=Lobed")]
    public int Lobes { get; set; } = 5;

    [Opt("Profundidade dos lóbulos", "Lobe depth", Group = GSec, Kind = OptKind.Percent, Min = 0, Max = 90, VisibleIf = "Section=Lobed")]
    public double LobeDepth { get; set; } = 25;

    [Opt("Raio interno da estrela", "Star inner radius", Group = GSec, Kind = OptKind.Percent, Min = 15, Max = 95, VisibleIf = "Section=Star")]
    public double StarInner { get; set; } = 55;

    [Opt("Proporção da elipse", "Ellipse ratio", Group = GSec, Kind = OptKind.Percent, Min = 10, Max = 100, VisibleIf = "Section=Ellipse")]
    public double EllipseRatio { get; set; } = 60;

    [Opt("Eixo", "Spine", Group = GAxis)]
    public ColumnSpine Spine { get; set; } = ColumnSpine.Straight;

    [Opt("Inclinação", "Lean", Group = GAxis, Kind = OptKind.Angle, Min = -60, Max = 60, VisibleIf = "Spine=Inclined")]
    public double LeanDeg { get; set; } = 10;

    [Opt("Direção do deslocamento", "Offset direction", Group = GAxis, Kind = OptKind.Angle, VisibleIf = "Spine=Inclined,Bow,SCurve")]
    public double LeanDirection { get; set; }

    [Opt("Deslocamento lateral", "Lateral offset", Group = GAxis, Kind = OptKind.Length, VisibleIf = "Spine=Bow,SCurve")]
    public double BendOffset { get; set; } = U.Mm(300);

    [Opt("Torção total (base → topo)", "Total twist (base → top)", Group = GMod, Kind = OptKind.Angle, Min = -1440, Max = 1440)]
    public double TwistDeg { get; set; }

    [Opt("Caneluras", "Flutes", Group = GMod, Kind = OptKind.Integer, Min = 0, Max = 64)]
    public int Flutes { get; set; }

    [Opt("Profundidade das caneluras", "Flute depth", Group = GMod, Kind = OptKind.Percent, Min = 0, Max = 50, VisibleIf = "Flutes!=0")]
    public double FluteDepth { get; set; } = 8;

    [Opt("Amplitude da ondulação", "Ripple amplitude", Group = GMod, Kind = OptKind.Percent, Min = -60, Max = 60,
        Tip = "Ondas na superfície, em % do raio. 0 = sem ondas.|Surface ripples, in % of the radius. 0 = none.")]
    public double WaveAmplitude { get; set; }

    [Opt("Ondas ao longo da altura", "Waves along the height", Group = GMod, Kind = OptKind.Number, Min = 0, Max = 40, VisibleIf = "WaveAmplitude!=0")]
    public double WaveCount { get; set; } = 3;

    [Opt("Espiral das ondas (voltas em planta)", "Wave spiral (lobes around)", Group = GMod, Kind = OptKind.Integer, Min = -24, Max = 24, VisibleIf = "WaveAmplitude!=0")]
    public int WaveLobes { get; set; }

    [Opt("Construção", "Construction", Group = GBuild)]
    public ColumnBuild Build { get; set; } = ColumnBuild.Solid;

    [Opt("Espessura da casca / pétala", "Shell / petal thickness", Group = GBuild, Kind = OptKind.Length, Min = 0.005, VisibleIf = "Build=Shell,Petals")]
    public double ShellThickness { get; set; } = U.Mm(40);

    [Opt("Quantidade (nervuras, hélices, lâminas ou pétalas)", "Count (ribs, helices, fins or petals)", Group = GBuild, Kind = OptKind.Integer, Min = 2, Max = 96, VisibleIf = "Build=Ribs,Grid,Lattice,Lamellas,Petals")]
    public int MemberCount { get; set; } = 12;

    [Opt("Espessura da barra / lâmina", "Bar / fin thickness", Group = GBuild, Kind = OptKind.Length, Min = 0.01, VisibleIf = "Build=Ribs,Grid,Lattice,Lamellas")]
    public double MemberSize { get; set; } = U.Mm(60);

    [Opt("Profundidade das lâminas", "Fin depth", Group = GBuild, Kind = OptKind.Length, Min = 0.01, VisibleIf = "Build=Lamellas",
        Tip = "Com núcleo, as lâminas vão do núcleo até a superfície e esta medida não é usada.|With a core, the fins run from the core to the surface and this value is not used.")]
    public double MemberDepth { get; set; } = U.Mm(120);

    [Opt("Anéis horizontais", "Horizontal rings", Group = GBuild, Kind = OptKind.Integer, Min = 2, Max = 60, VisibleIf = "Build=Ribs,Grid,Lattice")]
    public int RingCount { get; set; } = 2;

    [Opt("Voltas das hélices", "Helix turns", Group = GBuild, Kind = OptKind.Number, Min = 0.05, Max = 6, VisibleIf = "Build=Lattice")]
    public double LatticeTurns { get; set; } = 0.5;

    [Opt("Diâmetro do núcleo (0 = sem núcleo)", "Core diameter (0 = none)", Group = GBuild, Kind = OptKind.Length, Min = 0, VisibleIf = "Build=Ribs,Grid,Lattice,Lamellas")]
    public double CoreDiameter { get; set; }

    [Opt("Onde as pétalas começam", "Where the petals start", Group = GBuild, Kind = OptKind.Percent, Min = 5, Max = 95, VisibleIf = "Build=Petals")]
    public double PetalStart { get; set; } = 55;

    [Opt("Folga entre pétalas", "Gap between petals", Group = GBuild, Kind = OptKind.Percent, Min = 0, Max = 90, VisibleIf = "Build=Petals")]
    public double PetalGap { get; set; } = 12;

    [Opt("Afinamento das pétalas", "Petal taper", Group = GBuild, Kind = OptKind.Percent, Min = 0, Max = 95, VisibleIf = "Build=Petals")]
    public double PetalTaper { get; set; } = 50;

    [Opt("Base", "Base", Group = GEnds)]
    public ColumnEnd Base { get; set; } = ColumnEnd.None;

    [Opt("Diâmetro / lado da base", "Base diameter / side", Group = GEnds, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Base!=None")]
    public double BaseSize { get; set; } = U.Mm(600);

    [Opt("Espessura da base", "Base thickness", Group = GEnds, Kind = OptKind.Length, Min = 0.005, VisibleIf = "Base!=None")]
    public double BaseThickness { get; set; } = U.Mm(50);

    [Opt("Capitel", "Capital", Group = GEnds)]
    public ColumnEnd Capital { get; set; } = ColumnEnd.None;

    [Opt("Diâmetro / lado do capitel", "Capital diameter / side", Group = GEnds, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Capital!=None")]
    public double CapitalSize { get; set; } = U.Mm(900);

    [Opt("Espessura do capitel", "Capital thickness", Group = GEnds, Kind = OptKind.Length, Min = 0.005, VisibleIf = "Capital!=None")]
    public double CapitalThickness { get; set; } = U.Mm(60);

    [Opt("Material do fuste e do núcleo", "Shaft and core material", Group = GMat, Kind = OptKind.Material)]
    public string ShaftMaterial { get; set; } = "";

    [Opt("Material das nervuras, anéis, lâminas e pétalas", "Ribs, rings, fins and petals material", Group = GMat, Kind = OptKind.Material)]
    public string MemberMaterial { get; set; } = "";

    [Opt("Material da base e do capitel", "Base and capital material", Group = GMat, Kind = OptKind.Material)]
    public string EndMaterial { get; set; } = "";

    [Opt("Densidade para o peso (kg/m³)", "Density for the weight (kg/m³)", Group = GMat, Kind = OptKind.Number, Min = 1, Max = 20000,
        Tip = "Concreto 2500 · aço 7850 · madeira 600 · GRC 2000 · alumínio 2700.|Concrete 2500 · steel 7850 · timber 600 · GRC 2000 · aluminium 2700.")]
    public double Density { get; set; } = 2500;

    [Opt("Categoria no Revit", "Revit category", Group = GMat)]
    public OrganicCategory Category { get; set; } = OrganicCategory.Columns;

    [Opt("Resolução (pontos em volta)", "Resolution (points around)", Group = GMat, Kind = OptKind.Integer, Min = 12, Max = 128,
        Tip = "Mais pontos = superfície mais lisa e arquivo mais pesado.|More points = smoother surface and heavier file.")]
    public int Resolution { get; set; } = 48;

    [Opt("Escolher de novo as linhas desenhadas", "Pick the drawn lines again", Group = GMat, VisibleIf = "Section=Drawn|Silhouette=Drawn|Spine=Drawn")]
    public bool RepickDrawn { get; set; }

    /// <summary>Converte para o núcleo (diâmetros → raios, % → frações).</summary>
    public ColumnSettings ToCore(double height, ColumnContext? c = null)
    {
        var s = new ColumnSettings
        {
            Height = height,
            Silhouette = Silhouette,
            BaseRadius = BaseDiameter / 2,
            TopRadius = Silhouette == ColumnSilhouette.Cylinder ? BaseDiameter / 2 : TopDiameter / 2,
            MidRadius = MidDiameter / 2,
            FlarePower = FlarePower,
            MushroomStart = MushroomStart / 100,
            RadiusFormula = "(" + DiameterFormula + ")/2",
            Section = Section,
            Sides = Sides,
            Lobes = Lobes,
            LobeDepth = LobeDepth / 100,
            StarInner = StarInner / 100,
            EllipseRatio = EllipseRatio / 100,
            SectionRotationDeg = Rotation,
            Spine = Spine,
            LeanDeg = LeanDeg,
            LeanDirectionDeg = LeanDirection,
            BendOffset = BendOffset,
            TwistDeg = TwistDeg,
            Flutes = Flutes,
            FluteDepth = FluteDepth / 100,
            WaveAmplitude = WaveAmplitude / 100,
            WaveCount = WaveCount,
            WaveLobes = WaveLobes,
            Build = Build,
            ShellThickness = ShellThickness,
            MemberCount = MemberCount,
            MemberSize = MemberSize,
            MemberDepth = MemberDepth,
            RingCount = RingCount,
            LatticeTurns = LatticeTurns,
            CoreRadius = CoreDiameter / 2,
            PetalStart = PetalStart / 100,
            PetalGap = PetalGap / 100,
            PetalTaper = PetalTaper / 100,
            Base = Base,
            BaseSize = BaseSize,
            BaseThickness = BaseThickness,
            Capital = Capital,
            CapitalSize = CapitalSize,
            CapitalThickness = CapitalThickness,
            Resolution = Resolution,
        };
        if (c != null)
        {
            s.DrawnSection = OrganicGeo.Unpack2(c.DrawnSection);
            s.DrawnSilhouette = OrganicGeo.Unpack2(c.DrawnSilhouette);
            var spine = OrganicGeo.Unpack3(c.DrawnSpine);
            if (spine.Count >= 2) s.DrawnSpine = spine.Select(p => p - spine[0]).ToList();
        }
        return s;
    }

    /// <summary>Aplica um estilo pronto (forma), mantendo altura, materiais e saída escolhidos antes.</summary>
    public void ApplyStyle(ColumnSettings s)
    {
        Silhouette = s.Silhouette;
        BaseDiameter = s.BaseRadius * 2;
        TopDiameter = s.TopRadius * 2;
        MidDiameter = s.MidRadius * 2;
        FlarePower = s.FlarePower;
        MushroomStart = s.MushroomStart * 100;
        Section = s.Section;
        Sides = s.Sides;
        Lobes = s.Lobes;
        LobeDepth = s.LobeDepth * 100;
        StarInner = s.StarInner * 100;
        EllipseRatio = s.EllipseRatio * 100;
        Spine = s.Spine;
        LeanDeg = s.LeanDeg;
        BendOffset = s.BendOffset;
        TwistDeg = s.TwistDeg;
        Flutes = s.Flutes;
        FluteDepth = s.FluteDepth * 100;
        WaveAmplitude = s.WaveAmplitude * 100;
        WaveCount = s.WaveCount;
        WaveLobes = s.WaveLobes;
        Build = s.Build;
        ShellThickness = s.ShellThickness;
        MemberCount = s.MemberCount;
        MemberSize = s.MemberSize;
        MemberDepth = s.MemberDepth;
        RingCount = s.RingCount;
        LatticeTurns = s.LatticeTurns;
        CoreDiameter = s.CoreRadius * 2;
        PetalStart = s.PetalStart * 100;
        PetalGap = s.PetalGap * 100;
        PetalTaper = s.PetalTaper * 100;
        Base = s.Base;
        BaseSize = s.BaseSize;
        BaseThickness = s.BaseThickness;
        Capital = s.Capital;
        CapitalSize = s.CapitalSize;
        CapitalThickness = s.CapitalThickness;
        Resolution = s.Resolution;
    }
}

/// <summary>O que não cabe nas opções: pontos de inserção e linhas desenhadas (coordenadas do projeto).</summary>
public sealed class ColumnContext
{
    public List<double[]> Points { get; set; } = new();
    public List<double[]>? DrawnSection { get; set; }
    public List<double[]>? DrawnSilhouette { get; set; }
    public List<double[]>? DrawnSpine { get; set; }
    public string Style { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("org.column", Modules.Organic, "Colunas orgânicas", "Organic columns", Order = 10, Large = true,
    DescPt = "Cria colunas e pilares de formas orgânicas — cheios, ocos, vazados, torcidos, ondulados, em leque, treliça, lâminas ou pétalas — editáveis por parâmetros ou por linhas desenhadas.",
    DescEn = "Creates organic columns — solid, hollow, open, twisted, rippled, fanned, lattice, fins or petals — editable by parameters or drawn lines.",
    HelpPt = "1. Escolha um estilo pronto (ou suas últimas opções).\n2. Ajuste silhueta, seção, eixo, torção, caneluras, ondas, construção, base e capitel; a pré-visualização 3D mostra o resultado.\n3. Para formas livres, escolha \"Desenhado\" na seção (contorno fechado em planta), na silhueta (meio perfil em elevação + um clique no eixo) ou no eixo (linha 3D).\n4. Clique nos pontos de inserção (Esc para terminar). Com eixo desenhado, a coluna segue a linha.\n\nCada coluna vira uma forma livre com material por peça. O relatório traz volume, área, comprimento de barras, peso e uma esbeltez indicativa.\nPara alterar depois: Editar sistema gerado.\n\nLimitação: a verificação de esbeltez é só um alerta geométrico; o dimensionamento é do engenheiro.",
    HelpEn = "Pick a style, adjust the parameters with live 3D preview, optionally use drawn lines, then click insertion points. Each column becomes a free-form element with per-part materials, volumes, weights and an indicative slenderness check.",
    Keywords = "coluna pilar organico organica parametrico torcido ondulado leque arvore trelica lamela petala tulipa cogumelo canelado column organic")]
public sealed class OrganicColumnCommand : ToolCommand
{
    private const string ToolId = "org.column";

    public override void Run(ToolContext ctx)
    {
        var last = new ColumnStyle("", Loc.T("Minhas últimas opções", "My last options"), Loc.T("Abre o diálogo com as opções da última vez.", "Opens the dialog with your last options."), () => new ColumnSettings());
        var style = Pick.One(new[] { last }.Concat(ColumnStyles.All), s => s.Name, ctx.Tool.Name,
            Loc.T("Escolha um ponto de partida. Tudo pode ser ajustado no próximo passo.", "Pick a starting point. Everything can be adjusted next."),
            ctx.Owner, s => s.Description);
        if (style == null) ctx.Cancel();
        var o = ctx.LoadOptions<OrganicColumnOptions>();
        if (style!.Id != "") o.ApplyStyle(style.Create());
        var c = new ColumnContext { Style = style.Id };
        o = AskWithDrawings(ctx, o, c, Loc.T("Gerar", "Generate"));

        if (o.Spine != ColumnSpine.Drawn)
        {
            // pontos de inserção: vários cliques, Esc para terminar
            while (true)
            {
                XYZ p;
                try
                {
                    p = ctx.PickPoint(c.Points.Count == 0 ? "Clique onde fica a coluna (Esc para terminar)" : "Próxima coluna (Esc para terminar)",
                        c.Points.Count == 0 ? "Click where the column goes (Esc to finish)" : "Next column (Esc to finish)");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
                c.Points.Add(new[] { p.X, p.Y, p.Z });
                if (c.Points.Count >= 200) break;
            }
            if (c.Points.Count == 0) ctx.Cancel();
        }
        Generate(ctx, o, c, GenSystem.NewId());
    }

    /// <summary>Mostra o diálogo; se pedir algo desenhado que ainda não existe, pede as linhas e mostra de novo para conferir.</summary>
    private static OrganicColumnOptions AskWithDrawings(ToolContext ctx, OrganicColumnOptions o, ColumnContext c, string okText)
    {
        for (var round = 0; round < 6; round++)
        {
            o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: okText, key: ToolId);
            var picked = false;
            if (o.Section == ColumnSection.Drawn && (o.RepickDrawn || c.DrawnSection is not { Count: >= 3 }))
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione as linhas do contorno da seção (fechado, em planta)", "Select the section outline lines (closed, in plan)");
                c.DrawnSection = OrganicGeo.Pack(OrganicGeo.ReadClosedOutline(curves));
                picked = true;
            }
            if (o.Silhouette == ColumnSilhouette.Drawn && (o.RepickDrawn || c.DrawnSilhouette is not { Count: >= 2 }))
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione as linhas do meio perfil (em elevação ou corte)", "Select the half-profile lines (in elevation or section)");
                var axis = ctx.PickPoint("Clique um ponto do eixo da coluna (na mesma vista)", "Click a point on the column axis (same view)");
                c.DrawnSilhouette = OrganicGeo.Pack(OrganicGeo.ReadProfile(curves, axis));
                picked = true;
            }
            if (o.Spine == ColumnSpine.Drawn && (o.RepickDrawn || c.DrawnSpine is not { Count: >= 2 }))
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione as linhas do eixo (de baixo para cima)", "Select the spine lines (bottom to top)");
                c.DrawnSpine = OrganicGeo.Pack(OrganicGeo.ReadOpenPath(curves));
                picked = true;
            }
            o.RepickDrawn = false;
            if (!picked) return o;
            // mostra de novo, agora com as linhas desenhadas na pré-visualização
        }
        return o;
    }

    /// <summary>Altura da coluna no ponto (fixa ou até o nível acima).</summary>
    private static double HeightAt(Document doc, OrganicColumnOptions o, ColumnContext c, Vec3 p, List<string>? warnings)
    {
        if (o.Spine == ColumnSpine.Drawn) return o.Height;
        if (o.HeightMode == ColumnHeightMode.LevelAbove)
        {
            var above = OrganicGeo.LevelAbove(doc, p.Z);
            if (above != null) return Math.Max(U.Mm(300), above.Value - p.Z + o.TopOffset);
            warnings?.Add(Loc.T("Não há nível acima de um dos pontos: usei a altura fixa.", "No level above one of the points: the fixed height was used."));
        }
        return o.Height;
    }

    private static PreviewModel Preview(ToolContext ctx, OrganicColumnOptions o, ColumnContext c)
    {
        var at = c.Points.Count > 0 ? new Vec3(c.Points[0][0], c.Points[0][1], c.Points[0][2]) : Vec3.Zero;
        var h = HeightAt(ctx.Doc, o, c, at, null);
        var r = ColumnGenerator.Generate(o.ToCore(h, c));
        var pm = new PreviewModel();
        // planta: projeção de tudo (tracejado) e a pegada de cada peça no pé da coluna
        var all = r.Meshes.SelectMany(m => m.Vertices.Select(v => v.XY)).ToList();
        if (all.Count >= 3) pm.Outline(Hull(all), "#9AA3AE", 1, dashed: true);
        foreach (var m in r.Meshes.Where(x => x.Role is ColumnRole.Shaft or ColumnRole.Member or ColumnRole.Core or ColumnRole.Base).Take(200))
        {
            var zmin = m.Bounds().Min.Z;
            var pts = m.Vertices.Where(v => v.Z <= zmin + U.Mm(1)).Select(v => v.XY).ToList();
            if (pts.Count >= 3) pm.Outline(Hull(pts), OrganicGeo.Color(m.Role), 1.2);
        }
        OrganicGeo.AddToScene(pm.S3, r.Meshes, Vec3.Zero);
        var q = r.Quantities;
        var info = Loc.F("Altura {0} · volume {1} · peso ≈ {2} kg · área {3}",
            "Height {0} · volume {1} · weight ≈ {2} kg · area {3}",
            U.FormatM(q.Height), U.FormatVolume(q.Volume), Math.Round(q.Weight(o.Density)), U.FormatArea(q.SurfaceArea));
        if (q.MemberLength > 0) info += Loc.F(" · barras {0}", " · bars {0}", U.FormatM(q.MemberLength));
        if (q.Slenderness > 0) info += Loc.F(" · esbeltez λ ≈ {0}", " · slenderness λ ≈ {0}", Math.Round(q.Slenderness));
        info += Loc.F(" · {0} triângulos", " · {0} triangles", r.TriangleCount);
        if (r.Warnings.Count > 0) info += "\n⚠ " + string.Join("\n⚠ ", r.Warnings.Take(3));
        pm.Info = info;
        return pm;
    }

    /// <summary>Envoltória convexa (para desenhar a pegada em planta).</summary>
    private static Polygon2 Hull(List<Vec2> pts)
    {
        var p = pts.Distinct().OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        if (p.Count < 3) return new Polygon2(p);
        var lower = new List<Vec2>();
        foreach (var v in p)
        {
            while (lower.Count >= 2 && (lower[^1] - lower[^2]).Cross(v - lower[^2]) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(v);
        }
        var upper = new List<Vec2>();
        for (var i = p.Count - 1; i >= 0; i--)
        {
            var v = p[i];
            while (upper.Count >= 2 && (upper[^1] - upper[^2]).Cross(v - upper[^2]) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(v);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        return new Polygon2(lower.Concat(upper));
    }

    internal static void Generate(ToolContext ctx, OrganicColumnOptions o, ColumnContext c, string systemId)
    {
        var doc = ctx.Doc;
        var spine = OrganicGeo.Unpack3(c.DrawnSpine);
        var places = o.Spine == ColumnSpine.Drawn
            ? (spine.Count >= 2 ? new List<Vec3> { spine[0] } : new List<Vec3>())
            : c.Points.Select(p => new Vec3(p[0], p[1], p[2])).ToList();
        if (places.Count == 0)
            ToolContext.Fail("Nenhum ponto de inserção (ou eixo desenhado) foi informado.", "No insertion point (or drawn spine) was given.");

        var warnings = new List<string>();
        var results = places.Select(p => (Place: p, Result: ColumnGenerator.Generate(o.ToCore(HeightAt(doc, o, c, p, warnings), c)))).ToList();
        foreach (var w in results.SelectMany(r => r.Result.Warnings).Concat(warnings).Distinct()) ctx.Report.Warn(w);
        var triangles = results.Sum(r => r.Result.TriangleCount);
        if (triangles > 1_500_000)
            ToolContext.Fail("A malha ficaria pesada demais para o Revit. Reduza a resolução, a quantidade de barras ou de colunas.",
                "The mesh would be too heavy for Revit. Reduce the resolution, the number of bars or of columns.");

        var created = new List<ElementId>();
        var table = new TableData(Loc.T("Colunas orgânicas", "Organic columns"));
        table.AddColumn(Loc.T("Coluna", "Column"))
            .AddColumn(Loc.T("Altura (m)", "Height (m)"), ColumnKind.Number, 2)
            .AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3)
            .AddColumn(Loc.T("Área de superfície (m²)", "Surface area (m²)"), ColumnKind.Number, 2)
            .AddColumn(Loc.T("Barras (m)", "Bars (m)"), ColumnKind.Number, 2)
            .AddColumn(Loc.T("Peso (kg)", "Weight (kg)"), ColumnKind.Number, 0)
            .AddColumn(Loc.T("Esbeltez λ", "Slenderness λ"), ColumnKind.Number, 0);
        var parts = new TableData(Loc.T("Volume por peça", "Volume by part"));
        parts.AddColumn(Loc.T("Peça", "Part")).AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3);
        var byRole = new Dictionary<string, double>();
        ElementId? viewId = null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var mats = new Dictionary<string, ElementId>();
            ElementId Mat(string role)
            {
                var name = role switch
                {
                    ColumnRole.Shaft or ColumnRole.Core => o.ShaftMaterial,
                    ColumnRole.Base or ColumnRole.Capital => o.EndMaterial,
                    _ => o.MemberMaterial,
                };
                if (!mats.TryGetValue(name, out var id)) mats[name] = id = Lookup.Material(doc, name)?.Id ?? ElementId.InvalidElementId;
                return id;
            }
            var idx = 0;
            foreach (var (place, r) in results)
            {
                idx++;
                var label = Loc.F("Coluna {0}", "Column {0}", idx);
                var geo = OrganicGeo.Build(r.Meshes, place, Mat, out var failed, out var err);
                if (failed > 0) ctx.Report.ItemsFailed(Loc.F("Peças da {0}", "Parts of {0}", label.ToLowerInvariant()), failed, err ?? Loc.T("geometria inválida", "invalid geometry"));
                if (geo.Count == 0) continue;
                try
                {
                    var ds = Shapes.Create(doc, geo, OrganicGeo.Category(o.Category), "TT Coluna orgânica");
                    GenSystem.Mark(ds, systemId, ToolId, idx, label);
                    created.Add(ds.Id);
                    var q = r.Quantities;
                    table.AddRowWithIds(new[] { ds.Id.Value }, label, U.ToM(q.Height), U.CuFtToM3(q.Volume), U.SqFtToM2(q.SurfaceArea),
                        U.ToM(q.MemberLength), q.Weight(o.Density), q.Slenderness > 0 ? Math.Round(q.Slenderness) : (double?)null);
                    foreach (var (role, v) in q.VolumeByRole) byRole[role] = byRole.GetValueOrDefault(role) + v;
                }
                catch (Exception ex)
                {
                    ctx.ItemFailed("Colunas", "Columns", ex);
                }
            }
            ctx.RequireCreated(created.Count, "colunas", "columns");
            GenSystem.Register(doc, systemId, ToolId, o, c, Loc.T("Colunas orgânicas", "Organic columns"));
            if (created.Count > 0 && ctx.ActiveView is not View3D)
            {
                var boxes = created.Select(id => doc.GetElement(id)?.get_BoundingBox(null)).Where(b => b != null).ToList();
                if (boxes.Count > 0)
                {
                    var min = new XYZ(boxes.Min(b => b!.Min.X), boxes.Min(b => b!.Min.Y), boxes.Min(b => b!.Min.Z));
                    var max = new XYZ(boxes.Max(b => b!.Max.X), boxes.Max(b => b!.Max.Y), boxes.Max(b => b!.Max.Z));
                    viewId = OrganicGeo.View3DFor(ctx, Loc.F("TT Colunas orgânicas {0}", "TT Organic columns {0}", systemId), min, max);
                }
            }
            ctx.Report.Created(created.Count);
            foreach (var id in created) ctx.Report.AddResult(id.Value);
        });
        table.AddTotalsRow(Loc.T("Total", "Total"), 2, 3, 4, 5);
        foreach (var (role, v) in byRole) parts.AddRow(ColumnRole.Label(role), U.CuFtToM3(v));
        parts.AddTotalsRow(Loc.T("Total", "Total"), 1);
        ctx.Report.Tables.Add(table);
        ctx.Report.Tables.Add(parts);
        ctx.Report.Info(Loc.T("A esbeltez λ é só um alerta geométrico (coluna biapoiada); o dimensionamento é do engenheiro.",
            "Slenderness λ is only a geometric alert (pinned column); structural design is the engineer's job."));
        if (viewId != null && doc.GetElement(viewId) is View3D view)
        {
            try
            {
                ctx.UiDoc.ActiveView = view;
            }
            catch (Exception ex)
            {
                Log.Warn("Abrir vista 3D das colunas: " + ex.Message);
            }
        }
        ctx.Report.ForceShow = true;
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = new OrganicColumnOptions();
        Json.PopulateObject(o, optionsJson);
        var c = Json.Deserialize<ColumnContext>(contextJson) ?? throw new ToolException(Loc.T("Registro das colunas inválido.", "Invalid column record."));
        o = AskWithDrawings(ctx, o, c, Loc.T("Regenerar", "Regenerate"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover colunas anteriores", "Remove previous columns"), () => GenSystem.Delete(ctx.Doc, systemId));
            Generate(ctx, o, c, systemId);
        });
    }
}
