using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Kinetic;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public sealed class KineticOptions : IValidatableOptions
{
    [Opt("Ajustar à face (calcula colunas e linhas)", "Fit to face (computes columns and rows)", Group = "Painéis|Panels")]
    public bool FitToFace { get; set; } = true;

    [Opt("Colunas", "Columns", Group = "Painéis|Panels", Min = 1, Max = 1000, VisibleIf = "!FitToFace")]
    public int Columns { get; set; } = 8;

    [Opt("Linhas", "Rows", Group = "Painéis|Panels", Min = 1, Max = 1000, VisibleIf = "!FitToFace")]
    public int Rows { get; set; } = 3;

    [Opt("Largura do painel", "Panel width", Group = "Painéis|Panels", Kind = OptKind.Length, Min = 0.01)]
    public double PanelWidth { get; set; } = U.Mm(600);

    [Opt("Altura do painel", "Panel height", Group = "Painéis|Panels", Kind = OptKind.Length, Min = 0.01)]
    public double PanelHeight { get; set; } = U.Mm(1000);

    [Opt("Junta entre painéis", "Gap between panels", Group = "Painéis|Panels", Kind = OptKind.Length, Min = 0)]
    public double Gap { get; set; } = U.Mm(20);

    [Opt("Espessura", "Thickness", Group = "Painéis|Panels", Kind = OptKind.Length, Min = 0.001)]
    public double Thickness { get; set; } = U.Mm(30);

    [Opt("Afastamento da fachada", "Offset from facade", Group = "Painéis|Panels", Kind = OptKind.Length, Min = 0)]
    public double Offset { get; set; } = U.Mm(150);

    [Opt("Eixo de rotação", "Rotation axis", Group = "Movimento|Movement")]
    public HingeAxis Axis { get; set; } = HingeAxis.Vertical;

    [Opt("Posição do eixo", "Axis position", Group = "Movimento|Movement")]
    public HingePosition Hinge { get; set; } = HingePosition.Center;

    [Opt("Ângulo fechado", "Closed angle", Group = "Movimento|Movement", Kind = OptKind.Angle, Min = -180, Max = 180)]
    public double ClosedAngle { get; set; }

    [Opt("Ângulo aberto", "Open angle", Group = "Movimento|Movement", Kind = OptKind.Angle, Min = -180, Max = 180)]
    public double OpenAngle { get; set; } = 80;

    [Opt("Comportamento", "Behavior", Group = "Movimento|Movement")]
    public KineticMode Mode { get; set; } = KineticMode.Uniform;

    [Opt("Direção (escalonado/onda)", "Direction (stagger/wave)", Group = "Movimento|Movement", Kind = OptKind.Angle, VisibleIf = "Mode=Stagger,Wave")]
    public double Direction { get; set; }

    [Opt("Escalonamento (0–1)", "Stagger amount (0–1)", Group = "Movimento|Movement", Min = 0, Max = 1, VisibleIf = "Mode=Stagger")]
    public double Stagger { get; set; } = 0.6;

    [Opt("Períodos da onda", "Wave periods", Group = "Movimento|Movement", Min = 0.1, Max = 50, VisibleIf = "Mode=Wave")]
    public double WavePeriods { get; set; } = 1;

    [Opt("Atrator U (0–1)", "Attractor U (0–1)", Group = "Movimento|Movement", Min = 0, Max = 1, VisibleIf = "Mode=Attractor")]
    public double AttractorU { get; set; } = 0.5;

    [Opt("Atrator V (0–1)", "Attractor V (0–1)", Group = "Movimento|Movement", Min = 0, Max = 1, VisibleIf = "Mode=Attractor")]
    public double AttractorV { get; set; } = 0.5;

    [Opt("Alcance do atrator", "Attractor reach", Group = "Movimento|Movement", Min = 0.01, Max = 2, VisibleIf = "Mode=Attractor")]
    public double AttractorRadius { get; set; } = 0.6;

    [Opt("Inverter atrator", "Invert attractor", Group = "Movimento|Movement", VisibleIf = "Mode=Attractor")]
    public bool InvertAttractor { get; set; }

    [Opt("Fórmula (0 a 1)", "Formula (0 to 1)", Group = "Movimento|Movement", Kind = OptKind.Formula, VisibleIf = "Mode=Formula",
        Tip = "Variáveis: i, j, u, v, t (estado), d, r, cols, rows. Ex.: t*(0.5+0.5*sin(u*360))|Variables: i, j, u, v, t, d, r, cols, rows.")]
    public string Formula { get; set; } = "t";

    [Opt("Variação aleatória (0–1)", "Random spread (0–1)", Group = "Movimento|Movement", Min = 0, Max = 1, VisibleIf = "Mode=Random")]
    public double RandomSpread { get; set; } = 0.5;

    [Opt("Semente", "Seed", Group = "Movimento|Movement", VisibleIf = "Mode=Random,Formula")]
    public int Seed { get; set; } = 5;

    [Opt("Estados a gerar (% de abertura)", "States to generate (% open)", Group = "Estados|States", Tip = "Ex.: 50  ou  0;50;100 (um conjunto e uma vista 3D por estado).|E.g. 50 or 0;50;100 (one set and one 3D view per state).")]
    public string States { get; set; } = "50";

    [Opt("Criar vistas 3D por estado", "Create 3D views per state", Group = "Estados|States")]
    public bool CreateViews { get; set; } = true;

    [Opt("Exportar OBJ para animação (pasta)", "Export OBJ for animation (folder)", Group = "Estados|States", Kind = OptKind.Folder, Tip = "Gera uma sequência de quadros 0→100% (Blender, Rhino, 3ds Max...).|Writes a 0→100% frame sequence.")]
    public string ObjFolder { get; set; } = "";

    [Opt("Quadros da animação", "Animation frames", Group = "Estados|States", Min = 2, Max = 600)]
    public int ObjFrames { get; set; } = 24;

    [Opt("Material", "Material", Group = "Saída|Output", Kind = OptKind.Material)]
    public string Material { get; set; } = "";

    [Opt("Saída", "Output", Group = "Saída|Output")]
    public GenOutput Output { get; set; } = GenOutput.Parametric;

    [Opt("Família/tipo", "Family/type", Group = "Saída|Output", Kind = OptKind.FamilyType, Filter = "OST_GenericModel", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";

    public List<double> StateValues()
    {
        var list = new List<double>();
        foreach (var part in (States ?? "").Split(new[] { ';', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries))
            if (U.TryParseNumber(part.Replace("%", ""), out var v))
                list.Add(Math.Clamp(v, 0, 100));
        if (list.Count == 0) list.Add(50);
        return list.Distinct().ToList();
    }

    public IEnumerable<string> Validate()
    {
        if (StateValues().Count > 10) yield return Loc.T("Use no máximo 10 estados.", "Use at most 10 states.");
    }

    public KineticSettings ToCore(int cols, int rows) => new()
    {
        Columns = cols,
        Rows = rows,
        PanelWidth = PanelWidth,
        PanelHeight = PanelHeight,
        Gap = Gap,
        Thickness = Thickness,
        OffsetFromFacade = Offset,
        Axis = Axis,
        Hinge = Hinge,
        ClosedAngleDeg = ClosedAngle,
        OpenAngleDeg = OpenAngle,
        Mode = Mode,
        DirectionDeg = Direction,
        StaggerAmount = Stagger,
        WavePeriods = WavePeriods,
        AttractorU = AttractorU,
        AttractorV = AttractorV,
        AttractorRadius = AttractorRadius,
        InvertAttractor = InvertAttractor,
        Formula = Formula,
        Seed = Seed,
        RandomSpread = RandomSpread,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.kinetic", Modules.Generators, "Painéis cinéticos", "Kinetic panels", Order = 50, Large = true,
    DescPt = "Conjuntos de painéis móveis com eixos, ângulos de abertura, comportamentos (onda, atrator, fórmula), estados em vistas e exportação para animação.",
    DescEn = "Sets of movable panels with axes, opening angles, behaviors (wave, attractor, formula), states in views and export for animation.",
    HelpPt = "Escolha a face da fachada; o conjunto começa no canto inferior esquerdo da face. Cada estado (ex.: 0;50;100) gera um conjunto e, se pedido, uma vista 3D que mostra só aquele estado. O ângulo de cada painel fica no parâmetro TT_Abertura.\n\nPara mudar a abertura depois, use \"Atualizar estado cinético\".\n\nLimitação: a movimentação é paramétrica (estados). Animação contínua em tempo real e simulação física não fazem parte do Revit — use a exportação OBJ para animar em outro programa.",
    HelpEn = "Pick the facade face; the set starts at the face's bottom-left corner. Each state generates a set and optionally a 3D view showing only that state. Each panel's angle is stored in TT_Abertura.\n\nLimitation: movement is parametric (states). Real-time animation and physics simulation are outside Revit — use OBJ export to animate elsewhere.",
    Keywords = "cinetico cinético movel painel fachada abertura kinetic movable panel facade")]
public sealed class KineticCommand : ToolCommand
{
    public sealed class Context
    {
        public double[] Frame { get; set; } = new double[12];
        public double FaceWidth { get; set; }
        public double FaceHeight { get; set; }
    }

    public override void Run(ToolContext ctx)
    {
        var r = ctx.PickFace("Escolha a face plana da fachada", "Pick the planar facade face");
        var el = ctx.Doc.GetElement(r);
        if (el.GetGeometryObjectFromReference(r) is not PlanarFace pf) throw new ToolException(Loc.T("A face precisa ser plana.", "The face must be planar."));
        var (reg, frame) = GenHost.PlanarFaceRegion(pf, GenHost.TransformOf(el));
        var c = new Context { Frame = PlanarContext.From(frame, reg).Frame, FaceWidth = reg.Bounds.Width, FaceHeight = reg.Bounds.Height };
        var o = ctx.Ask(ctx.LoadOptions<KineticOptions>(), preview: x => Preview(x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static (int Cols, int Rows) Grid(KineticOptions o, Context c)
    {
        if (!o.FitToFace) return (o.Columns, o.Rows);
        var cols = Math.Max(1, Core.Mathx.MathUtil.FitCount(c.FaceWidth, o.PanelWidth, o.Gap));
        var rows = Math.Max(1, Core.Mathx.MathUtil.FitCount(c.FaceHeight, o.PanelHeight, o.Gap));
        return (cols, rows);
    }

    private static Frame3 FrameOf(Context c) => new PlanarContext { Frame = c.Frame }.GetFrame();

    private static PreviewModel Preview(KineticOptions o, Context c)
    {
        var (cols, rows) = Grid(o, c);
        var s = o.ToCore(cols, rows);
        var state = o.StateValues()[0] / 100.0;
        var panels = KineticGenerator.Evaluate(s, Frame3.World, state);
        var pm = new PreviewModel();
        pm.Outline(Polygon2.Rectangle(Vec2.Zero, c.FaceWidth, c.FaceHeight), "#5F6B7A", 2);
        foreach (var p in panels.Take(4000))
        {
            // Projeção frontal (X, Y da fachada); a cor indica a abertura.
            var pts = p.Corners.Select(v => new Vec2(v.X, v.Y)).ToList();
            pm.Polygon(new Polygon2(pts).IsEmpty ? Polygon2.Rectangle(pts[0], 0.01, 0.01) : new Polygon2(pts), GenHost.Gradient(p.OpeningFraction), "#6B4A33", 0.6);
        }
        // 3D: painéis na posição do estado, sobre a fachada (plano XY = fachada, Z = para fora)
        var sc = pm.S3;
        var facade = new Frame3(Vec3.Zero, Vec3.UnitX, Vec3.UnitZ, -Vec3.UnitY);
        sc.Face(facade, Polygon2.Rectangle(Vec2.Zero, c.FaceWidth, c.FaceHeight), "#D9D4CC", 0.3);
        var panels3d = KineticGenerator.Evaluate(s, facade, state);
        foreach (var p in panels3d.Take(5000)) sc.Box(p.Box, GenHost.Gradient(p.OpeningFraction));
        pm.Info = Loc.F("{0} × {1} = {2} painéis · estado {3:0}% · {4}°–{5}°", "{0} × {1} = {2} panels · state {3:0}% · {4}°–{5}°",
            cols, rows, cols * rows, state * 100, panels.Min(x => x.AngleDeg).ToString("0", CultureInfo.CurrentCulture), panels.Max(x => x.AngleDeg).ToString("0", CultureInfo.CurrentCulture));
        return pm;
    }

    internal static void Generate(ToolContext ctx, KineticOptions o, Context c, string systemId)
    {
        var doc = ctx.Doc;
        var (cols, rows) = Grid(o, c);
        var s = o.ToCore(cols, rows);
        var frame = FrameOf(c);
        var states = o.StateValues();
        ctx.CheckCount(cols * rows * states.Count, "painéis", "panels");
        var sink = new BoxSink(ctx, o.Output, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Cinético" };
        sink.Prepare();
        var labels = states.Select(v => Loc.F("Estado {0:0}%", "State {0:0}%", v)).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            for (var k = 0; k < states.Count; k++)
            {
                var panels = KineticGenerator.Evaluate(s, frame, states[k] / 100.0);
                var stateSink = new BoxSink(ctx, sink.Output, BoxKind.Block, systemId, o.UserFamily, status: labels[k]) { Name = "TT Cinético " + labels[k] };
                if (sink.Output != GenOutput.FreeForm) stateSink.Prepare();
                var i = 0;
                foreach (var p in panels)
                {
                    stateSink.Add(p.Box, string.IsNullOrWhiteSpace(o.Material) ? null : o.Material, k * 100000 + i++, e =>
                    {
                        RevitGeo.Set(e, Core.Mathx.MathUtil.ToRadians(p.AngleDeg), SharedParams.Abertura);
                        RevitGeo.Set(e, $"P{p.J + 1:00}-{p.I + 1:00}", SharedParams.Codigo);
                    });
                }
                stateSink.Finish();
                foreach (var id in stateSink.Created) ctx.Report.AddResult(id.Value);
                ctx.Report.Created(stateSink.Created.Count);
            }
            GenSystem.Register(doc, systemId, "gen.kinetic", o, c, Loc.T("Cinético", "Kinetic"));

            if (o.CreateViews && states.Count > 1)
            {
                var statusId = ViewFilters.SharedParamId(doc, SharedParams.Status);
                var vft = Lookup.ViewFamilyType(doc, ViewFamily.ThreeDimensional);
                if (statusId == ElementId.InvalidElementId || vft == null)
                {
                    ctx.Report.Info(Loc.T("Vistas por estado não criadas (parâmetro TT_Status indisponível).", "Views per state not created (TT_Status parameter unavailable)."));
                }
                else
                {
                    var cats = ViewFilters.CategoryIds(BuiltInCategory.OST_GenericModel);
                    for (var k = 0; k < states.Count; k++)
                    {
                        var v = View3D.CreateIsometric(doc, vft.Id);
                        v.Name = Lookup.UniqueViewName(doc, $"TT {Loc.T("Cinético", "Kinetic")} {systemId} — {labels[k]}");
                        for (var m = 0; m < states.Count; m++)
                        {
                            if (m == k) continue;
                            var rule = ParameterFilterRuleFactory.CreateEqualsRule(statusId, labels[m]);
                            var f = ViewFilters.Ensure(doc, $"TT {systemId} {labels[m]}", cats, new ElementParameterFilter(rule));
                            ViewFilters.Apply(v, f.Id, null, visible: false);
                        }
                        ctx.Report.Count(Loc.T("Vistas", "Views"));
                    }
                }
            }
        });

        if (!string.IsNullOrWhiteSpace(o.ObjFolder))
        {
            try
            {
                Directory.CreateDirectory(o.ObjFolder);
                for (var f = 0; f < o.ObjFrames; f++)
                {
                    var t = (double)f / (o.ObjFrames - 1);
                    var panels = KineticGenerator.Evaluate(s, frame, t);
                    ObjWriter.Write(Path.Combine(o.ObjFolder, $"TT_cinetico_{systemId}_{f:000}.obj"), panels.Select(p => (p.Box, $"P{p.J + 1:00}_{p.I + 1:00}")));
                }
                ctx.Report.Success(Loc.F("{0} quadros OBJ exportados em {1}", "{0} OBJ frames exported to {1}", o.ObjFrames, o.ObjFolder));
            }
            catch (Exception ex)
            {
                ctx.Report.Warn(Loc.F("Falha ao exportar OBJ: {0}", "OBJ export failed: {0}", ex.Message));
            }
        }
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<KineticOptions>(optionsJson) ?? new KineticOptions();
        var c = Json.Deserialize<Context>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "gen.kinetic");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}

/// <summary>Regenera qualquer sistema gerado a partir do registro no projeto.</summary>
internal static class GenRegistry
{
    public static readonly Dictionary<string, Action<ToolContext, string, string, string>> Regenerators = new()
    {
        ["gen.pattern"] = PatternCommand.Regenerate,
        ["gen.image"] = ImageCommand.Regenerate,
        ["gen.brise"] = BriseCommand.Regenerate,
        ["gen.spaceframe"] = SpaceFrameCommand.Regenerate,
        ["join.free"] = Joinery.FreeCabinetCommand.Regenerate,
        ["gen.kinetic"] = KineticCommand.Regenerate,
        ["wall.slats"] = Interiors.SlatsCommand.Regenerate,
        ["wall.boiserie"] = Interiors.BoiserieCommand.Regenerate,
        ["join.module"] = Joinery.JoineryUi.Regenerate,
    };

    public static (string SystemId, ProjectStorage.GeneratedSystem System) PickSystem(ToolContext ctx, string? toolFilter = null)
    {
        Element? e = ctx.Selected().FirstOrDefault(x => ProjectStorage.ReadGenerated(x) != null);
        e ??= ctx.PickElement("Escolha um elemento do sistema gerado", "Pick an element of the generated system",
            x => ProjectStorage.ReadGenerated(x) is { } g && (toolFilter == null || g.ToolId == toolFilter));
        var g = ProjectStorage.ReadGenerated(e) ?? throw new ToolException(Loc.T("O elemento não pertence a um sistema gerado.", "The element is not part of a generated system."));
        var sys = ProjectStorage.GetSystem(ctx.Doc, g.SystemId)
                  ?? throw new ToolException(Loc.T("Registro do sistema não encontrado no projeto.", "System record not found in the project."));
        return (g.SystemId, sys);
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.edit", Modules.Generators, "Editar sistema gerado", "Edit generated system", Order = 90,
    DescPt = "Reabre as opções de qualquer padrão, mosaico, brise, quadro espacial ou cinético gerado e o regenera no mesmo lugar.",
    DescEn = "Reopens the options of any generated pattern, mosaic, louver, space frame or kinetic set and regenerates it in place.",
    HelpPt = "Selecione um elemento criado por um gerador. As opções usadas na criação são carregadas; ao confirmar, o sistema antigo é substituído (tudo em um único Desfazer).",
    HelpEn = "Select an element created by a generator. The options used at creation are loaded; on confirm, the old system is replaced (single Undo).",
    Keywords = "editar regenerar sistema gerado edit regenerate generated")]
public sealed class EditGeneratedCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var (id, sys) = GenRegistry.PickSystem(ctx);
        if (!GenRegistry.Regenerators.TryGetValue(sys.ToolId, out var regen))
            throw new ToolException(Loc.F("Sistemas de \"{0}\" não podem ser regenerados.", "Systems from \"{0}\" cannot be regenerated.", sys.ToolId));
        ctx.Group(ctx.Tool.Name, () => regen(ctx, sys.OptionsJson, sys.ContextJson, id));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.select", Modules.Generators, "Selecionar sistema gerado", "Select generated system", Order = 91, Ribbon = false,
    DescPt = "Seleciona todos os elementos do sistema gerado ao qual o elemento escolhido pertence.",
    DescEn = "Selects all elements of the generated system the picked element belongs to.",
    Keywords = "selecionar sistema gerado select generated")]
public sealed class SelectGeneratedCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var (id, _) = GenRegistry.PickSystem(ctx);
        var ids = ProjectStorage.FindSystem(ctx.Doc, id).Select(e => e.Id).ToList();
        ctx.UiDoc.Selection.SetElementIds(ids);
        ctx.Report.Count(Loc.T("Elementos", "Elements"), ids.Count);
    }
}
