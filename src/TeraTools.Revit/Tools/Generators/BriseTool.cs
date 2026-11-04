using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Facade;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public sealed class BriseOptions
{
    [Opt("Orientação", "Orientation", Group = "Aletas|Fins")]
    public FinOrientation Orientation { get; set; } = FinOrientation.Vertical;

    [Opt("Largura da aleta (perpendicular à fachada)", "Fin depth (perpendicular to facade)", Group = "Aletas|Fins", Kind = OptKind.Length, Min = 0.001)]
    public double FinDepth { get; set; } = U.Mm(300);

    [Opt("Espessura", "Thickness", Group = "Aletas|Fins", Kind = OptKind.Length, Min = 0.001)]
    public double FinThickness { get; set; } = U.Mm(40);

    [Opt("Distância da fachada", "Distance from facade", Group = "Aletas|Fins", Kind = OptKind.Length, Min = 0)]
    public double DistanceFromFacade { get; set; } = U.Mm(100);

    [Opt("Rotação (em torno do eixo da aleta)", "Rotation (about the fin axis)", Group = "Aletas|Fins", Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Pivô da rotação", "Rotation pivot", Group = "Aletas|Fins")]
    public FinPivot Pivot { get; set; } = FinPivot.Center;

    [Opt("Distribuição", "Distribution", Group = "Distribuição|Distribution")]
    public FinDistribution Distribution { get; set; } = FinDistribution.BySpacing;

    [Opt("Espaçamento (eixo a eixo)", "Spacing (center to center)", Group = "Distribuição|Distribution", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Distribution!=ByCount")]
    public double Spacing { get; set; } = U.Mm(300);

    [Opt("Quantidade", "Count", Group = "Distribuição|Distribution", Min = 1, Max = 10000, VisibleIf = "Distribution=ByCount")]
    public int Count { get; set; } = 20;

    [Opt("Margem inicial", "Start margin", Group = "Distribuição|Distribution", Kind = OptKind.Length, Min = 0)]
    public double StartMargin { get; set; }

    [Opt("Margem final", "End margin", Group = "Distribuição|Distribution", Kind = OptKind.Length, Min = 0)]
    public double EndMargin { get; set; }

    [Opt("Usar altura da parede", "Use wall height", Group = "Limites|Limits")]
    public bool UseWallHeight { get; set; } = true;

    [Opt("Cota inferior (relativa ao nível)", "Bottom elevation (from level)", Group = "Limites|Limits", Kind = OptKind.Length, VisibleIf = "!UseWallHeight")]
    public double Bottom { get; set; }

    [Opt("Cota superior (relativa ao nível)", "Top elevation (from level)", Group = "Limites|Limits", Kind = OptKind.Length, VisibleIf = "!UseWallHeight")]
    public double Top { get; set; } = U.M(3);

    [Opt("Ajustar aos limites da fachada (desviar de janelas/portas)", "Fit to facade limits (avoid windows/doors)", Group = "Limites|Limits")]
    public bool ClipToFacade { get; set; }

    [Opt("Variação", "Variation", Group = "Variação paramétrica|Parametric variation")]
    public VariationMode Variation { get; set; } = VariationMode.None;

    [Opt("Rotação mínima", "Minimum rotation", Group = "Variação paramétrica|Parametric variation", Kind = OptKind.Angle, VisibleIf = "Variation!=None")]
    public double VariationMin { get; set; } = -45;

    [Opt("Rotação máxima", "Maximum rotation", Group = "Variação paramétrica|Parametric variation", Kind = OptKind.Angle, VisibleIf = "Variation!=None")]
    public double VariationMax { get; set; } = 45;

    [Opt("Períodos da onda", "Wave periods", Group = "Variação paramétrica|Parametric variation", Min = 0.1, Max = 100, VisibleIf = "Variation=Sine")]
    public double SinePeriods { get; set; } = 2;

    [Opt("Posição do atrator (0–1)", "Attractor position (0–1)", Group = "Variação paramétrica|Parametric variation", Min = 0, Max = 1, VisibleIf = "Variation=Attractor")]
    public double AttractorPosition { get; set; } = 0.5;

    [Opt("Alcance do atrator (0–1)", "Attractor reach (0–1)", Group = "Variação paramétrica|Parametric variation", Min = 0.01, Max = 1, VisibleIf = "Variation=Attractor")]
    public double AttractorRadius { get; set; } = 0.3;

    [Opt("Fórmula (0 a 1)", "Formula (0 to 1)", Group = "Variação paramétrica|Parametric variation", Kind = OptKind.Formula, VisibleIf = "Variation=Formula",
        Tip = "Variáveis: t (0–1 ao longo), i (índice), n (quantidade), s (posição em m), r (aleatório).|Variables: t, i, n, s, r.")]
    public string Formula { get; set; } = "0.5 + 0.5*sin(t*360)";

    [Opt("Semente", "Seed", Group = "Variação paramétrica|Parametric variation", VisibleIf = "Variation=Random")]
    public int Seed { get; set; } = 3;

    [Opt("Variar também a largura", "Also vary depth", Group = "Variação paramétrica|Parametric variation", VisibleIf = "Variation!=None")]
    public bool VaryDepth { get; set; }

    [Opt("Largura mínima", "Minimum depth", Group = "Variação paramétrica|Parametric variation", Kind = OptKind.Length, Min = 0.001, VisibleIf = "VaryDepth&Variation!=None")]
    public double DepthMin { get; set; } = U.Mm(150);

    [Opt("Largura máxima", "Maximum depth", Group = "Variação paramétrica|Parametric variation", Kind = OptKind.Length, Min = 0.001, VisibleIf = "VaryDepth&Variation!=None")]
    public double DepthMax { get; set; } = U.Mm(400);

    [Opt("Material do conjunto 1", "Set 1 material", Group = "Materiais por conjunto|Materials by set", Kind = OptKind.Material)]
    public string Material1 { get; set; } = "";

    [Opt("Material do conjunto 2", "Set 2 material", Group = "Materiais por conjunto|Materials by set", Kind = OptKind.Material)]
    public string Material2 { get; set; } = "";

    [Opt("Material do conjunto 3", "Set 3 material", Group = "Materiais por conjunto|Materials by set", Kind = OptKind.Material)]
    public string Material3 { get; set; } = "";

    [Opt("Sequência dos conjuntos", "Set sequence", Group = "Materiais por conjunto|Materials by set", Tip = "Ex.: 1,1,2 = duas aletas do conjunto 1 e uma do 2, repetindo.|E.g. 1,1,2 = two fins of set 1 then one of set 2, repeating.")]
    public string MaterialSequence { get; set; } = "1";

    [Opt("Saída", "Output", Group = "Saída|Output")]
    public GenOutput Output { get; set; } = GenOutput.Parametric;

    [Opt("Família/tipo", "Family/type", Group = "Saída|Output", Kind = OptKind.FamilyType, Filter = "OST_GenericModel,OST_SpecialityEquipment", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";

    [Opt("Criar tabela de quantitativos", "Create quantity schedule", Group = "Saída|Output")]
    public bool CreateSchedule { get; set; } = true;

    public BriseSettings ToCore(double z0, double z1) => new()
    {
        Orientation = Orientation,
        FinDepth = FinDepth,
        FinThickness = FinThickness,
        Distribution = Distribution,
        Spacing = Spacing,
        Count = Count,
        StartMargin = StartMargin,
        EndMargin = EndMargin,
        BottomElevation = z0,
        TopElevation = z1,
        DistanceFromFacade = DistanceFromFacade,
        Pivot = Pivot,
        RotationDeg = Rotation,
        Variation = Variation,
        VariationMinDeg = VariationMin,
        VariationMaxDeg = VariationMax,
        SinePeriods = SinePeriods,
        AttractorPosition = AttractorPosition,
        AttractorRadius = AttractorRadius,
        Formula = Formula,
        Seed = Seed,
        VaryDepth = VaryDepth,
        DepthMin = DepthMin,
        DepthMax = DepthMax,
        MaterialSequence = MaterialSequence,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.brise", Modules.Generators, "Aletas e brises", "Fins and louvers", Order = 30, Large = true,
    DescPt = "Gera aletas verticais ou horizontais ao longo de paredes ou linhas, com inclinação, variação paramétrica, materiais por conjunto e quantitativos.",
    DescEn = "Generates vertical or horizontal fins along walls or lines, with tilt, parametric variation, materials by set and quantities.",
    HelpPt = "Escolha uma parede (o brise fica do lado externo, a partir da face) ou linhas de modelo (caminho livre, inclusive curvo). A variação altera a rotação (e opcionalmente a largura) de cada aleta: linear, onda, aleatória, atrator ou fórmula.\n\nCom 'Ajustar aos limites da fachada', as aletas são interrompidas nas janelas e portas da parede.",
    HelpEn = "Pick a wall (fins on the exterior side) or model lines (free path, including curves). Variation changes each fin's rotation (and optionally depth).\n\nWith 'Fit to facade limits' fins stop at the wall's windows and doors.",
    Keywords = "brise aleta brises louver fin fachada sombreamento")]
public sealed class BriseCommand : ToolCommand
{
    public sealed class Context
    {
        public List<double[]> Path { get; set; } = new();
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public double LevelZ { get; set; }
        public double FacadeOffset { get; set; }
        public bool Left { get; set; } = true;
        public long WallId { get; set; } = -1;
        public PlanarContext? Facade { get; set; }
    }

    public override void Run(ToolContext ctx)
    {
        var i = Msg.Choose(Loc.T("Onde gerar as aletas?", "Where to generate the fins?"), ctx.Tool.Name,
            Loc.T("Ao longo de uma parede", "Along a wall"), Loc.T("Ao longo de linhas de modelo", "Along model lines"));
        if (i < 0) ctx.Cancel();
        var c = new Context();
        if (i == 0)
        {
            var wall = (Wall)ctx.PickElement("Escolha a parede", "Pick the wall", e => e is Wall);
            var (path, facade, z0, z1, ext) = GenHost.WallFacade(wall);
            c.Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList();
            c.Z0 = z0;
            c.Z1 = z1;
            c.LevelZ = (ctx.Doc.GetElement(wall.LevelId) as Level)?.Elevation ?? z0;
            c.WallId = wall.Id.Value;
            c.Left = path.TangentAt(0).Perp.Dot(ext) >= 0;
            c.FacadeOffset = wall.Width / 2;
            c.Facade = PlanarContext.From(Frame3.World, facade);
        }
        else
        {
            var lines = ctx.SelectedOrPick("Selecione as linhas do caminho", "Select the path lines", e => e is CurveElement);
            var path = GenHost.PathFromCurves(lines.OfType<CurveElement>().Select(l => l.GeometryCurve));
            c.Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList();
            var z = lines.OfType<CurveElement>().First().GeometryCurve.GetEndPoint(0).Z;
            var lvl = Lookup.LevelBelow(ctx.Doc, z);
            c.LevelZ = lvl?.Elevation ?? z;
            c.Z0 = c.LevelZ;
            c.Z1 = c.LevelZ + U.M(3);
            c.Left = true;
        }
        var o = ctx.LoadOptions<BriseOptions>();
        if (c.WallId < 0) o.UseWallHeight = false;
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static BriseResult Compute(BriseOptions o, Context c)
    {
        var path = new PolylinePath(c.Path.Select(p => new Vec2(p[0], p[1])));
        var z0 = o.UseWallHeight && c.WallId >= 0 ? c.Z0 : c.LevelZ + o.Bottom;
        var z1 = o.UseWallHeight && c.WallId >= 0 ? c.Z1 : c.LevelZ + o.Top;
        var s = o.ToCore(z0, z1);
        s.Side = c.Left ? FinSide.Left : FinSide.Right;
        s.DistanceFromFacade = o.DistanceFromFacade + c.FacadeOffset;
        var facade = o.ClipToFacade && c.Facade != null ? c.Facade.GetRegion() : null;
        return BriseGenerator.Generate(s, path, facade);
    }

    private static PreviewModel Preview(BriseOptions o, Context c)
    {
        var r = Compute(o, c);
        var pm = new PreviewModel();
        var pts = c.Path.Select(p => new Vec2(p[0], p[1])).ToList();
        for (var k = 0; k + 1 < pts.Count; k++) pm.Line(pts[k], pts[k + 1], "#5F6B7A", 2);
        foreach (var f in r.Fins.Take(5000))
        {
            var corners = f.Box.Corners().Select(p => p.XY).ToList();
            pm.Polygon(PolygonOps.ConvexHull(corners), GenHost.Gradient(f.VariationValue), "#6B4A33", 0.6);
        }
        pm.Info = Loc.F("{0} aletas · {1} lineares · {2}", "{0} fins · {1} linear · {2}", r.Fins.Count, U.FormatM(r.TotalLength), U.FormatVolume(r.TotalVolume));
        return pm;
    }

    internal static void Generate(ToolContext ctx, BriseOptions o, Context c, string systemId)
    {
        var doc = ctx.Doc;
        var r = Compute(o, c);
        ctx.CheckCount(r.Fins.Count, "aletas", "fins");
        var mats = new[] { o.Material1, o.Material2, o.Material3 };
        var sink = new BoxSink(ctx, o.Output, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Brise" };
        sink.Prepare();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            foreach (var f in r.Fins)
            {
                var mat = mats[Math.Clamp(f.MaterialIndex, 0, 2)];
                sink.Add(f.Box, string.IsNullOrWhiteSpace(mat) ? null : mat, f.Index, e =>
                {
                    RevitGeo.Set(e, $"A{f.Index + 1:000}", SharedParams.Codigo);
                    RevitGeo.Set(e, (f.MaterialIndex + 1).ToString(), SharedParams.Grupo);
                    RevitGeo.Set(e, Core.Mathx.MathUtil.ToRadians(f.RotationDeg), SharedParams.Abertura);
                });
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, "gen.brise", o, c, Loc.T("Brise", "Louvers"));
            if (o.CreateSchedule && sink.Output == GenOutput.Parametric)
            {
                Schedules.Create(doc, BuiltInCategory.OST_GenericModel, Loc.F("TT Brise {0}", "TT Louvers {0}", systemId),
                    new[]
                    {
                        new Schedules.Spec(SharedParams.Grupo, Loc.T("Conjunto", "Set")),
                        new Schedules.Spec(SharedParams.Altura, Loc.T("Comprimento/altura", "Length/height")),
                        new Schedules.Spec(SharedParams.Largura),
                        new Schedules.Spec(SharedParams.Profundidade),
                        new Schedules.Spec(SharedParams.Material),
                        new Schedules.Spec("#count", Loc.T("Quantidade", "Count")),
                    },
                    new[] { (SharedParams.Sistema, ScheduleFilterType.Equal, systemId) },
                    new[] { SharedParams.Grupo }, itemized: false, report: ctx.Report);
                ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
            }
            ctx.Report.Created(sink.Created.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });
        ctx.Report.Tables.Add(r.Quantities());
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<BriseOptions>(optionsJson) ?? new BriseOptions();
        var c = Json.Deserialize<Context>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "gen.brise");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}
