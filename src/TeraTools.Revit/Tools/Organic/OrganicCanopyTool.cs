using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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

public enum CanopySupportSource
{
    [Text("Sem apoios próprios", "No own supports")] None,
    [Text("Grade automática dentro da planta", "Automatic grid inside the outline")] Grid,
    [Text("Pontos clicados", "Clicked points")] Picked,
}

public sealed class OrganicCanopyOptions
{
    private const string GSurf = "Superfície|Surface";
    private const string GSkin = "Pele|Skin";
    private const string GStruct = "Estrutura|Structure";
    private const string GSup = "Apoios próprios|Own supports";
    private const string GCol = "Ligação a colunas existentes|Connection to existing columns";
    private const string GOut = "Materiais, verificações e saída|Materials, checks and output";

    [Opt("Forma da superfície", "Surface shape", Group = GSurf)]
    public CanopySurface Surface { get; set; } = CanopySurface.Pillow;

    [Opt("Altura do beiral (ponto mais baixo)", "Eave height (lowest point)", Group = GSurf, Kind = OptKind.Length, Min = 0)]
    public double Height { get; set; } = U.M(3.5);

    [Opt("Quanto a superfície sobe", "How much the surface rises", Group = GSurf, Kind = OptKind.Length, Min = 0, VisibleIf = "Surface!=Flat&Surface!=Formula")]
    public double Rise { get; set; } = U.M(1.2);

    [Opt("Direção (eixo da abóbada, das ondas, da sela ou do caimento)", "Direction (vault, waves, saddle or slope axis)", Group = GSurf, Kind = OptKind.Angle, VisibleIf = "Surface=Flat,Vault,Saddle,Waves,Formula")]
    public double Direction { get; set; }

    [Opt("Caimento (%)", "Slope (%)", Group = GSurf, Kind = OptKind.Number, Min = -100, Max = 100, VisibleIf = "Surface=Flat")]
    public double SlopePercent { get; set; } = 3;

    [Opt("Ondas na direção principal", "Waves along the main direction", Group = GSurf, Kind = OptKind.Number, Min = 0.25, Max = 20, VisibleIf = "Surface=Waves")]
    public double WavesU { get; set; } = 2;

    [Opt("Ondas na direção transversal (0 = só uma direção)", "Waves across (0 = one direction only)", Group = GSurf, Kind = OptKind.Number, Min = 0, Max = 20, VisibleIf = "Surface=Waves")]
    public double WavesV { get; set; }

    [Opt("Fase das ondas", "Wave phase", Group = GSurf, Kind = OptKind.Angle, VisibleIf = "Surface=Waves")]
    public double WavePhase { get; set; }

    [Opt("Invertido (funil: baixo no pilar, recolhe a água)", "Inverted (funnel: low at the column, collects water)", Group = GSurf, VisibleIf = "Surface=Umbrellas")]
    public bool UmbrellaInverted { get; set; } = true;

    [Opt("Curvatura do perfil", "Profile curvature", Group = GSurf, Kind = OptKind.Number, Min = 0.3, Max = 6, VisibleIf = "Surface=Umbrellas,Membrane",
        Tip = "1 = superfícies retas (como paraboloides); maior = mais curvas e picos mais agudos.|1 = straight surfaces; larger = more curved, sharper peaks.")]
    public double Sharpness { get; set; } = 1.6;

    [Opt("Raio de influência", "Influence radius", Group = GSurf, Kind = OptKind.Length, Min = 0.3, VisibleIf = "Surface=Membrane,Attractors")]
    public double InfluenceRadius { get; set; } = U.M(4);

    [Opt("Atratores afundam (em vez de levantar)", "Attractors sink (instead of lifting)", Group = GSurf, VisibleIf = "Surface=Attractors")]
    public bool AttractorsDown { get; set; }

    [Opt("Clicar pontos atratores (senão, usa os apoios)", "Click attractor points (otherwise the supports are used)", Group = GSurf, VisibleIf = "Surface=Attractors")]
    public bool PickAttractors { get; set; }

    [Opt("Fórmula da altura (mm acima do beiral)", "Height formula (mm above the eave)", Group = GSurf, Kind = OptKind.Formula, VisibleIf = "Surface=Formula",
        Tip = "Variáveis: u e v (0 a 1 ao longo e através da direção), x e y (mm a partir do centro), r (mm até o centro), d (mm até a borda). Ângulos em graus.|Variables: u and v (0 to 1), x and y (mm from the center), r (mm to the center), d (mm to the edge). Degrees.")]
    public string Formula { get; set; } = "800*sin(180*u)*sin(180*v)";

    [Opt("Pele", "Skin", Group = GSkin)]
    public CanopySkin Skin { get; set; } = CanopySkin.Continuous;

    [Opt("Espessura da pele / painel", "Skin / panel thickness", Group = GSkin, Kind = OptKind.Length, Min = 0.005, VisibleIf = "Skin=Continuous,Panels")]
    public double SkinThickness { get; set; } = U.Mm(60);

    [Opt("Painel: comprimento", "Panel: length", Group = GSkin, Kind = OptKind.Length, Min = 0.1, VisibleIf = "Skin=Panels")]
    public double PanelSizeU { get; set; } = U.Mm(1500);

    [Opt("Painel: largura", "Panel: width", Group = GSkin, Kind = OptKind.Length, Min = 0.1, VisibleIf = "Skin=Panels")]
    public double PanelSizeV { get; set; } = U.Mm(1500);

    [Opt("Junta entre painéis", "Joint between panels", Group = GSkin, Kind = OptKind.Length, Min = 0, VisibleIf = "Skin=Panels")]
    public double PanelJoint { get; set; } = U.Mm(20);

    [Opt("Rotação dos painéis", "Panel rotation", Group = GSkin, Kind = OptKind.Angle, VisibleIf = "Skin=Panels")]
    public double PanelRotation { get; set; }

    [Opt("Espaçamento das lâminas", "Slat spacing", Group = GSkin, Kind = OptKind.Length, Min = 0.03, VisibleIf = "Skin=Slats")]
    public double SlatSpacing { get; set; } = U.Mm(300);

    [Opt("Altura das lâminas", "Slat depth", Group = GSkin, Kind = OptKind.Length, Min = 0.01, VisibleIf = "Skin=Slats")]
    public double SlatDepth { get; set; } = U.Mm(150);

    [Opt("Espessura das lâminas", "Slat thickness", Group = GSkin, Kind = OptKind.Length, Min = 0.005, VisibleIf = "Skin=Slats")]
    public double SlatThickness { get; set; } = U.Mm(40);

    [Opt("Direção das lâminas", "Slat direction", Group = GSkin, Kind = OptKind.Angle, VisibleIf = "Skin=Slats")]
    public double SlatDirection { get; set; }

    [Opt("Tamanho da malha da superfície (0 = automático)", "Surface mesh size (0 = automatic)", Group = GSkin, Kind = OptKind.Length, Min = 0, VisibleIf = "Skin=Continuous,Panels",
        Tip = "Menor = curvas mais lisas e arquivo mais pesado.|Smaller = smoother curves and heavier file.")]
    public double MeshSize { get; set; }

    [Opt("Estrutura", "Structure", Group = GStruct)]
    public CanopyStructure Structure { get; set; } = CanopyStructure.Grid;

    [Opt("Espaçamento numa direção", "Spacing in one direction", Group = GStruct, Kind = OptKind.Length, Min = 0.1, VisibleIf = "Structure=Grid,Diagrid")]
    public double StructureSpacingU { get; set; } = U.M(1.5);

    [Opt("Espaçamento na outra direção", "Spacing in the other direction", Group = GStruct, Kind = OptKind.Length, Min = 0.1, VisibleIf = "Structure=Grid,Diagrid")]
    public double StructureSpacingV { get; set; } = U.M(1.5);

    [Opt("Rotação da estrutura", "Structure rotation", Group = GStruct, Kind = OptKind.Angle, VisibleIf = "Structure!=None")]
    public double StructureRotation { get; set; }

    [Opt("Nervuras por apoio", "Ribs per support", Group = GStruct, Kind = OptKind.Integer, Min = 3, Max = 72, VisibleIf = "Structure=Radial")]
    public int RadialCount { get; set; } = 12;

    [Opt("Anéis por apoio", "Rings per support", Group = GStruct, Kind = OptKind.Integer, Min = 0, Max = 30, VisibleIf = "Structure=Radial")]
    public int RadialRings { get; set; } = 3;

    [Opt("Viga de borda", "Edge beam", Group = GStruct)]
    public bool EdgeBeam { get; set; } = true;

    [Opt("Perfil das vigas", "Beam profile", Group = GStruct, VisibleIf = "Structure!=None|EdgeBeam")]
    public MemberProfile Profile { get; set; } = MemberProfile.Rectangular;

    [Opt("Largura da viga", "Beam width", Group = GStruct, Kind = OptKind.Length, Min = 0.01, VisibleIf = "Profile=Rectangular&Structure!=None|Profile=Rectangular&EdgeBeam")]
    public double MemberWidth { get; set; } = U.Mm(80);

    [Opt("Altura da viga / diâmetro do tubo", "Beam depth / tube diameter", Group = GStruct, Kind = OptKind.Length, Min = 0.01, VisibleIf = "Structure!=None|EdgeBeam")]
    public double MemberHeight { get; set; } = U.Mm(200);

    [Opt("Apoios", "Supports", Group = GSup)]
    public CanopySupportSource SupportSource { get; set; } = CanopySupportSource.Grid;

    [Opt("Apoios na direção principal", "Supports along the main direction", Group = GSup, Kind = OptKind.Integer, Min = 1, Max = 30, VisibleIf = "SupportSource=Grid")]
    public int SupportsU { get; set; } = 2;

    [Opt("Apoios na direção transversal", "Supports across", Group = GSup, Kind = OptKind.Integer, Min = 1, Max = 30, VisibleIf = "SupportSource=Grid")]
    public int SupportsV { get; set; } = 1;

    [Opt("Tipo de apoio", "Support type", Group = GSup, VisibleIf = "SupportSource!=None")]
    public CanopySupports SupportKind { get; set; } = CanopySupports.Tree;

    [Opt("Diâmetro do tronco / pilar", "Trunk / post diameter", Group = GSup, Kind = OptKind.Length, Min = 0.03, VisibleIf = "SupportSource!=None|ConnectColumns")]
    public double TrunkDiameter { get; set; } = U.Mm(300);

    [Opt("Altura da forquilha", "Fork height", Group = GSup, Kind = OptKind.Length, Min = 0.3, VisibleIf = "SupportKind=Tree&SupportSource!=None")]
    public double ForkHeight { get; set; } = U.M(2.4);

    [Opt("Galhos", "Branches", Group = GSup, Kind = OptKind.Integer, Min = 1, Max = 12, VisibleIf = "SupportKind=Tree&SupportSource!=None|ConnectColumns&Connection!=Top")]
    public int Branches { get; set; } = 4;

    [Opt("Níveis de galhos", "Branch levels", Group = GSup, Kind = OptKind.Integer, Min = 1, Max = 2, VisibleIf = "SupportKind=Tree&SupportSource!=None|ConnectColumns&Connection!=Top")]
    public int BranchLevels { get; set; } = 2;

    [Opt("Alcance dos galhos", "Branch reach", Group = GSup, Kind = OptKind.Length, Min = 0.2, VisibleIf = "SupportKind=Tree&SupportSource!=None|ConnectColumns&Connection!=Top")]
    public double BranchReach { get; set; } = U.M(2.2);

    [Opt("Abertura entre galhos secundários", "Spread of secondary branches", Group = GSup, Kind = OptKind.Angle, Min = 0, Max = 80, VisibleIf = "BranchLevels=2")]
    public double BranchSpread { get; set; } = 25;

    [Opt("Rotação dos galhos", "Branch rotation", Group = GSup, Kind = OptKind.Angle, VisibleIf = "SupportKind=Tree&SupportSource!=None|ConnectColumns&Connection!=Top")]
    public double BranchRotation { get; set; } = 45;

    [Opt("Ligar a colunas existentes", "Connect to existing columns", Group = GCol,
        Tip = "Depois de OK, selecione as colunas (inclusive as Colunas orgânicas). A cobertura não depende delas: a ligação é opcional.|After OK, select the columns (including Organic columns). The canopy does not depend on them: the connection is optional.")]
    public bool ConnectColumns { get; set; }

    [Opt("Onde ligar", "Where to connect", Group = GCol, VisibleIf = "ConnectColumns")]
    public ColumnConnection Connection { get; set; } = ColumnConnection.Top;

    [Opt("Altura do ponto intermediário (% da coluna)", "Intermediate point height (% of the column)", Group = GCol, Kind = OptKind.Percent, Min = 5, Max = 98, VisibleIf = "ConnectColumns&Connection!=Top")]
    public double IntermediateHeight { get; set; } = 60;

    [Opt("Material da pele", "Skin material", Group = GOut, Kind = OptKind.Material)]
    public string SkinMaterial { get; set; } = "";

    [Opt("Material da estrutura", "Structure material", Group = GOut, Kind = OptKind.Material)]
    public string StructureMaterial { get; set; } = "";

    [Opt("Material dos apoios e ligações", "Supports and connections material", Group = GOut, Kind = OptKind.Material)]
    public string SupportMaterial { get; set; } = "";

    [Opt("Categoria da pele no Revit", "Skin Revit category", Group = GOut)]
    public OrganicCategory Category { get; set; } = OrganicCategory.Roofs;

    [Opt("Altura livre mínima desejada", "Minimum desired clear height", Group = GOut, Kind = OptKind.Length, Min = 0)]
    public double MinClearance { get; set; } = U.M(2.1);

    [Opt("Escolher de novo pontos, atratores e colunas", "Pick points, attractors and columns again", Group = GOut, VisibleIf = "SupportSource=Picked|PickAttractors|ConnectColumns")]
    public bool Repick { get; set; }

    public CanopySettings ToCore(CanopyContext c)
    {
        var poly = new Polygon2(OrganicGeo.Unpack2(c.Footprint));
        var s = new CanopySettings
        {
            Footprint = poly,
            Surface = Surface,
            Height = Height,
            Rise = Rise,
            DirectionDeg = Direction,
            SlopePercent = SlopePercent,
            WavesU = WavesU,
            WavesV = WavesV,
            WavePhaseDeg = WavePhase,
            UmbrellaInverted = UmbrellaInverted,
            Sharpness = Sharpness,
            InfluenceRadius = InfluenceRadius,
            AttractorsDown = AttractorsDown,
            Formula = Formula,
            Skin = Skin,
            SkinThickness = SkinThickness,
            PanelSizeU = PanelSizeU,
            PanelSizeV = PanelSizeV,
            PanelJoint = PanelJoint,
            PanelRotationDeg = PanelRotation,
            SlatSpacing = SlatSpacing,
            SlatDepth = SlatDepth,
            SlatThickness = SlatThickness,
            SlatDirectionDeg = SlatDirection,
            MeshSize = MeshSize,
            Structure = Structure,
            StructureSpacingU = StructureSpacingU,
            StructureSpacingV = StructureSpacingV,
            StructureRotationDeg = StructureRotation,
            Profile = Profile,
            MemberWidth = MemberWidth,
            MemberHeight = MemberHeight,
            EdgeBeam = EdgeBeam,
            RadialCount = RadialCount,
            RadialRings = RadialRings,
            SupportKind = SupportSource == CanopySupportSource.None ? CanopySupports.None : SupportKind,
            TrunkDiameter = TrunkDiameter,
            ForkHeight = ForkHeight,
            Branches = Branches,
            BranchLevels = BranchLevels,
            BranchReach = BranchReach,
            BranchSpreadDeg = BranchSpread,
            BranchRotationDeg = BranchRotation,
            Connection = Connection,
            IntermediateFraction = IntermediateHeight / 100,
            MinClearance = MinClearance,
        };
        s.Supports = SupportSource switch
        {
            CanopySupportSource.Grid => CanopyStyles.SupportGrid(poly, SupportsU, SupportsV, Direction),
            CanopySupportSource.Picked => OrganicGeo.Unpack2(c.Supports),
            _ => new List<Vec2>(),
        };
        if (Surface == CanopySurface.Attractors && PickAttractors) s.Attractors = OrganicGeo.Unpack2(c.Attractors);
        if (ConnectColumns)
            s.Columns = (c.Columns ?? new List<double[]>()).Where(a => a.Length >= 4)
                .Select(a => new CanopyColumn { At = new Vec2(a[0], a[1]), BaseZ = a[2] - c.BaseZ, TopZ = a[3] - c.BaseZ }).ToList();
        return s;
    }

    public void ApplyStyle(CanopySettings s, int supportsU, int supportsV)
    {
        Surface = s.Surface;
        Height = s.Height;
        Rise = s.Rise;
        SlopePercent = s.SlopePercent;
        WavesU = s.WavesU;
        WavesV = s.WavesV;
        UmbrellaInverted = s.UmbrellaInverted;
        Sharpness = s.Sharpness;
        InfluenceRadius = s.InfluenceRadius;
        Skin = s.Skin;
        SkinThickness = s.SkinThickness;
        PanelSizeU = s.PanelSizeU;
        PanelSizeV = s.PanelSizeV;
        PanelJoint = s.PanelJoint;
        SlatSpacing = s.SlatSpacing;
        SlatDepth = s.SlatDepth;
        SlatThickness = s.SlatThickness;
        Structure = s.Structure;
        StructureSpacingU = s.StructureSpacingU;
        StructureSpacingV = s.StructureSpacingV;
        Profile = s.Profile;
        MemberWidth = s.MemberWidth;
        MemberHeight = s.MemberHeight;
        EdgeBeam = s.EdgeBeam;
        RadialCount = s.RadialCount;
        RadialRings = s.RadialRings;
        SupportKind = s.SupportKind == CanopySupports.None ? CanopySupports.Tree : s.SupportKind;
        SupportSource = supportsU * supportsV > 0 && s.SupportKind != CanopySupports.None ? CanopySupportSource.Grid : CanopySupportSource.None;
        if (supportsU > 0) SupportsU = supportsU;
        if (supportsV > 0) SupportsV = supportsV;
        TrunkDiameter = s.TrunkDiameter;
        ForkHeight = s.ForkHeight;
        Branches = s.Branches;
        BranchLevels = s.BranchLevels;
        BranchReach = s.BranchReach;
        BranchSpread = s.BranchSpreadDeg;
    }
}

/// <summary>Planta, base e pontos escolhidos (coordenadas do projeto).</summary>
public sealed class CanopyContext
{
    public List<double[]> Footprint { get; set; } = new();
    public double BaseZ { get; set; }
    public List<double[]>? Supports { get; set; }
    public List<double[]>? Attractors { get; set; }
    /// <summary>Colunas existentes: x, y, cota da base, cota do topo.</summary>
    public List<double[]>? Columns { get; set; }
    public string Style { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("org.canopy", Modules.Organic, "Coberturas orgânicas", "Organic canopies", Order = 20, Large = true,
    DescPt = "Cria coberturas de forma livre — almofada, abóbada, sela, ondas, guarda-chuvas, membrana, bolhas ou fórmula — com pele (contínua, painéis ou lâminas), estrutura e apoios em árvore, ligáveis a colunas existentes.",
    DescEn = "Creates free-form canopies — pillow, vault, saddle, waves, umbrellas, membrane, bubbles or formula — with skin, structure and tree supports, optionally connected to existing columns.",
    HelpPt = "1. Escolha um estilo pronto (ou suas últimas opções).\n2. Indique a planta: retângulo, contorno de piso/cobertura/ambiente, linhas desenhadas (contorno fechado) ou círculo.\n3. Ajuste superfície, pele, estrutura e apoios com a pré-visualização 3D.\n4. Opcional: ligue a colunas existentes (inclusive as Colunas orgânicas) no topo, num ponto intermediário ou nos dois — os galhos saem da coluna e sobem até a cobertura.\n\nPele, estrutura e apoios viram formas livres separadas, com material próprio. O relatório traz áreas, comprimentos, alturas, altura livre e pontos onde a água pode empoçar.\nPara alterar depois: Editar sistema gerado.\n\nLimitação: é um gerador geométrico; estrutura e drenagem devem ser verificadas pelos projetistas.",
    HelpEn = "Pick a style and an outline, adjust surface, skin, structure and supports with live 3D preview, optionally connect to existing columns. Reports areas, lengths, clear height and ponding points.",
    Keywords = "cobertura organica marquise toldo membrana tensionada guarda-chuva funil abobada sela paraboloide ondas arvore galhos pele paineis laminas canopy roof organic")]
public sealed class OrganicCanopyCommand : ToolCommand
{
    private const string ToolId = "org.canopy";

    public override void Run(ToolContext ctx)
    {
        var last = new CanopyStyle("", Loc.T("Minhas últimas opções", "My last options"), Loc.T("Abre o diálogo com as opções da última vez.", "Opens the dialog with your last options."), () => new CanopySettings(), 0, 0);
        var style = Pick.One(new[] { last }.Concat(CanopyStyles.All), s => s.Name, ctx.Tool.Name,
            Loc.T("Escolha um ponto de partida. Tudo pode ser ajustado depois.", "Pick a starting point. Everything can be adjusted next."), ctx.Owner, s => s.Description);
        if (style == null) ctx.Cancel();
        var c = new CanopyContext { Style = style!.Id };
        ReadFootprint(ctx, c);
        var o = ctx.LoadOptions<OrganicCanopyOptions>();
        if (style.Id != "") o.ApplyStyle(style.Create(), style.SupportsU, style.SupportsV);
        o = AskWithPicks(ctx, o, c, Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static void ReadFootprint(ToolContext ctx, CanopyContext c)
    {
        var i = Msg.Choose(Loc.T("Qual é a planta da cobertura?", "What is the canopy outline?"), ctx.Tool.Name,
            Loc.T("Retângulo por dois pontos", "Rectangle by two points"),
            Loc.T("Contorno de piso, cobertura ou ambiente", "Floor, roof or room outline"),
            Loc.T("Linhas desenhadas (contorno fechado)", "Drawn lines (closed outline)"),
            Loc.T("Círculo (centro e raio)", "Circle (center and radius)"));
        if (i < 0) ctx.Cancel();
        Polygon2 poly;
        switch (i)
        {
            case 0:
            {
                var a = ctx.PickPoint("Primeiro canto", "First corner");
                var b = ctx.PickPoint("Canto oposto", "Opposite corner");
                var r = Rect2.FromPoints(new[] { a.ToVec2(), b.ToVec2() });
                if (r.Width < U.Mm(500) || r.Height < U.Mm(500)) ToolContext.Fail("Retângulo pequeno demais.", "Rectangle too small.");
                poly = r.ToPolygon();
                c.BaseZ = a.Z;
                break;
            }
            case 1:
            {
                var e = ctx.PickElement("Escolha o piso, a cobertura ou o ambiente", "Pick the floor, roof or room", x => x is Floor or RoofBase or Room);
                var reg = e is Room room ? RevitGeo.RoomRegion(room) : RevitGeo.HostRegion(e);
                var box = RevitGeo.Box(e);
                if (reg == null || reg.Area <= 0) ToolContext.Fail("Não foi possível ler o contorno do elemento escolhido.", "Could not read the outline of the picked element.");
                poly = reg!.Outer;
                c.BaseZ = e switch
                {
                    Room rr => rr.Level?.Elevation ?? 0,
                    Floor => box?.Max.Z ?? 0,
                    _ => (e.LevelId != ElementId.InvalidElementId ? (ctx.Doc.GetElement(e.LevelId) as Level)?.Elevation : null) ?? box?.Min.Z ?? 0,
                };
                break;
            }
            case 2:
            {
                var curves = OrganicGeo.PickCurves(ctx, "Selecione as linhas do contorno (fechado)", "Select the outline lines (closed)");
                var loop = Shapes.ChainLoops(curves, 0.02).OrderByDescending(l => l.Sum(x => x.Length)).FirstOrDefault()
                           ?? throw new ToolException(Loc.T("As linhas não formam um contorno fechado.", "The lines do not form a closed outline."));
                var pts = new List<Vec2>();
                foreach (var cv in loop)
                    foreach (var p in RevitGeo.TessellateXY(cv))
                        if (pts.Count == 0 || pts[^1].DistanceTo(p) > 1e-6) pts.Add(p);
                if (pts.Count > 2 && pts[0].DistanceTo(pts[^1]) < 0.02) pts.RemoveAt(pts.Count - 1);
                poly = new Polygon2(pts).CleanForSketch(U.Mm(5)) ?? new Polygon2(pts);
                c.BaseZ = loop[0].GetEndPoint(0).Z;
                break;
            }
            default:
            {
                var center = ctx.PickPoint("Centro do círculo", "Circle center");
                var edge = ctx.PickPoint("Um ponto da borda (define o raio)", "A point on the edge (sets the radius)");
                var radius = center.ToVec2().DistanceTo(edge.ToVec2());
                if (radius < U.Mm(300)) ToolContext.Fail("Raio pequeno demais.", "Radius too small.");
                poly = Polygon2.Circle(center.ToVec2(), radius, 72);
                c.BaseZ = center.Z;
                break;
            }
        }
        if (poly.IsEmpty || poly.Area < U.M2ToSqFt(0.5)) ToolContext.Fail("Contorno inválido ou pequeno demais.", "Invalid or too small outline.");
        c.Footprint = OrganicGeo.Pack(poly.EnsureCounterClockwise());
    }

    /// <summary>Diálogo; depois de OK, pede pontos/atratores/colunas que faltarem e mostra de novo para conferir.</summary>
    private static OrganicCanopyOptions AskWithPicks(ToolContext ctx, OrganicCanopyOptions o, CanopyContext c, string okText)
    {
        for (var round = 0; round < 6; round++)
        {
            o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: okText, key: ToolId);
            var picked = false;
            if (o.SupportSource == CanopySupportSource.Picked && (o.Repick || c.Supports is not { Count: > 0 }))
            {
                c.Supports = PickPoints(ctx, "Clique os pontos dos apoios (Esc para terminar)", "Click the support points (Esc to finish)");
                picked = true;
            }
            if (o.Surface == CanopySurface.Attractors && o.PickAttractors && (o.Repick || c.Attractors is not { Count: > 0 }))
            {
                c.Attractors = PickPoints(ctx, "Clique os pontos atratores (Esc para terminar)", "Click the attractor points (Esc to finish)");
                picked = true;
            }
            if (o.ConnectColumns && (o.Repick || c.Columns is not { Count: > 0 }))
            {
                var els = ctx.UiDoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new PredicateFilter(e => e.Category?.BuiltInCategory is BuiltInCategory.OST_Columns or BuiltInCategory.OST_StructuralColumns or BuiltInCategory.OST_GenericModel || e is DirectShape),
                    Loc.T("Selecione as colunas a ligar (Concluir na barra de opções)", "Select the columns to connect (Finish on the options bar)"))
                    .Select(r => ctx.Doc.GetElement(r)).Where(e => e != null).ToList();
                c.Columns = els.Select(e => RevitGeo.Box(e)).Where(b => b != null)
                    .Select(b => new[] { (b!.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2, b.Min.Z, b.Max.Z }).ToList();
                picked = true;
            }
            o.Repick = false;
            if (!picked) return o;
        }
        return o;
    }

    private static List<double[]> PickPoints(ToolContext ctx, string pt, string en)
    {
        var list = new List<double[]>();
        while (list.Count < 200)
        {
            try
            {
                var p = ctx.PickPoint(pt, en);
                list.Add(new[] { p.X, p.Y });
            }
            catch (Exception ex) when (ex is OperationCanceledException or Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                break;
            }
        }
        return list;
    }

    private static string RoleColor(string role) => role switch
    {
        CanopyRole.Skin => "#D9CBB5",
        CanopyRole.Panel => "#A9C4D6",
        CanopyRole.Slat => "#B98B5E",
        CanopyRole.Structure => "#6B7682",
        CanopyRole.Edge => "#4E5864",
        CanopyRole.Support => "#8C6A4F",
        CanopyRole.Connector => "#B5652B",
        _ => "#9AA3AE",
    };

    private static PreviewModel Preview(ToolContext ctx, OrganicCanopyOptions o, CanopyContext c)
    {
        var s = o.ToCore(c);
        var r = CanopyGenerator.Generate(s);
        var pm = new PreviewModel();
        pm.Outline(s.Footprint, "#5F6B7A", 2);
        foreach (var l in r.PlanLines.Take(3000))
            for (var i = 1; i < l.Count; i++) pm.Line(l[i - 1], l[i], "#9AA3AE", 0.6);
        foreach (var p in s.Supports) pm.Dot(p, U.Mm(150), "#8C6A4F");
        foreach (var col in s.Columns) pm.Dot(col.At, U.Mm(150), "#B5652B");
        foreach (var a in s.Attractors) pm.Dot(a, U.Mm(100), "#3B7DD8");
        var center = s.Footprint.Centroid;
        // a 3D fica centrada no contorno (coordenadas do projeto podem estar longe da origem)
        OrganicGeo.AddToScene(pm.S3, r.Meshes, new Vec3(-center.X, -center.Y, 0), RoleColor);
        var q = r.Quantities;
        var info = Loc.F("Planta {0} · pele {1} · alturas {2} a {3} · altura livre mín. {4}",
            "Plan {0} · skin {1} · heights {2} to {3} · min. clear height {4}",
            U.FormatArea(q.PlanArea), U.FormatArea(q.SkinArea), U.FormatM(q.MinHeight), U.FormatM(q.MaxHeight), U.FormatM(q.MinClearance));
        if (q.PanelCount > 0) info += Loc.F(" · {0} painéis", " · {0} panels", q.PanelCount);
        if (q.StructureLength + q.EdgeLength > 0) info += Loc.F(" · estrutura {0}", " · structure {0}", U.FormatM(q.StructureLength + q.EdgeLength));
        if (q.SupportCount > 0) info += Loc.F(" · {0} apoios", " · {0} supports", q.SupportCount);
        if (q.ConnectionCount > 0) info += Loc.F(" · {0} ligações", " · {0} connections", q.ConnectionCount);
        if (r.Warnings.Count > 0) info += "\n⚠ " + string.Join("\n⚠ ", r.Warnings.Take(3));
        pm.Info = info;
        return pm;
    }

    internal static void Generate(ToolContext ctx, OrganicCanopyOptions o, CanopyContext c, string systemId)
    {
        var doc = ctx.Doc;
        var s = o.ToCore(c);
        var r = CanopyGenerator.Generate(s);
        if (r.Meshes.Count == 0)
            ToolContext.Fail("Nada a gerar: confira o contorno e as opções (pele, estrutura e apoios estão todos desligados?).",
                "Nothing to generate: check the outline and options (are skin, structure and supports all off?).");
        if (r.TriangleCount > 1_500_000)
            ToolContext.Fail("A malha ficaria pesada demais para o Revit. Aumente o tamanho da malha, dos painéis ou os espaçamentos.",
                "The mesh would be too heavy for Revit. Increase the mesh size, panel size or spacings.");
        foreach (var w in r.Warnings) ctx.Report.Warn(w);
        var created = new List<ElementId>();
        ElementId? viewId = null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var mats = new Dictionary<string, ElementId>();
            ElementId Mat(string role)
            {
                var name = role switch
                {
                    CanopyRole.Skin or CanopyRole.Panel or CanopyRole.Slat => o.SkinMaterial,
                    CanopyRole.Structure or CanopyRole.Edge => o.StructureMaterial,
                    _ => o.SupportMaterial,
                };
                if (!mats.TryGetValue(name, out var id)) mats[name] = id = Lookup.Material(doc, name)?.Id ?? ElementId.InvalidElementId;
                return id;
            }
            var offset = new Vec3(0, 0, c.BaseZ);
            var idx = 0;
            foreach (var group in r.Meshes.GroupBy(m => m.Role))
            {
                var label = CanopyRole.Label(group.Key);
                var category = group.Key switch
                {
                    CanopyRole.Skin or CanopyRole.Panel or CanopyRole.Slat => OrganicGeo.Category(o.Category),
                    CanopyRole.Support => BuiltInCategory.OST_StructuralColumns,
                    _ => BuiltInCategory.OST_StructuralFraming,
                };
                foreach (var chunk in group.Chunk(400))
                {
                    var geo = OrganicGeo.Build(chunk, offset, Mat, out var failed, out var err);
                    if (failed > 0) ctx.Report.ItemsFailed(label, failed, err ?? Loc.T("geometria inválida", "invalid geometry"));
                    if (geo.Count == 0) continue;
                    try
                    {
                        var ds = Shapes.Create(doc, geo, category, "TT Cobertura orgânica — " + label);
                        GenSystem.Mark(ds, systemId, ToolId, ++idx, label);
                        created.Add(ds.Id);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.ItemsFailed(label, chunk.Length, ex.Message);
                    }
                }
            }
            ctx.RequireCreated(created.Count, "partes da cobertura", "canopy parts");
            GenSystem.Register(doc, systemId, ToolId, o, c, Loc.T("Cobertura orgânica", "Organic canopy"));
            if (created.Count > 0 && !ctx.Silent && ctx.ActiveView is not View3D)
            {
                var b = s.Footprint.Bounds;
                viewId = OrganicGeo.View3DFor(ctx, Loc.F("TT Cobertura orgânica {0}", "TT Organic canopy {0}", systemId),
                    new XYZ(b.Min.X, b.Min.Y, c.BaseZ), new XYZ(b.Max.X, b.Max.Y, c.BaseZ + r.Quantities.MaxHeight));
            }
            ctx.Report.Created(created.Count);
            foreach (var id in created) ctx.Report.AddResult(id.Value);
        });

        var q = r.Quantities;
        var t = new TableData(Loc.T("Quantitativos da cobertura", "Canopy quantities"));
        t.AddColumn(Loc.T("Item", "Item")).AddColumn(Loc.T("Quantidade", "Quantity"), ColumnKind.Number, 2).AddColumn(Loc.T("Unidade", "Unit"));
        t.AddRow(Loc.T("Área coberta (planta)", "Covered area (plan)"), U.SqFtToM2(q.PlanArea), "m²");
        if (q.SkinArea > 0) t.AddRow(Loc.T("Área da pele (superfície real)", "Skin area (true surface)"), U.SqFtToM2(q.SkinArea), "m²");
        if (q.SkinVolume > 0) t.AddRow(Loc.T("Volume da pele", "Skin volume"), U.CuFtToM3(q.SkinVolume), "m³");
        if (q.PanelCount > 0) t.AddRow(Loc.T("Painéis", "Panels"), q.PanelCount, Loc.T("un", "pcs"));
        if (q.SlatLength > 0) t.AddRow(Loc.T("Lâminas", "Slats"), U.ToM(q.SlatLength), "m");
        if (q.StructureLength > 0) t.AddRow(Loc.T("Vigas da estrutura", "Structure beams"), U.ToM(q.StructureLength), "m");
        if (q.EdgeLength > 0) t.AddRow(Loc.T("Viga de borda", "Edge beam"), U.ToM(q.EdgeLength), "m");
        if (q.SupportCount > 0) t.AddRow(Loc.T("Apoios", "Supports"), q.SupportCount, Loc.T("un", "pcs"));
        if (q.SupportLength > 0) t.AddRow(Loc.T("Troncos, galhos e ligações", "Trunks, branches and connections"), U.ToM(q.SupportLength), "m");
        if (q.ConnectionCount > 0) t.AddRow(Loc.T("Ligações a colunas", "Column connections"), q.ConnectionCount, Loc.T("un", "pcs"));
        t.AddRow(Loc.T("Altura mínima / máxima da superfície", "Surface min / max height"), U.ToM(q.MinHeight), "m");
        t.AddRow("", U.ToM(q.MaxHeight), "m");
        t.AddRow(Loc.T("Altura livre mínima", "Minimum clear height"), U.ToM(q.MinClearance), "m");
        t.AddRow(Loc.T("Pontos onde a água pode empoçar", "Points where water may pond"), q.PondingPoints, Loc.T("un", "pcs"));
        ctx.Report.Tables.Add(t);
        var parts = new TableData(Loc.T("Volume por parte", "Volume by part"));
        parts.AddColumn(Loc.T("Parte", "Part")).AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3);
        foreach (var (role, v) in q.VolumeByRole) parts.AddRow(CanopyRole.Label(role), U.CuFtToM3(v));
        parts.AddTotalsRow(Loc.T("Total", "Total"), 1);
        ctx.Report.Tables.Add(parts);
        if (viewId != null && doc.GetElement(viewId) is View3D view)
        {
            try
            {
                ctx.UiDoc.ActiveView = view;
            }
            catch (Exception ex)
            {
                Log.Warn("Abrir vista 3D da cobertura: " + ex.Message);
            }
        }
        ctx.Report.ForceShow = true;
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = new OrganicCanopyOptions();
        Json.PopulateObject(o, optionsJson);
        var c = Json.Deserialize<CanopyContext>(contextJson) ?? throw new ToolException(Loc.T("Registro da cobertura inválido.", "Invalid canopy record."));
        o = AskWithPicks(ctx, o, c, Loc.T("Regenerar", "Regenerate"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover cobertura anterior", "Remove previous canopy"), () => GenSystem.Delete(ctx.Doc, systemId));
            Generate(ctx, o, c, systemId);
        });
    }
}
