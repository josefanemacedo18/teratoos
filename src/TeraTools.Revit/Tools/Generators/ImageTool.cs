using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Imaging;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public enum ImagePlacement
{
    [Text("Horizontal (piso/mesa) a partir de um ponto", "Horizontal (floor/table) from a point")] Horizontal,
    [Text("Sobre uma face plana (parede/painel)", "On a planar face (wall/panel)")] Face,
}

public sealed class ImageOptions : IValidatableOptions
{
    [Opt("Imagem", "Image", Group = "Imagem|Image", Kind = OptKind.File, Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif", Required = true)]
    public string ImagePath { get; set; } = "";

    [Opt("Colunas (resolução)", "Columns (resolution)", Group = "Imagem|Image", Min = 2, Max = 2000)]
    public int Columns { get; set; } = 40;

    [Opt("Largura total", "Total width", Group = "Imagem|Image", Kind = OptKind.Length, Min = 0.01)]
    public double TotalWidth { get; set; } = U.M(2);

    [Opt("Junta entre blocos", "Gap between blocks", Group = "Imagem|Image", Kind = OptKind.Length, Min = 0)]
    public double Gap { get; set; } = U.Mm(5);

    [Opt("Limite de blocos", "Block limit", Group = "Imagem|Image", Min = 10, Max = 200000, Tip = "Acima disso a resolução é reduzida automaticamente.|Above this the resolution is reduced automatically.")]
    public int MaxElements { get; set; } = 3000;

    [Opt("Brilho (−1 a 1)", "Brightness (−1 to 1)", Group = "Tons|Tones", Min = -1, Max = 1)]
    public double Brightness { get; set; }

    [Opt("Contraste (−1 a 3)", "Contrast (−1 to 3)", Group = "Tons|Tones", Min = -1, Max = 3)]
    public double Contrast { get; set; }

    [Opt("Gama", "Gamma", Group = "Tons|Tones", Min = 0.1, Max = 5)]
    public double Gamma { get; set; } = 1;

    [Opt("Inverter", "Invert", Group = "Tons|Tones")]
    public bool Invert { get; set; }

    [Opt("Ignorar fundo mais claro que (0–1)", "Skip background brighter than (0–1)", Group = "Tons|Tones", Min = 0, Max = 1, Tip = "1 = não ignora nada.|1 = skip nothing.")]
    public double SkipBrighterThan { get; set; } = 1;

    [Opt("Relevo", "Relief", Group = "Relevo|Relief")]
    public HeightSource HeightSource { get; set; } = HeightSource.Brightness;

    [Opt("Altura mínima", "Minimum height", Group = "Relevo|Relief", Kind = OptKind.Length, Min = 0.001)]
    public double MinHeight { get; set; } = U.Mm(10);

    [Opt("Altura máxima", "Maximum height", Group = "Relevo|Relief", Kind = OptKind.Length, Min = 0.001, VisibleIf = "HeightSource!=Flat")]
    public double MaxHeight { get; set; } = U.Mm(80);

    [Opt("Níveis de altura (0 = contínuo)", "Height levels (0 = continuous)", Group = "Relevo|Relief", Min = 0, Max = 64, VisibleIf = "HeightSource!=Flat")]
    public int HeightLevels { get; set; } = 6;

    [Opt("Cores → materiais", "Colors → materials", Group = "Cores|Colors")]
    public BlockColorMode ColorMode { get; set; } = BlockColorMode.AutoPalette;

    [Opt("Quantidade de cores", "Number of colors", Group = "Cores|Colors", Min = 1, Max = 32, VisibleIf = "ColorMode=AutoPalette,GrayLevels")]
    public int PaletteSize { get; set; } = 4;

    [Opt("Cores definidas (uma por linha, #RRGGBB)", "Defined colors (one per line, #RRGGBB)", Group = "Cores|Colors", Kind = OptKind.MultiLine, VisibleIf = "ColorMode=FixedPalette")]
    public List<string> FixedPalette { get; set; } = new() { "#2B2B2B", "#7A7A7A", "#C8C8C8", "#F2F2F2" };

    [Opt("Limiar (0–1)", "Threshold (0–1)", Group = "Cores|Colors", Min = 0, Max = 1, VisibleIf = "ColorMode=Threshold")]
    public double Threshold { get; set; } = 0.5;

    [Opt("Material único", "Single material", Group = "Cores|Colors", Kind = OptKind.Material, VisibleIf = "ColorMode=Single")]
    public string SingleMaterial { get; set; } = "";

    [Opt("Prefixo dos materiais criados", "Created materials prefix", Group = "Cores|Colors", VisibleIf = "ColorMode!=Single")]
    public string MaterialPrefix { get; set; } = "TT Imagem";

    [Opt("Posição", "Placement", Group = "Saída|Output")]
    public ImagePlacement Placement { get; set; } = ImagePlacement.Horizontal;

    [Opt("Saída", "Output", Group = "Saída|Output")]
    public GenOutput Output { get; set; } = GenOutput.Parametric;

    [Opt("Família/tipo", "Family/type", Group = "Saída|Output", Kind = OptKind.FamilyType, Filter = "OST_GenericModel,OST_Casework,OST_Furniture,OST_SpecialityEquipment", VisibleIf = "Output=UserFamily")]
    public string UserFamily { get; set; } = "";

    public IEnumerable<string> Validate()
    {
        if (!string.IsNullOrWhiteSpace(ImagePath) && !File.Exists(ImagePath))
            yield return Loc.T("Arquivo de imagem não encontrado.", "Image file not found.");
        if (MaxHeight < MinHeight && HeightSource != HeightSource.Flat)
            yield return Loc.T("A altura máxima deve ser maior que a mínima.", "Maximum height must be greater than minimum.");
    }

    public ImageBlockSettings ToCore() => new()
    {
        Columns = Columns,
        TotalWidth = TotalWidth,
        Gap = Gap,
        HeightSource = HeightSource,
        MinHeight = MinHeight,
        MaxHeight = MaxHeight,
        HeightLevels = HeightLevels,
        Brightness = Brightness,
        Contrast = Contrast,
        Gamma = Gamma,
        Invert = Invert,
        ColorMode = ColorMode,
        PaletteSize = PaletteSize,
        FixedPalette = FixedPalette,
        Threshold = Threshold,
        SkipBrighterThan = SkipBrighterThan,
        MaxElements = MaxElements,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.image", Modules.Generators, "Imagem para blocos", "Image to blocks", Order = 20, Large = true,
    DescPt = "Converte uma imagem em blocos (mosaicos, relevos e painéis), com altura por tom e cores mapeadas para materiais.",
    DescEn = "Converts an image into blocks (mosaics, reliefs and panels), with height by tone and colors mapped to materials.",
    HelpPt = "A imagem é dividida em células; cada célula vira um bloco com altura pelo brilho e material pela cor.\n\n• Níveis de altura reduzem variações (mais fácil de fabricar).\n• Paleta automática agrupa as cores (k-means) e cria um material por cor.\n• O limite de blocos protege o desempenho do modelo: a resolução é reduzida se necessário.",
    HelpEn = "The image is divided into cells; each becomes a block with height by brightness and material by color.\n\n• Height levels reduce variations (easier to fabricate).\n• Automatic palette groups colors (k-means) and creates one material per color.\n• The block limit protects model performance.",
    Keywords = "imagem foto mosaico relevo pixel blocos image mosaic relief pixels")]
public sealed class ImageCommand : ToolCommand
{
    public sealed class Context
    {
        public double[] Frame { get; set; } = new double[12];
    }

    public override void Run(ToolContext ctx)
    {
        var o = ctx.LoadOptions<ImageOptions>();
        o = ctx.Ask(o, preview: Preview, okText: Loc.T("Continuar", "Continue"));
        Frame3 frame;
        if (o.Placement == ImagePlacement.Face)
        {
            var r = ctx.PickFace("Escolha a face plana onde o painel será aplicado", "Pick the planar face for the panel");
            var el = ctx.Doc.GetElement(r);
            if (el.GetGeometryObjectFromReference(r) is not PlanarFace pf) throw new ToolException(Loc.T("A face precisa ser plana.", "The face must be planar."));
            var (_, f) = GenHost.PlanarFaceRegion(pf, GenHost.TransformOf(el));
            var p = ctx.PickPoint("Clique no canto inferior esquerdo do painel (sobre a face)", "Click the bottom-left corner of the panel (on the face)");
            var local = f.ToLocal(p.ToVec3());
            frame = f with { Origin = f.ToWorld(local.X, local.Y, 0) };
        }
        else
        {
            var p = ctx.PickPoint("Clique no canto inferior esquerdo do mosaico", "Click the bottom-left corner of the mosaic");
            frame = new Frame3(p.ToVec3(), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
        }
        var c = new Context { Frame = PlanarContext.From(frame, new Region2(Polygon2.Rectangle(Vec2.Zero, 1, 1))).Frame };
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static PreviewModel? Preview(ImageOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.ImagePath) || !File.Exists(o.ImagePath)) return null;
        var r = ImageToBlocks.Generate(ImageLoader.Load(o.ImagePath), o.ToCore());
        var pm = new PreviewModel();
        var palette = r.Palette;
        foreach (var b in r.Blocks.Take(12000))
        {
            var fill = o.ColorMode == BlockColorMode.Single ? GenHost.Gradient(b.Value) : palette[Math.Clamp(b.PaletteIndex, 0, palette.Count - 1)].Hex;
            pm.Polygon(Polygon2.Rectangle(b.Center, b.Size, b.Size, 0), fill, fill, 0.2);
        }
        // 3D: blocos numa fachada vertical (X = largura, Z = altura, Y = relevo)
        var sc = pm.S3;
        var wall = new Frame3(Vec3.Zero, Vec3.UnitX, Vec3.UnitZ, -Vec3.UnitY);
        sc.Face(wall, Polygon2.Rectangle(Vec2.Zero, r.TotalWidth, r.TotalHeight), "#D9D4CC", 0.3);
        foreach (var b in r.Blocks.Take(20000))
        {
            var fill = o.ColorMode == BlockColorMode.Single ? GenHost.Gradient(b.Value) : palette[Math.Clamp(b.PaletteIndex, 0, palette.Count - 1)].Hex;
            var origin = wall.ToWorld(b.Center.X - b.Size / 2, b.Center.Y - b.Size / 2, 0);
            sc.Box(new OrientedBox(new Frame3(origin, wall.XAxis, wall.YAxis, wall.ZAxis), b.Size, b.Size, Math.Max(b.Height, U.Mm(2))), fill);
        }
        pm.Info = Loc.F("{0} × {1} = {2} blocos · {3} cor(es) · {4} × {5}", "{0} × {1} = {2} blocks · {3} color(s) · {4} × {5}",
            r.Columns, r.Rows, r.Blocks.Count, palette.Count, U.FormatM(r.TotalWidth), U.FormatM(r.TotalHeight))
            + (r.ResolutionReduced ? Loc.T(" · resolução reduzida pelo limite", " · resolution reduced by the limit") : "");
        return pm;
    }

    internal static void Generate(ToolContext ctx, ImageOptions o, Context c, string systemId)
    {
        var doc = ctx.Doc;
        var r = ImageToBlocks.Generate(ImageLoader.Load(o.ImagePath), o.ToCore());
        ctx.CheckCount(r.Blocks.Count, "blocos", "blocks");
        var frame = new PlanarContext { Frame = c.Frame }.GetFrame();
        var sink = new BoxSink(ctx, o.Output, BoxKind.Block, systemId, o.UserFamily) { Name = "TT Imagem" };
        sink.Prepare();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var mats = new List<string?>();
            if (o.ColorMode == BlockColorMode.Single)
            {
                mats.Add(string.IsNullOrWhiteSpace(o.SingleMaterial) ? null : o.SingleMaterial);
            }
            else
            {
                for (var k = 0; k < r.Palette.Count; k++)
                {
                    var name = $"{o.MaterialPrefix} {k + 1:00} {r.Palette[k].Hex}";
                    MaterialsService.Ensure(doc, name, r.Palette[k]);
                    mats.Add(name);
                }
            }
            var i = 0;
            foreach (var b in r.Blocks)
            {
                var mat = mats[Math.Clamp(o.ColorMode == BlockColorMode.Single ? 0 : b.PaletteIndex, 0, mats.Count - 1)];
                var box = GenHost.CellBox(frame, b.Center, 0, b.Size, b.Size, b.Height, 0);
                sink.Add(box, mat, i++, e => RevitGeo.Set(e, $"L{b.Row + 1}C{b.Column + 1}", SharedParams.Codigo));
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, "gen.image", o, c, Path.GetFileName(o.ImagePath));
            ctx.Report.Created(sink.Created.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });

        var t = new TableData(Loc.T("Blocos por cor e altura", "Blocks by color and height"));
        t.AddColumn(Loc.T("Cor", "Color")).AddColumn(Loc.T("Nível de altura", "Height level"), ColumnKind.Integer)
         .AddColumn(Loc.T("Altura (mm)", "Height (mm)"), ColumnKind.Number, 0).AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer);
        foreach (var g in r.Blocks.GroupBy(b => (b.PaletteIndex, b.HeightLevel)).OrderBy(g => g.Key.PaletteIndex).ThenBy(g => g.Key.HeightLevel))
        {
            var color = o.ColorMode == BlockColorMode.Single ? "-" : r.Palette[Math.Clamp(g.Key.PaletteIndex, 0, r.Palette.Count - 1)].Hex;
            t.AddRow(color, g.Key.HeightLevel + 1, U.ToMm(g.First().Height), g.Count());
        }
        t.AddTotalsRow(Loc.T("Total", "Total"), 3);
        ctx.Report.Tables.Add(t);
        if (r.ResolutionReduced)
            ctx.Report.Info(Loc.F("Resolução reduzida para {0} × {1} pelo limite de blocos.", "Resolution reduced to {0} × {1} by the block limit.", r.Columns, r.Rows));
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<ImageOptions>(optionsJson) ?? new ImageOptions();
        var c = Json.Deserialize<Context>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: Preview, okText: Loc.T("Regenerar", "Regenerate"), key: "gen.image");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}
