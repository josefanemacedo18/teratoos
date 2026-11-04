using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Patterns;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public enum PatternOutput
{
    [Text("Família paramétrica TT_Bloco (BIM editável)", "TT_Bloco parametric family (editable BIM)")] Parametric,
    [Text("Forma livre (formas exatas, leve)", "Free form (exact shapes, light)")] FreeForm,
    [Text("Família/tipo escolhido no projeto", "Family/type chosen in the project")] UserFamily,
    [Text("Perfurações no hospedeiro (aberturas)", "Perforations in the host (openings)")] Perforation,
}

public enum MaterialMode
{
    [Text("Um material", "Single material")] Single,
    [Text("Dois materiais pelo valor do gradiente", "Two materials by gradient value")] ByValue,
    [Text("Alternar (xadrez)", "Alternate (checker)")] Alternate,
}

/// <summary>Opções do gerador de padrões (mapeiam para <see cref="PatternSettings"/>).</summary>
public sealed class PatternOptions : IValidatableOptions
{
    /// <summary>Superfície escolhida antes do diálogo (guardada para regenerar).</summary>
    public SurfaceHost Host { get; set; } = SurfaceHost.PlanarFace;

    [Opt("Saída", "Output", Group = "Superfície|Surface")]
    public PatternOutput Output { get; set; } = PatternOutput.Parametric;

    [Opt("Família/tipo", "Family/type", Group = "Superfície|Surface", Kind = OptKind.FamilyType, Filter = "OST_GenericModel,OST_Furniture,OST_SpecialityEquipment,OST_LightingFixtures,OST_Casework,OST_Planting", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";

    [Opt("Distribuição", "Layout", Group = "Distribuição|Layout")]
    public PatternLayout Layout { get; set; } = PatternLayout.Rectangular;

    [Opt("Forma do módulo", "Module shape", Group = "Distribuição|Layout")]
    public ModuleShape Shape { get; set; } = ModuleShape.Rectangle;

    [Opt("Espaçamento X", "Spacing X", Group = "Distribuição|Layout", Kind = OptKind.Length, Min = 0.01)]
    public double SpacingX { get; set; } = U.Mm(300);

    [Opt("Espaçamento Y", "Spacing Y", Group = "Distribuição|Layout", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Layout!=Radial,Spiral,Random,Linear")]
    public double SpacingY { get; set; } = U.Mm(300);

    [Opt("Largura do módulo", "Module width", Group = "Módulo|Module", Kind = OptKind.Length, Min = 0.001)]
    public double ModuleWidth { get; set; } = U.Mm(250);

    [Opt("Altura do módulo", "Module height", Group = "Módulo|Module", Kind = OptKind.Length, Min = 0.001)]
    public double ModuleHeight { get; set; } = U.Mm(250);

    [Opt("Profundidade / relevo", "Depth / relief", Group = "Módulo|Module", Kind = OptKind.Length, Min = 0.001)]
    public double Depth { get; set; } = U.Mm(20);

    [Opt("Afastamento da superfície", "Offset from surface", Group = "Módulo|Module", Kind = OptKind.Length)]
    public double SurfaceOffset { get; set; }

    [Opt("Rotação da grade", "Grid rotation", Group = "Orientação|Orientation", Kind = OptKind.Angle)]
    public double GridRotation { get; set; }

    [Opt("Rotação de cada módulo", "Module rotation", Group = "Orientação|Orientation", Kind = OptKind.Angle)]
    public double ModuleRotation { get; set; }

    [Opt("Ponto inicial", "Start point", Group = "Orientação|Orientation")]
    public PatternAnchor Anchor { get; set; } = PatternAnchor.RegionCenter;

    [Opt("Deslocamento X", "Offset X", Group = "Orientação|Orientation", Kind = OptKind.Length)]
    public double OffsetX { get; set; }

    [Opt("Deslocamento Y", "Offset Y", Group = "Orientação|Orientation", Kind = OptKind.Length)]
    public double OffsetY { get; set; }

    [Opt("Anéis", "Rings", Group = "Radial|Radial", Min = 1, Max = 500, VisibleIf = "Layout=Radial")]
    public int Rings { get; set; } = 6;

    [Opt("Espaçamento entre anéis", "Ring spacing", Group = "Radial|Radial", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Layout=Radial")]
    public double RingSpacing { get; set; } = U.Mm(300);

    [Opt("Raio inicial", "Inner radius", Group = "Radial|Radial", Kind = OptKind.Length, Min = 0, VisibleIf = "Layout=Radial")]
    public double InnerRadius { get; set; }

    [Opt("Módulos por anel (0 = automático)", "Modules per ring (0 = auto)", Group = "Radial|Radial", Min = 0, Max = 5000, VisibleIf = "Layout=Radial")]
    public int PerRing { get; set; }

    [Opt("Ângulo total", "Sweep angle", Group = "Radial|Radial", Kind = OptKind.Angle, Min = 1, Max = 360, VisibleIf = "Layout=Radial")]
    public double Sweep { get; set; } = 360;

    [Opt("Alinhar tangente", "Align tangent", Group = "Radial|Radial", VisibleIf = "Layout=Radial,Spiral")]
    public bool AlignTangent { get; set; } = true;

    [Opt("Quantidade na espiral", "Spiral count", Group = "Radial|Radial", Min = 1, Max = 100000, VisibleIf = "Layout=Spiral")]
    public int SpiralCount { get; set; } = 400;

    [Opt("Gradiente", "Gradient", Group = "Gradiente e variação|Gradient and variation")]
    public GradientMode Gradient { get; set; } = GradientMode.None;

    [Opt("Ângulo do gradiente", "Gradient angle", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Angle, VisibleIf = "Gradient=Linear")]
    public double GradientAngle { get; set; }

    [Opt("Atrator X (a partir do canto)", "Attractor X (from corner)", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, VisibleIf = "Gradient=Radial,Formula")]
    public double AttractorX { get; set; } = U.M(1);

    [Opt("Atrator Y (a partir do canto)", "Attractor Y (from corner)", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, VisibleIf = "Gradient=Radial,Formula")]
    public double AttractorY { get; set; } = U.M(1);

    [Opt("Raio do atrator", "Attractor radius", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Gradient=Radial")]
    public double AttractorRadius { get; set; } = U.M(3);

    [Opt("Escala do ruído", "Noise scale", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Gradient=Noise")]
    public double NoiseScale { get; set; } = U.M(2);

    [Opt("Fórmula (0 a 1)", "Formula (0 to 1)", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Formula, VisibleIf = "Gradient=Formula",
        Tip = "Variáveis: x, y (m), u, v (0–1), i, j, n, d (dist. ao atrator, m), r (aleatório), w, h. Ex.: 0.5+0.5*sin(x*90)|Variables: x, y (m), u, v (0–1), i, j, n, d, r, w, h.")]
    public string Formula { get; set; } = "u";

    [Opt("Imagem do gradiente", "Gradient image", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.File, Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif", VisibleIf = "Gradient=Image")]
    public string ImagePath { get; set; } = "";

    [Opt("Inverter gradiente", "Invert gradient", Group = "Gradiente e variação|Gradient and variation", VisibleIf = "Gradient!=None")]
    public bool Invert { get; set; }

    [Opt("Gradiente controla a escala", "Gradient drives scale", Group = "Gradiente e variação|Gradient and variation", VisibleIf = "Gradient!=None")]
    public bool ApplyScale { get; set; } = true;

    [Opt("Escala mínima", "Minimum scale", Group = "Gradiente e variação|Gradient and variation", Min = 0, Max = 10, VisibleIf = "ApplyScale&Gradient!=None")]
    public double ScaleMin { get; set; } = 0.3;

    [Opt("Escala máxima", "Maximum scale", Group = "Gradiente e variação|Gradient and variation", Min = 0, Max = 10, VisibleIf = "ApplyScale&Gradient!=None")]
    public double ScaleMax { get; set; } = 1.0;

    [Opt("Gradiente controla a profundidade", "Gradient drives depth", Group = "Gradiente e variação|Gradient and variation", VisibleIf = "Gradient!=None")]
    public bool ApplyDepth { get; set; }

    [Opt("Profundidade mínima", "Minimum depth", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, Min = 0.001, VisibleIf = "ApplyDepth&Gradient!=None")]
    public double DepthMin { get; set; } = U.Mm(10);

    [Opt("Profundidade máxima", "Maximum depth", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Length, Min = 0.001, VisibleIf = "ApplyDepth&Gradient!=None")]
    public double DepthMax { get; set; } = U.Mm(120);

    [Opt("Gradiente controla a rotação", "Gradient drives rotation", Group = "Gradiente e variação|Gradient and variation", VisibleIf = "Gradient!=None")]
    public bool ApplyRotation { get; set; }

    [Opt("Rotação máxima", "Maximum rotation", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Angle, VisibleIf = "ApplyRotation&Gradient!=None")]
    public double RotationMax { get; set; } = 90;

    [Opt("Gradiente controla a densidade", "Gradient drives density", Group = "Gradiente e variação|Gradient and variation", VisibleIf = "Gradient!=None")]
    public bool ApplyDensity { get; set; }

    [Opt("Variação aleatória de posição (0–0,5)", "Random position jitter (0–0.5)", Group = "Gradiente e variação|Gradient and variation", Min = 0, Max = 0.5)]
    public double JitterPosition { get; set; }

    [Opt("Variação aleatória de rotação (±)", "Random rotation jitter (±)", Group = "Gradiente e variação|Gradient and variation", Kind = OptKind.Angle, Min = 0, Max = 180)]
    public double JitterRotation { get; set; }

    [Opt("Variação aleatória de escala (0–0,9)", "Random scale jitter (0–0.9)", Group = "Gradiente e variação|Gradient and variation", Min = 0, Max = 0.9)]
    public double JitterScale { get; set; }

    [Opt("Remover módulos ao acaso (0–1)", "Randomly remove modules (0–1)", Group = "Gradiente e variação|Gradient and variation", Min = 0, Max = 1)]
    public double RemoveProbability { get; set; }

    [Opt("Semente", "Seed", Group = "Gradiente e variação|Gradient and variation", Tip = "Mesma semente = mesmo resultado.|Same seed = same result.")]
    public int Seed { get; set; } = 1;

    [Opt("Recorte", "Clipping", Group = "Limites|Limits")]
    public ClipMode Clip { get; set; } = ClipMode.FullyInside;

    [Opt("Margem das bordas", "Edge margin", Group = "Limites|Limits", Kind = OptKind.Length, Min = 0)]
    public double EdgeMargin { get; set; }

    [Opt("Modo de material", "Material mode", Group = "Materiais|Materials", VisibleIf = "Output!=Perforation")]
    public MaterialMode MaterialMode { get; set; } = MaterialMode.Single;

    [Opt("Material A", "Material A", Group = "Materiais|Materials", Kind = OptKind.Material, VisibleIf = "Output!=Perforation")]
    public string MaterialA { get; set; } = "";

    [Opt("Material B", "Material B", Group = "Materiais|Materials", Kind = OptKind.Material, VisibleIf = "MaterialMode!=Single&Output!=Perforation")]
    public string MaterialB { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (Output == PatternOutput.Perforation && (Host == SurfaceHost.Rectangle || Host == SurfaceHost.Room))
            yield return Loc.T("Perfurações exigem uma face de um elemento hospedeiro (piso, forro, telhado ou parede).", "Perforations require a face of a host element (floor, ceiling, roof or wall).");
        if (Output == PatternOutput.UserFamily && string.IsNullOrWhiteSpace(UserFamily))
            yield return Loc.T("Escolha a família/tipo.", "Choose the family/type.");
        if (Gradient == GradientMode.Image && string.IsNullOrWhiteSpace(ImagePath))
            yield return Loc.T("Escolha a imagem do gradiente.", "Choose the gradient image.");
    }

    public PatternSettings ToCore() => new()
    {
        Layout = Layout,
        Shape = Shape,
        SpacingX = SpacingX,
        SpacingY = SpacingY,
        ModuleWidth = ModuleWidth,
        ModuleHeight = ModuleHeight,
        Depth = Depth,
        GridRotationDeg = GridRotation,
        ModuleRotationDeg = ModuleRotation,
        Anchor = Anchor,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        RadialRings = Rings,
        RingSpacing = RingSpacing,
        InnerRadius = InnerRadius,
        ModulesPerRing = PerRing,
        SweepAngleDeg = Sweep,
        AlignTangent = AlignTangent,
        SpiralCount = SpiralCount,
        Seed = Seed,
        JitterPosition = JitterPosition,
        JitterRotationDeg = JitterRotation,
        JitterScale = JitterScale,
        RemoveProbability = RemoveProbability,
        Gradient = Gradient,
        GradientAngleDeg = GradientAngle,
        AttractorX = AttractorX,
        AttractorY = AttractorY,
        AttractorRadius = AttractorRadius,
        NoiseScale = NoiseScale,
        Formula = Formula,
        InvertGradient = Invert,
        ApplyToScale = ApplyScale && Gradient != GradientMode.None,
        ScaleMin = ScaleMin,
        ScaleMax = ScaleMax,
        ApplyToDepth = ApplyDepth && Gradient != GradientMode.None,
        DepthMin = DepthMin,
        DepthMax = DepthMax,
        ApplyToRotation = ApplyRotation && Gradient != GradientMode.None,
        RotationMinDeg = 0,
        RotationMaxDeg = RotationMax,
        ApplyToDensity = ApplyDensity && Gradient != GradientMode.None,
        Clip = Clip,
        EdgeMargin = EdgeMargin,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.pattern", Modules.Generators, "Gerador de padrões", "Pattern generator", Order = 10, Large = true,
    DescPt = "Padrões lineares, retangulares, triangulares, hexagonais, radiais, espirais e aleatórios em faces planas/curvas, com gradientes, atratores, relevo e perfurações.",
    DescEn = "Linear, rectangular, triangular, hexagonal, radial, spiral and random patterns on planar/curved faces, with gradients, attractors, relief and perforations.",
    HelpPt = "1. Escolha onde aplicar (face plana, face curva, retângulo ou ambiente) e a saída.\n2. Ajuste distribuição, módulo e gradiente vendo a pré-visualização.\n3. Clique em Gerar.\n\n• Família paramétrica: cada módulo é um TT_Bloco editável (largura, profundidade, altura e material por instância).\n• Forma livre: formas exatas (círculos, hexágonos...) num elemento leve por material.\n• Perfurações: abre furos no piso/forro/telhado (formas exatas) ou na parede (retângulos).\n• Para editar depois: Geradores › Editar sistema gerado.\n\nLimitação: em faces curvas os módulos ficam tangentes à superfície no centro de cada célula; o espaçamento é medido sobre a superfície desenrolada (aproximado).",
    HelpEn = "1. Choose where to apply and the output.\n2. Adjust layout, module and gradient with the preview.\n3. Click Generate.\n\nTo edit later: Generators › Edit generated system.\n\nLimitation: on curved faces modules are tangent to the surface at each cell center; spacing is measured on the unrolled surface (approximate).",
    Keywords = "padrao pattern hexagonal triangular radial perfuracao brise painel modulo gradiente atrator pixy")]
public sealed class PatternCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var first = ctx.LoadOptions<PatternOptions>();
        first.Host = PickHost(ctx);
        var c = GenHost.Acquire(ctx, first.Host);
        var o = ctx.Ask(first, preview: x => Preview(ctx, x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    internal static SurfaceHost PickHost(ToolContext ctx)
    {
        var options = new[]
        {
            Loc.T("Face plana (parede, piso, qualquer elemento)", "Planar face (wall, floor, any element)"),
            Loc.T("Face curva", "Curved face"),
            Loc.T("Retângulo por dois pontos", "Rectangle by two points"),
            Loc.T("Ambiente", "Room"),
        };
        var i = Msg.Choose(Loc.T("Onde aplicar o padrão?", "Where to apply the pattern?"), ctx.Tool.Name, options);
        if (i < 0) ctx.Cancel();
        return (SurfaceHost)i;
    }

    internal static List<PatternCell> Cells(ToolContext ctx, PatternOptions o, PlanarContext c, out Region2 region, out CurvedSurface? curved)
    {
        curved = GenHost.Curved(ctx.Doc, c);
        region = curved?.Domain ?? c.GetRegion();
        var image = o.Gradient == GradientMode.Image && !string.IsNullOrWhiteSpace(o.ImagePath) ? ImageLoader.BrightnessSampler(o.ImagePath) : null;
        var cells = PatternGenerator.Generate(o.ToCore(), region, image, ctx.Settings.ElementHardLimit + 1);
        if (curved != null)
        {
            var cs = curved;
            cells = cells.Where(cell => cs.IsInside(cell.Center)).ToList();
        }
        return cells;
    }

    private static PreviewModel Preview(ToolContext ctx, PatternOptions o, PlanarContext c)
    {
        var cells = Cells(ctx, o, c, out var region, out var curved);
        var pm = new PreviewModel();
        pm.Region(region);
        foreach (var cell in cells.Take(6000)) pm.Polygon(cell.Outline, GenHost.Gradient(cell.Value), "#6B4A33", 0.5);
        var (n, smin, smax) = PatternGenerator.Summary(cells, o.ToCore());
        pm.Info = Loc.F("{0} módulos · escala {1:0.00}–{2:0.00} · área {3}", "{0} modules · scale {1:0.00}–{2:0.00} · area {3}", n, smin, smax, U.FormatArea(region.Area))
                  + (curved != null ? Loc.T(" · superfície desenrolada", " · unrolled surface") : "")
                  + (cells.Count > 6000 ? Loc.T(" (pré-visualização parcial)", " (partial preview)") : "");
        return pm;
    }

    internal static void Generate(ToolContext ctx, PatternOptions o, PlanarContext c, string systemId)
    {
        var doc = ctx.Doc;
        var cells = Cells(ctx, o, c, out _, out var curved);
        ctx.CheckCount(cells.Count, "módulos", "modules");
        var output = o.Output;
        if (output == PatternOutput.Parametric && o.Shape != ModuleShape.Rectangle)
        {
            ctx.Report.Info(Loc.T("Módulos não retangulares foram gerados como forma livre (formas exatas).", "Non-rectangular modules were generated as free form (exact shapes)."));
            output = PatternOutput.FreeForm;
        }

        if (output == PatternOutput.Perforation)
        {
            Perforate(ctx, o, c, cells);
            return;
        }

        var sink = new BoxSink(ctx, output switch
        {
            PatternOutput.FreeForm => GenOutput.FreeForm,
            PatternOutput.UserFamily => GenOutput.UserFamily,
            _ => GenOutput.Parametric,
        }, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Padrão" };
        sink.Prepare();
        var frame = curved == null ? c.GetFrame() : Frame3.World;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var i = 0;
            foreach (var cell in cells)
            {
                var mat = o.MaterialMode switch
                {
                    MaterialMode.ByValue => cell.Value >= 0.5 ? o.MaterialB : o.MaterialA,
                    MaterialMode.Alternate => ((cell.I + cell.J) & 1) == 0 ? o.MaterialA : o.MaterialB,
                    _ => o.MaterialA,
                };
                if (string.IsNullOrWhiteSpace(mat)) mat = null;
                var f = curved?.FrameAt(cell.Center) ?? frame;
                var center = curved != null ? Vec2.Zero : cell.Center;
                if (sink.Output == GenOutput.FreeForm && o.Shape != ModuleShape.Rectangle)
                {
                    var local = cell.Outline.Select(p => p - cell.Center + center).ToList();
                    var pts = local.Select(p => f.ToWorld(p.X, p.Y, o.SurfaceOffset).ToXYZ()).ToList();
                    try
                    {
                        var loop = new CurveLoop();
                        for (var k = 0; k < pts.Count; k++)
                            if (pts[k].DistanceTo(pts[(k + 1) % pts.Count]) > Tolerance.ShortCurve)
                                loop.Append(Line.CreateBound(pts[k], pts[(k + 1) % pts.Count]));
                        var solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, f.ZAxis.ToXYZ(), cell.Depth, Shapes.Options(doc, mat));
                        sink.AddSolid(solid, mat);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Prisma ignorado: " + ex.Message);
                    }
                    i++;
                    continue;
                }
                var box = GenHost.CellBox(f, center, cell.RotationRad, cell.Width, cell.Height, cell.Depth, o.SurfaceOffset);
                try
                {
                    sink.Add(box, mat, i);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("Módulo {0}: {1}", "Module {0}: {1}", i, ex.Message));
                }
                i++;
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, "gen.pattern", o, c, Loc.T("Padrão", "Pattern"));
            ctx.Report.Created(sink.Created.Count);
            ctx.Report.Count(Loc.T("Módulos", "Modules"), cells.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });
    }

    private static void Perforate(ToolContext ctx, PatternOptions o, PlanarContext c, List<PatternCell> cells)
    {
        var doc = ctx.Doc;
        var host = doc.GetElement(new ElementId(c.HostId));
        if (host is not (Floor or Ceiling or RoofBase or Wall))
            throw new ToolException(Loc.T("Perfurações só funcionam em pisos, forros, telhados e paredes.", "Perforations only work on floors, ceilings, roofs and walls."));
        if (c.Curved) throw new ToolException(Loc.T("Perfurações exigem uma face plana.", "Perforations require a planar face."));
        var f = c.GetFrame();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var ok = 0;
            foreach (var cell in cells)
            {
                try
                {
                    if (host is Wall wall)
                    {
                        var b = cell.Outline.Bounds;
                        var p1 = f.ToWorld(b.MinX, b.MinY, 0).ToXYZ();
                        var p2 = f.ToWorld(b.MaxX, b.MaxY, 0).ToXYZ();
                        doc.Create.NewOpening(wall, p1, p2);
                    }
                    else
                    {
                        var arr = new CurveArray();
                        var pts = cell.Outline.Select(p => f.ToWorld(p.X, p.Y, 0).ToXYZ()).ToList();
                        if (cell.Shape == ModuleShape.Circle)
                        {
                            var cc = f.ToWorld(cell.Center.X, cell.Center.Y, 0).ToXYZ();
                            var r = cell.Width / 2;
                            arr.Append(Arc.Create(cc, r, 0, Math.PI, f.XAxis.ToXYZ(), f.YAxis.ToXYZ()));
                            arr.Append(Arc.Create(cc, r, Math.PI, 2 * Math.PI, f.XAxis.ToXYZ(), f.YAxis.ToXYZ()));
                        }
                        else
                        {
                            for (var k = 0; k < pts.Count; k++)
                                if (pts[k].DistanceTo(pts[(k + 1) % pts.Count]) > Tolerance.ShortCurve)
                                    arr.Append(Line.CreateBound(pts[k], pts[(k + 1) % pts.Count]));
                        }
                        doc.Create.NewOpening(host, arr, true);
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    Log.Debug("Furo ignorado: " + ex.Message);
                }
            }
            ctx.Report.Count(Loc.T("Perfurações", "Perforations"), ok);
            if (ok < cells.Count)
                ctx.Report.Warn(Loc.F("{0} perfuração(ões) não puderam ser criadas (sobreposição ou fora do hospedeiro).", "{0} perforation(s) could not be created (overlap or outside the host).", cells.Count - ok));
            if (host is Wall && o.Shape != ModuleShape.Rectangle)
                ctx.Report.Info(Loc.T("Em paredes, o Revit só cria aberturas retangulares: formas foram aproximadas pelo retângulo envolvente.", "In walls Revit only creates rectangular openings: shapes were approximated by their bounding rectangle."));
            ctx.Report.AddResult(host.Id.Value);
        });
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<PatternOptions>(optionsJson) ?? new PatternOptions();
        var c = Json.Deserialize<PlanarContext>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "gen.pattern");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}
