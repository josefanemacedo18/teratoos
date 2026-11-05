using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Colors;
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
    [Text("Vazios — furos recortados na superfície", "Voids — holes cut into the surface")] Void,
    [Text("Sólidos — relevo extrudado da superfície", "Solids — relief extruded from the surface")] Solid,
}

/// <summary>Opções do gerador de padrões (no estilo Pixy Tools).</summary>
public sealed class PatternOptions : IValidatableOptions
{
    [Opt("Padrão", "Pattern", Group = "Padrão|Pattern")]
    public PixyPattern Pattern { get; set; } = PixyPattern.Hexagonal;

    [Opt("Saída", "Output", Group = "Padrão|Pattern")]
    public PatternOutput Output { get; set; } = PatternOutput.Void;

    [Opt("Tamanho da célula (densidade)", "Cell size (density)", Group = "Padrão|Pattern", Kind = OptKind.Length, Min = 0.02,
        Tip = "Distância entre os centros das formas. Menor = mais denso.|Distance between shape centers. Smaller = denser.")]
    public double CellSize { get; set; } = U.Mm(300);

    [Opt("Proporção abertura/sólido", "Opening/solid ratio", Group = "Padrão|Pattern", Min = 0.05, Max = 0.95,
        Tip = "Quanto de cada célula a forma ocupa: 0,9 = aberturas grandes e nervuras finas.|How much of each cell the shape fills.")]
    public double OpeningRatio { get; set; } = 0.6;

    [Opt("Escala", "Scale", Group = "Padrão|Pattern", Min = 0.1, Max = 3)]
    public double Scale { get; set; } = 1;

    [Opt("Rotação", "Rotation", Group = "Padrão|Pattern", Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Pontas da estrela", "Star points", Group = "Padrão|Pattern", Min = 3, Max = 16, VisibleIf = "Pattern=StarBurst")]
    public int StarPoints { get; set; } = 5;

    [Opt("Raio interno da estrela (0,1–0,9)", "Star inner radius (0.1–0.9)", Group = "Padrão|Pattern", Min = 0.1, Max = 0.9, VisibleIf = "Pattern=StarBurst")]
    public double StarInner { get; set; } = 0.45;

    [Opt("Amplitude da onda (células)", "Wave amplitude (cells)", Group = "Padrão|Pattern", Min = 0, Max = 5, VisibleIf = "Pattern=Waves")]
    public double WaveAmplitude { get; set; } = 0.6;

    [Opt("Comprimento da onda (células)", "Wave length (cells)", Group = "Padrão|Pattern", Min = 1, Max = 100, VisibleIf = "Pattern=Waves")]
    public double WaveLength { get; set; } = 8;

    [Opt("Tamanho aleatório (0–1)", "Random size (0–1)", Group = "Aleatoriedade|Randomization", Min = 0, Max = 1)]
    public double RandomSize { get; set; }

    [Opt("Rotação aleatória (±)", "Random rotation (±)", Group = "Aleatoriedade|Randomization", Kind = OptKind.Angle, Min = 0, Max = 180)]
    public double RandomRotation { get; set; }

    [Opt("Posição aleatória (0–0,5)", "Random position (0–0.5)", Group = "Aleatoriedade|Randomization", Min = 0, Max = 0.5)]
    public double RandomPosition { get; set; }

    [Opt("Remover formas ao acaso (0–1)", "Randomly remove shapes (0–1)", Group = "Aleatoriedade|Randomization", Min = 0, Max = 0.95)]
    public double RandomSkip { get; set; }

    [Opt("Semente", "Seed", Group = "Aleatoriedade|Randomization", Min = 0, Max = 99999, Tip = "Mesma semente = mesmo resultado.|Same seed = same result.")]
    public int Seed { get; set; } = 1;

    [Opt("Efeito fade", "Fade effect", Group = "Fade e escala|Fade and scale")]
    public FadeMode Fade { get; set; } = FadeMode.None;

    [Opt("Direção do fade", "Fade direction", Group = "Fade e escala|Fade and scale", Kind = OptKind.Angle, VisibleIf = "Fade=Linear",
        Tip = "0° = da esquerda para a direita; 90° = de baixo para cima.|0° = left to right; 90° = bottom to top.")]
    public double FadeAngle { get; set; }

    [Opt("Tamanho no início do fade", "Size at fade start", Group = "Fade e escala|Fade and scale", Min = 0, Max = 2, VisibleIf = "Fade!=None")]
    public double FadeFrom { get; set; } = 1;

    [Opt("Tamanho no fim do fade", "Size at fade end", Group = "Fade e escala|Fade and scale", Min = 0, Max = 2, VisibleIf = "Fade!=None")]
    public double FadeTo { get; set; } = 0.15;

    [Opt("Centro horizontal (0–1)", "Center horizontal (0–1)", Group = "Fade e escala|Fade and scale", Min = 0, Max = 1, VisibleIf = "Fade=Radial|Pattern=StarBurst")]
    public double CenterU { get; set; } = 0.5;

    [Opt("Centro vertical (0–1)", "Center vertical (0–1)", Group = "Fade e escala|Fade and scale", Min = 0, Max = 1, VisibleIf = "Fade=Radial|Pattern=StarBurst")]
    public double CenterV { get; set; } = 0.5;

    [Opt("Espessura do relevo", "Relief thickness", Group = "Espessura|Thickness", Kind = OptKind.Length, Min = 0.002, VisibleIf = "Output=Solid")]
    public double Thickness { get; set; } = U.Mm(40);

    [Opt("Espessura aleatória (0–1)", "Random thickness (0–1)", Group = "Espessura|Thickness", Min = 0, Max = 1, VisibleIf = "Output=Solid")]
    public double RandomThickness { get; set; }

    [Opt("Espessura acompanha o fade", "Thickness follows the fade", Group = "Espessura|Thickness", VisibleIf = "Output=Solid")]
    public bool ThicknessFollowsFade { get; set; }

    [Opt("Afastamento da face", "Offset from face", Group = "Espessura|Thickness", Kind = OptKind.Length, VisibleIf = "Output=Solid")]
    public double FaceOffset { get; set; }

    [Opt("Profundidade do corte (0 = atravessa o elemento)", "Cut depth (0 = through the element)", Group = "Espessura|Thickness", Kind = OptKind.Length, Min = 0, VisibleIf = "Output=Void")]
    public double CutDepth { get; set; }

    [Opt("Margem das bordas", "Edge margin", Group = "Limites|Limits", Kind = OptKind.Length, Min = 0)]
    public double EdgeMargin { get; set; } = U.Mm(100);

    [Opt("Limite de formas", "Shape limit", Group = "Limites|Limits", Min = 10, Max = 20000,
        Tip = "Proteção de desempenho. Acima de ~3000 formas o Revit fica lento.|Performance guard. Above ~3000 shapes Revit gets slow.")]
    public int MaxShapes { get; set; } = 3000;

    [Opt("Material", "Material", Group = "Material|Material", Kind = OptKind.Material, VisibleIf = "Output=Solid")]
    public string Material { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (CellSize < U.Mm(20)) yield return Loc.T("Tamanho da célula muito pequeno (mínimo 20 mm).", "Cell size too small (minimum 20 mm).");
        if (Fade != FadeMode.None && FadeFrom <= 0 && FadeTo <= 0) yield return Loc.T("O fade deixaria todas as formas com tamanho zero.", "The fade would make all shapes zero size.");
    }

    public PixySettings ToCore() => new()
    {
        Pattern = Pattern, CellSize = CellSize, OpeningRatio = OpeningRatio, Scale = Scale, RotationDeg = Rotation,
        StarPoints = StarPoints, StarInnerRatio = StarInner, WaveAmplitude = WaveAmplitude, WaveLength = WaveLength,
        RandomSize = RandomSize, RandomRotationDeg = RandomRotation, RandomPosition = RandomPosition, RandomSkip = RandomSkip, Seed = Seed,
        Fade = Fade, FadeAngleDeg = FadeAngle, FadeFrom = FadeFrom, FadeTo = FadeTo, CenterU = CenterU, CenterV = CenterV, EdgeMargin = EdgeMargin,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.pattern", Modules.Generators, "Gerador de padrões", "Pattern generator", Order = 10, Large = true,
    DescPt = "Padrões paramétricos (hexagonal, Voronoi, star burst, círculos, losangos, triângulos, quadrados, ondas, tijolos) como furos reais ou relevo sólido em faces planas e curvas.",
    DescEn = "Parametric patterns (hexagonal, Voronoi, star burst, circles, diamonds, triangles, squares, waves, bricks) as real holes or solid relief on planar and curved faces.",
    HelpPt = "1. Clique na face de uma parede, piso, forro ou telhado (plana ou curva).\n2. Escolha o padrão e a saída: Vazios recortam furos reais no elemento; Sólidos criam relevo extrudado da face.\n3. Ajuste densidade, proporção abertura/sólido, escala, rotação, aleatoriedade e o efeito fade, olhando a pré-visualização 2D/3D.\n4. Confirme. O padrão vira uma família de Modelo genérico com as formas exatas; os vazios usam o corte nativo do Revit.\n\nPara mudar depois: Geradores ▸ Editar sistema gerado (as opções reabrem e o padrão é refeito no mesmo lugar).\n\nDica: em paredes, as janelas e portas são respeitadas — nenhuma forma é criada sobre elas.",
    HelpEn = "1. Click a wall, floor, ceiling or roof face (planar or curved).\n2. Choose the pattern and output: Voids cut real holes; Solids create relief.\n3. Adjust density, opening ratio, scale, rotation, randomization and fade, watching the 2D/3D preview.\n4. Confirm. The pattern becomes a Generic Model family with exact shapes; voids use Revit's native cut.\n\nTo change later: Generators ▸ Edit generated system.",
    Keywords = "padrao padroes hexagonal voronoi estrela star burst furos perfurado vazio relevo fachada pattern void solid perforated facade pixy")]
public sealed class PatternCommand : ToolCommand
{
    private const string ToolId = "gen.pattern";

    public override void Run(ToolContext ctx)
    {
        var c = GenHost.Acquire(ctx, SurfaceHost.PlanarFace);
        var o = ctx.LoadOptions<PatternOptions>();
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Gerar", "Generate"));
        Generate(ctx, o, c, GenSystem.NewId());
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = new PatternOptions();
        Json.PopulateObject(o, optionsJson);
        var c = Json.Deserialize<PlanarContext>(contextJson) ?? throw new ToolException(Loc.T("Registro do padrão inválido.", "Invalid pattern record."));
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Regenerar", "Regenerate"), key: ToolId);
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover padrão anterior", "Remove previous pattern"), () =>
            {
                GenSystem.Delete(ctx.Doc, systemId);
                PatternFamilyBuilder.DeleteFamily(ctx.Doc, FamilyName(systemId));
            });
            Generate(ctx, o, c, systemId);
        });
    }

    private static string FamilyName(string systemId) => "TT Padrão " + systemId;

    /// <summary>Domínio 2D da face e função que leva cada célula para 3D.</summary>
    private sealed class Surface
    {
        public Region2 Domain { get; init; } = null!;
        public Frame3? Planar { get; init; }
        public CurvedSurface? Curved { get; init; }

        public Frame3 FrameAt(Vec2 p) => Planar is { } f ? f : Curved!.FrameAt(p);

        /// <summary>Polígono no sistema local da célula (plano: o próprio; curvo: centrado no ponto da célula).</summary>
        public Polygon2 Local(PixyCell cell) => Planar != null ? cell.Shape : cell.Shape.Translate(new Vec2(-cell.Center.X, -cell.Center.Y));

        public Frame3 CellFrame(PixyCell cell) => Planar ?? Curved!.FrameAt(cell.Center);
    }

    private static Surface SurfaceOf(Document doc, PlanarContext c)
    {
        if (!c.Curved) return new Surface { Domain = c.GetRegion(), Planar = c.GetFrame() };
        var cs = GenHost.Curved(doc, c) ?? throw new ToolException(Loc.T("A face curva não existe mais.", "The curved face no longer exists."));
        return new Surface { Domain = cs.Domain, Curved = cs };
    }

    private static double HostThickness(Document doc, PlanarContext c)
    {
        var host = doc.GetElement(new ElementId(c.HostId));
        if (host is Wall w) return w.Width;
        if (host is HostObject ho && doc.GetElement(ho.GetTypeId()) is HostObjAttributes hoa && hoa.GetCompoundStructure() is { } cs) return cs.GetWidth();
        var bb = host == null ? null : RevitGeo.Box(host);
        return bb == null ? U.M(0.5) : Math.Max(U.Mm(50), Math.Min(bb.Max.X - bb.Min.X, Math.Min(bb.Max.Y - bb.Min.Y, bb.Max.Z - bb.Min.Z)));
    }

    private static List<PatternExtrusion> Extrusions(PatternOptions o, Surface s, List<PixyCell> cells, double hostThickness)
    {
        var list = new List<PatternExtrusion>(cells.Count);
        foreach (var cell in cells)
        {
            double z0, z1;
            if (o.Output == PatternOutput.Void)
            {
                var depth = o.CutDepth > 0 ? o.CutDepth : hostThickness + U.Mm(50);
                z0 = -depth;
                z1 = U.Mm(20);
            }
            else
            {
                var t = o.Thickness * (1 + o.RandomThickness * (cell.Random01 * 2 - 1));
                if (o.ThicknessFollowsFade) t *= Math.Clamp(cell.Factor, 0.05, 3);
                // Quantiza a espessura (agrupa extrusões iguais = família mais leve).
                t = Math.Max(U.Mm(1), Math.Round(t / U.Mm(2)) * U.Mm(2));
                z0 = o.FaceOffset;
                z1 = o.FaceOffset + t;
            }
            list.Add(new PatternExtrusion(s.CellFrame(cell), s.Local(cell), z0, z1));
        }
        return list;
    }

    private static PreviewModel Preview(ToolContext ctx, PatternOptions o, PlanarContext c)
    {
        var s = SurfaceOf(ctx.Doc, c);
        var cells = PixyPatternGenerator.Generate(o.ToCore(), s.Domain, Math.Min(o.MaxShapes, 6000));
        var pm = new PreviewModel();
        var isVoid = o.Output == PatternOutput.Void;
        pm.Region(s.Domain);
        var fill = isVoid ? "#2B2F36" : "#B5652B";
        foreach (var cell in cells) pm.Polygon(cell.Shape, fill, isVoid ? "#2B2F36" : "#6B4A33", 0.4);

        // 3D: face hospedeira + formas
        var sc = pm.S3;
        var color = isVoid ? "#2B2F36" : (Lookup.Material(ctx.Doc, o.Material) is { } m ? MaterialsService.ColorOf(m).Hex : "#B5652B");
        var thick = isVoid ? HostThickness(ctx.Doc, c) : 0;
        if (s.Planar is { } f)
        {
            if (isVoid) sc.Prism(f, s.Domain.Outer, -thick, thick, "#D9D4CC", 0.35);
            else sc.Face(f, s.Domain.Outer, "#D9D4CC", 0.45);
        }
        else
        {
            // superfície curva: malha de linhas
            var d = s.Domain.Bounds;
            for (var i = 0; i <= 12; i++)
            {
                var u = d.MinX + d.Width * i / 12;
                Vec3? prev = null;
                for (var j = 0; j <= 12; j++)
                {
                    var p = s.FrameAt(new Vec2(u, d.MinY + d.Height * j / 12)).Origin;
                    if (prev != null) sc.Line(prev.Value, p, "#8A9099");
                    prev = p;
                }
            }
        }
        foreach (var e in Extrusions(o, s, cells, thick))
            sc.Prism(e.Frame, e.Local, e.Z0, e.Z1 - e.Z0, color);

        var area = cells.Sum(x => x.Shape.Area);
        var pct = s.Domain.Area > 0 ? area / s.Domain.Area * 100 : 0;
        pm.Info = Loc.F("{0} formas · {1} ({2:0}% da face) {3}", "{0} shapes · {1} ({2:0}% of the face) {3}",
            cells.Count, U.FormatArea(area), pct, isVoid ? Loc.T("abertos", "open") : Loc.T("em relevo", "in relief"));
        if (cells.Count >= o.MaxShapes) pm.Info += Loc.T(" · limite de formas atingido", " · shape limit reached");
        return pm;
    }

    internal static void Generate(ToolContext ctx, PatternOptions o, PlanarContext c, string systemId)
    {
        var doc = ctx.Doc;
        var s = SurfaceOf(doc, c);
        var cells = PixyPatternGenerator.Generate(o.ToCore(), s.Domain, o.MaxShapes);
        if (cells.Count == 0) ToolContext.Fail("Nenhuma forma cabe na face com essas opções. Diminua o tamanho da célula ou a margem.", "No shape fits the face with these options. Reduce cell size or margin.");
        ctx.CheckCount(cells.Count, "formas", "shapes");
        var isVoid = o.Output == PatternOutput.Void;
        var host = doc.GetElement(new ElementId(c.HostId));
        if (isVoid && (host == null || !InstanceVoidCutUtils.CanBeCutWithVoid(host)))
            ToolContext.Fail("Este elemento não aceita corte por vazio (ex.: parede-cortina ou família). Use a saída \"Sólidos\" ou escolha uma parede, piso, forro ou telhado.",
                "This element can't be cut by voids. Use \"Solids\" output or pick a wall, floor, ceiling or roof.");
        var items = Extrusions(o, s, cells, HostThickness(doc, c));
        var near = s.FrameAt(s.Domain.Bounds.Center).Origin;
        var res = PatternFamilyBuilder.Build(ctx, FamilyName(systemId), items, isVoid, near);
        var created = res.Created;
        var failed = res.Failed;
        var reason = res.FirstError;
        List<GeometryObject>? freeForm = null;
        if (res.Instance == null)
        {
            // Vazios precisam de família (forma livre não recorta). O relevo vira forma livre com o mesmo desenho.
            if (isVoid)
                ToolContext.Fail(
                    $"Nenhum furo do padrão pôde ser criado. Motivo informado pelo Revit: {reason ?? "não informado"}. Tente uma célula maior ou uma proporção de abertura menor.",
                    $"No pattern hole could be created. Revit reported: {reason ?? "not given"}. Try a larger cell or a smaller opening ratio.");
            freeForm = PatternFamilyBuilder.Solids(doc, items, o.Material, out failed, out var solidError);
            reason = solidError ?? reason;
            if (freeForm.Count == 0)
                ToolContext.Fail(
                    $"Nenhuma forma do padrão pôde ser criada. Motivo informado pelo Revit: {reason ?? "não informado"}.",
                    $"No pattern shape could be created. Revit reported: {reason ?? "not given"}.");
            created = freeForm.Count;
            ctx.Report.Info(Loc.T("A família do padrão não aceitou as formas; o relevo foi criado como forma livre (edite por Geradores ▸ Editar sistema gerado).",
                "The pattern family rejected the shapes; the relief was created as free form (edit via Generators ▸ Edit generated system)."));
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            Element el = res.Instance != null
                ? res.Instance
                : Shapes.Create(doc, freeForm!, BuiltInCategory.OST_GenericModel, FamilyName(systemId));
            GenSystem.Mark(el, systemId, ToolId, 0);
            RevitGeo.Set(el, o.Pattern.ToString(), SharedParams.Codigo);
            if (isVoid)
            {
                InstanceVoidCutUtils.AddInstanceVoidCut(doc, host!, (FamilyInstance)el);
                ctx.Report.AddResult(host!.Id.Value);
            }
            else if (res.Instance != null && !string.IsNullOrWhiteSpace(o.Material) && Lookup.Material(doc, o.Material) is { } mat)
            {
                res.Instance.Symbol.LookupParameter(PatternFamilyBuilder.MaterialParam)?.Set(mat.Id);
            }
            GenSystem.Register(doc, systemId, ToolId, o, c, Loc.T("Padrão ", "Pattern ") + o.Pattern);
            ctx.Report.Created();
            ctx.Report.AddResult(el.Id.Value);
        });
        ctx.Report.Count(Loc.T("Formas", "Shapes"), created);
        ctx.Report.ItemsFailed(Loc.T("Formas do padrão", "Pattern shapes"), failed, reason ?? Loc.T("forma pequena demais", "shape too small"));
        ctx.Report.Info(Loc.T("Para alterar o padrão: Geradores ▸ Editar sistema gerado.", "To change the pattern: Generators ▸ Edit generated system."));
    }
}
