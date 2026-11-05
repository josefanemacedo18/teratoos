using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Colors;
using TeraTools.Core.Export;
using TeraTools.Core.Finishes;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Tiling;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Floors;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Ui;
using Grid = System.Windows.Controls.Grid;
using TextBox = System.Windows.Controls.TextBox;
using Visibility = System.Windows.Visibility;

namespace TeraTools.Revit.Tools.Materials;

// ============================================================ Armazenamento e sincronização

/// <summary>O catálogo fica dentro do .rvt (seção do armazenamento do projeto).</summary>
internal static class FinishStore
{
    public static FinishCatalog Load(Document doc) => ProjectStorage.GetSection<FinishCatalog>(doc, FinishCatalog.SectionKey);

    public static void Save(Document doc, FinishCatalog c) => ProjectStorage.SetSection(doc, FinishCatalog.SectionKey, c);
}

/// <summary>
/// Cria/atualiza no Revit o que cada item do catálogo precisa: material (com cor, dados e hachura das
/// juntas), materiais das tonalidades e o tipo de piso, parede, rodapé ou forro. Deve rodar numa transação.
/// </summary>
internal static class FinishSync
{
    public sealed class Result
    {
        public int Materials;
        public int Types;
        public int Patterns;
    }

    public static Result Apply(ToolContext ctx, IEnumerable<FinishItem> items)
    {
        var r = new Result();
        foreach (var it in items)
        {
            try
            {
                var m = EnsureMaterial(ctx.Doc, it, r);
                ToneMaterials(ctx.Doc, it, m, r);
                EnsureType(ctx.Doc, it, m, r);
            }
            catch (Exception ex)
            {
                ctx.ItemFailed("Itens do catálogo não criados no Revit", "Catalog items not created in Revit", ex);
            }
        }
        return r;
    }

    public static Material EnsureMaterial(Document doc, FinishItem it, Result r)
    {
        var name = it.MaterialName;
        var m = Lookup.Material(doc, name);
        if (m == null)
        {
            // Item renomeado: procura o material pelo código (marca de identificação).
            m = Lookup.Collect<Material>(doc).FirstOrDefault(x =>
                string.Equals(RevitGeo.Text(x, BuiltInParameter.ALL_MODEL_MARK), it.Code, StringComparison.OrdinalIgnoreCase) &&
                x.Name.StartsWith(it.Code, StringComparison.OrdinalIgnoreCase));
            if (m != null && Lookup.Material(doc, name) == null)
            {
                try { m.Name = name; } catch { m = null; }
            }
        }
        var color = RgbColor.TryParse(it.ColorHex, out var c) ? c : RgbColor.Gray;
        var transparency = it.Kind == FinishKind.Glass ? 60 : 0;
        var shininess = it.Surface.Contains("poli", StringComparison.OrdinalIgnoreCase) || it.Surface.Contains("brilh", StringComparison.OrdinalIgnoreCase) ? 90 : 20;
        m = MaterialsService.Ensure(doc, name, color, transparency, shininess, updateColor: true);
        r.Materials++;
        try { m.MaterialCategory = FinishLabels.Label(it.Kind); } catch { /* categoria somente leitura */ }
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MARK, it.Code);
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_DESCRIPTION, string.IsNullOrWhiteSpace(it.Surface) ? it.Name : $"{it.Name} — {it.Surface}");
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MANUFACTURER, it.Supplier);
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MODEL, it.Line);
        if (it.Price > 0) RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_COST, it.Price);
        if (!string.IsNullOrWhiteSpace(it.Notes)) RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, it.Notes);
        // Hachura de superfície com as juntas reais (aparece em planta e 3D).
        if (EnsurePattern(doc, it, r) is { } fpe)
        {
            try
            {
                m.SurfaceForegroundPatternId = fpe.Id;
                var jc = RgbColor.TryParse(it.JointColorHex, out var j) ? j : Darker(color);
                m.SurfaceForegroundPatternColor = new Color(jc.R, jc.G, jc.B);
            }
            catch (Exception ex)
            {
                Log.Warn("Hachura do material: " + ex.Message);
            }
        }
        return m;
    }

    private static RgbColor Darker(RgbColor c) => new((byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6));

    /// <summary>Materiais das tonalidades (o primeiro é o próprio material do item).</summary>
    public static List<Material> ToneMaterials(Document doc, FinishItem it, Material baseMat, Result? r)
    {
        var list = new List<Material> { baseMat };
        var colors = it.ToneColors();
        for (var k = 1; k < colors.Count; k++)
        {
            var name = ToneName(it, k);
            var existed = Lookup.Material(doc, name) != null;
            var m = MaterialsService.Ensure(doc, name, colors[k], baseMat.Transparency, baseMat.Shininess, updateColor: true);
            if (!existed && r != null) r.Materials++;
            try { m.MaterialCategory = baseMat.MaterialCategory; } catch { /* ignora */ }
            RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MARK, it.Code);
            if (baseMat.SurfaceForegroundPatternId != ElementId.InvalidElementId)
            {
                try
                {
                    m.SurfaceForegroundPatternId = baseMat.SurfaceForegroundPatternId;
                    m.SurfaceForegroundPatternColor = baseMat.SurfaceForegroundPatternColor;
                }
                catch
                {
                    // ignora
                }
            }
            list.Add(m);
        }
        return list;
    }

    public static string ToneName(FinishItem it, int k) => $"{it.MaterialName} · tom {k + 1}";

    /// <summary>Hachura de modelo com as juntas do item (alinhada, amarração e diagonal).</summary>
    private static FillPatternElement? EnsurePattern(Document doc, FinishItem it, Result r)
    {
        if (!it.HasPieces) return null;
        var L = it.Length;
        var W = it.Width;
        var rot = MathX(it.RotationDeg);
        var grids = new List<FillGrid>();
        switch (it.Layout)
        {
            case TileLayout.Grid:
            case TileLayout.Diagonal:
            {
                if (it.Layout == TileLayout.Diagonal) rot += Math.PI / 4;
                grids.Add(new FillGrid { Angle = rot, Origin = new UV(0, 0), Offset = W, Shift = 0 });
                grids.Add(new FillGrid { Angle = rot + Math.PI / 2, Origin = new UV(0, 0), Offset = L, Shift = 0 });
                break;
            }
            case TileLayout.RunningBond:
            {
                // Juntas verticais tracejadas: uma fileira sim, n-1 não; cada linha deslocada uma fileira.
                var b = Math.Clamp(it.BondOffset, 0, 1);
                var n = b < 0.05 ? 1 : (int)Math.Round(1 / b);
                grids.Add(new FillGrid { Angle = rot, Origin = new UV(0, 0), Offset = W, Shift = 0 });
                if (n <= 1)
                {
                    grids.Add(new FillGrid { Angle = rot + Math.PI / 2, Origin = new UV(0, 0), Offset = L, Shift = 0 });
                }
                else
                {
                    var g = new FillGrid { Angle = rot + Math.PI / 2, Origin = new UV(0, 0), Offset = L / n, Shift = W };
                    g.SetSegments(new List<double> { W, (n - 1) * W });
                    grids.Add(g);
                }
                break;
            }
            default:
                return null; // espinha, trama e modulares: a paginação real (partes) mostra o desenho
        }
        var name = $"TT {it.Code} {Math.Round(it.LengthMm)}x{Math.Round(it.WidthMm)} {FinishLabels.Label(it.Layout)}";
        try
        {
            var fp = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToHost);
            fp.SetFillGrids(grids);
            var fpe = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Model, name);
            if (fpe == null) fpe = FillPatternElement.Create(doc, fp);
            else fpe.SetFillPattern(fp);
            r.Patterns++;
            return fpe;
        }
        catch (Exception ex)
        {
            Log.Warn($"Hachura de {it.Code}: {ex.Message}");
            return null;
        }
    }

    private static double MathX(double deg) => deg * Math.PI / 180;

    /// <summary>Tipo de piso, parede (revestimento/rodapé) ou forro com uma camada do material do item.</summary>
    public static ElementId? EnsureType(Document doc, FinishItem it, Material m, Result r)
    {
        switch (it.Use)
        {
            case FinishUse.Floor:
            {
                var t = Lookup.Named<FloorType>(doc, it.TypeName);
                if (t == null)
                {
                    var src = Lookup.Collect<FloorType>(doc).Where(x => !x.IsFoundationSlab && x.GetCompoundStructure() != null)
                        .OrderBy(x => x.GetCompoundStructure().GetWidth()).FirstOrDefault()
                        ?? throw new ToolException(Loc.T("o projeto não tem um tipo de piso básico para duplicar", "the project has no basic floor type to duplicate"));
                    t = (FloorType)src.Duplicate(it.TypeName);
                }
                SetLayer(t, Math.Max(it.Thickness, U.Mm(1)), m.Id);
                r.Types++;
                return t.Id;
            }
            case FinishUse.Wall when it.Kind != FinishKind.Paint:
            case FinishUse.Baseboard:
            {
                var t = Lookup.Named<WallType>(doc, it.TypeName);
                if (t == null)
                {
                    var src = Lookup.Collect<WallType>(doc).Where(x => x.Kind == WallKind.Basic && x.GetCompoundStructure() != null)
                        .OrderBy(x => x.Width).FirstOrDefault()
                        ?? throw new ToolException(Loc.T("o projeto não tem um tipo de parede básica para duplicar", "the project has no basic wall type to duplicate"));
                    t = (WallType)src.Duplicate(it.TypeName);
                }
                var w = it.Thickness > 0 ? it.Thickness : U.Mm(it.Use == FinishUse.Baseboard ? 15 : 10);
                SetLayer(t, w, m.Id);
                r.Types++;
                return t.Id;
            }
            case FinishUse.Ceiling:
            {
                var t = Lookup.Named<CeilingType>(doc, it.TypeName);
                if (t == null)
                {
                    var src = Lookup.Collect<CeilingType>(doc).FirstOrDefault(x => x.GetCompoundStructure() != null);
                    if (src == null) return null; // só há forro básico (sem camadas): o item usa o material
                    t = (CeilingType)src.Duplicate(it.TypeName);
                }
                SetLayer(t, Math.Max(it.Thickness, U.Mm(5)), m.Id);
                r.Types++;
                return t.Id;
            }
            default:
                return null;
        }
    }

    private static void SetLayer(HostObjAttributes t, double width, ElementId mat)
    {
        try
        {
            t.SetCompoundStructure(CompoundStructure.CreateSingleLayerCompoundStructure(MaterialFunctionAssignment.Finish1, width, mat));
        }
        catch
        {
            t.SetCompoundStructure(CompoundStructure.CreateSingleLayerCompoundStructure(MaterialFunctionAssignment.Structure, width, mat));
        }
    }

    /// <summary>Texto curto de um item para listas: tipo, aplicação, medidas e preço.</summary>
    public static string Detail(FinishItem i)
    {
        var parts = new List<string> { FinishLabels.Label(i.Use), FinishLabels.Label(i.Kind) };
        if (i.HasPieces) parts.Add($"{i.LengthMm:0.#}×{i.WidthMm:0.#} mm · {FinishLabels.Label(i.Layout).ToLowerInvariant()}");
        else if (i.ThicknessMm > 0) parts.Add($"{i.ThicknessMm:0.#} mm");
        if (i.Price > 0) parts.Add($"{U.FormatMoney(i.Price)}/{FinishLabels.Label(i.PriceUnit)}");
        if (!string.IsNullOrWhiteSpace(i.Supplier)) parts.Add(i.Supplier);
        return string.Join(" · ", parts);
    }
}

// ============================================================ Formulário do item

/// <summary>Campos de um item do catálogo no diálogo padrão (medidas em mm na tela).</summary>
public sealed class FinishItemForm
{
    [Opt("Código", "Code", Group = "Identificação|Identification", Required = true, Tip = "Único no catálogo, ex.: PI-01. Vai para a marca de identificação do material.|Unique, e.g. PI-01.")]
    public string Code { get; set; } = "";

    [Opt("Nome", "Name", Group = "Identificação|Identification", Required = true)]
    public string Name { get; set; } = "";

    [Opt("Tipo de acabamento", "Finish type", Group = "Identificação|Identification")]
    public FinishKind Kind { get; set; } = FinishKind.Porcelain;

    [Opt("Aplicação", "Use", Group = "Identificação|Identification",
        Tip = "Define o que o plugin cria: piso → tipo de piso; parede → tipo de parede fina (ou pintura); rodapé → tipo de parede de rodapé; forro → tipo de forro.|Defines what the plugin creates in Revit.")]
    public FinishUse Use { get; set; } = FinishUse.Floor;

    [Opt("Fornecedor / fabricante", "Supplier / manufacturer", Group = "Identificação|Identification")]
    public string Supplier { get; set; } = "";

    [Opt("Linha / referência", "Line / reference", Group = "Identificação|Identification")]
    public string Line { get; set; } = "";

    [Opt("Onde usar (ambientes)", "Where to use (rooms)", Group = "Identificação|Identification")]
    public string Rooms { get; set; } = "";

    [Opt("Observações", "Notes", Group = "Identificação|Identification")]
    public string Notes { get; set; } = "";

    [Opt("Cor", "Color", Group = "Aparência|Appearance", Kind = OptKind.Color)]
    public string ColorHex { get; set; } = "#C8BBA8";

    [Opt("Acabamento da superfície", "Surface finish", Group = "Aparência|Appearance", Tip = "Ex.: fosco, acetinado, polido, natural, escovado.|E.g. matte, satin, polished.")]
    public string Surface { get; set; } = "";

    [Opt("Tonalidades", "Tones", Group = "Aparência|Appearance", Min = 1, Max = 9,
        Tip = "Quantas variações de cor a paginação distribui entre as peças (madeira e pedra: 3 a 5).|How many color variations are spread among pieces.")]
    public int Tones { get; set; } = 1;

    [Opt("Variação entre tonalidades", "Variation between tones", Group = "Aparência|Appearance", Kind = OptKind.Percent, Min = 0, Max = 60, VisibleIf = "Tones!=1")]
    public double ToneVariation { get; set; } = 8;

    [Opt("Imagem de textura (referência)", "Texture image (reference)", Group = "Aparência|Appearance", Kind = OptKind.File, Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.tif")]
    public string TexturePath { get; set; } = "";

    [Opt("Comprimento da peça", "Piece length", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Length, Min = 0, Tip = "0 = sem peças (pintura, cimentício, carpete em rolo...).|0 = no pieces.")]
    public double Length { get; set; }

    [Opt("Largura da peça", "Piece width", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Length, Min = 0,
        Tip = "No rodapé, é a altura do rodapé.|For baseboards this is the baseboard height.")]
    public double Width { get; set; }

    [Opt("Espessura", "Thickness", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Length, Min = 0)]
    public double Thickness { get; set; }

    [Opt("Junta", "Joint", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Length, Min = 0)]
    public double Joint { get; set; }

    [Opt("Cor da junta", "Joint color", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Color)]
    public string JointColorHex { get; set; } = "";

    [Opt("Paginação", "Layout", Group = "Peça e paginação|Piece and layout")]
    public TileLayout Layout { get; set; } = TileLayout.Grid;

    [Opt("Deslocamento da amarração (0–1)", "Bond offset (0–1)", Group = "Peça e paginação|Piece and layout", Min = 0, Max = 1, VisibleIf = "Layout=RunningBond")]
    public double BondOffset { get; set; } = 0.5;

    [Opt("Rotação", "Rotation", Group = "Peça e paginação|Piece and layout", Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Perda", "Waste", Group = "Compra e custo|Purchase and cost", Kind = OptKind.Percent, Min = 0, Max = 100)]
    public double LossPercent { get; set; } = 10;

    [Opt("Preço (R$)", "Price (R$)", Group = "Compra e custo|Purchase and cost", Min = 0)]
    public double Price { get; set; }

    [Opt("Unidade do preço", "Price unit", Group = "Compra e custo|Purchase and cost")]
    public PriceUnit PriceUnit { get; set; } = PriceUnit.SquareMeter;

    [Opt("Peças por caixa", "Pieces per box", Group = "Compra e custo|Purchase and cost", Min = 0, Max = 100000)]
    public int PiecesPerBox { get; set; }

    [Opt("m² por caixa", "m² per box", Group = "Compra e custo|Purchase and cost", Min = 0)]
    public double M2PerBox { get; set; }

    public static FinishItemForm From(FinishItem i) => new()
    {
        Code = i.Code, Name = i.Name, Kind = i.Kind, Use = i.Use, Supplier = i.Supplier, Line = i.Line, Rooms = i.Rooms, Notes = i.Notes,
        ColorHex = i.ColorHex, Surface = i.Surface, Tones = i.Tones, ToneVariation = i.ToneVariation, TexturePath = i.TexturePath,
        Length = i.Length, Width = i.Width, Thickness = i.Thickness, Joint = i.Joint, JointColorHex = i.JointColorHex,
        Layout = i.Layout, BondOffset = i.BondOffset, Rotation = i.RotationDeg,
        LossPercent = i.LossPercent, Price = i.Price, PriceUnit = i.PriceUnit, PiecesPerBox = (int)Math.Round(i.PiecesPerBox), M2PerBox = i.M2PerBox,
    };

    public void ApplyTo(FinishItem i)
    {
        i.Code = Code.Trim();
        i.Name = Name.Trim();
        i.Kind = Kind;
        i.Use = Use;
        i.Supplier = Supplier.Trim();
        i.Line = Line.Trim();
        i.Rooms = Rooms.Trim();
        i.Notes = Notes.Trim();
        i.ColorHex = ColorHex;
        i.Surface = Surface.Trim();
        i.Tones = Math.Clamp(Tones, 1, 9);
        i.ToneVariation = ToneVariation;
        i.TexturePath = TexturePath;
        i.LengthMm = Math.Round(U.ToMm(Length), 2);
        i.WidthMm = Math.Round(U.ToMm(Width), 2);
        i.ThicknessMm = Math.Round(U.ToMm(Thickness), 2);
        i.JointMm = Math.Round(U.ToMm(Joint), 2);
        i.JointColorHex = JointColorHex;
        i.Layout = Layout;
        i.BondOffset = BondOffset;
        i.RotationDeg = Rotation;
        i.LossPercent = LossPercent;
        i.Price = Price;
        i.PriceUnit = PriceUnit;
        i.PiecesPerBox = PiecesPerBox;
        i.M2PerBox = M2PerBox;
    }

    public FinishItem ToItem()
    {
        var i = new FinishItem();
        ApplyTo(i);
        return i;
    }

    /// <summary>Amostra de 2,4 × 2,4 m com a paginação, as juntas e as tonalidades do item (2D e 3D).</summary>
    public static PreviewModel Preview(FinishItem it)
    {
        var pm = new PreviewModel();
        var size = U.M(2.4);
        var square = new Region2(Polygon2.Rectangle(Vec2.Zero, size, size));
        var colors = it.ToneColors().Select(c => c.Hex).ToList();
        var joint = RgbColor.TryParse(it.JointColorHex, out var jc) ? jc.Hex : "#8A8F98";
        var floor = new Frame3(Vec3.Zero, Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
        if (!it.HasPieces)
        {
            pm.Polygon(square.Outer, colors[0], "#5F6B7A", 1);
            pm.S3.Prism(floor, square.Outer, 0, Math.Max(it.Thickness, U.Mm(5)), colors[0]);
            pm.Info = Loc.T("Sem peças: o acabamento é contínuo (pintura, cimentício, carpete em rolo...).", "No pieces: continuous finish.");
            return pm;
        }
        TileLayoutResult r;
        try
        {
            var ts = it.ToTileSettings();
            ts.Start = TileStart.Corner;
            r = TileLayoutEngine.Generate(square, ts, 20000);
        }
        catch (Exception ex)
        {
            pm.Info = ex.Message;
            return pm;
        }
        pm.Polygon(square.Outer, joint, "#5F6B7A", 1);
        pm.S3.Prism(floor, square.Outer, -U.Mm(10), U.Mm(10), joint);
        var h = Math.Max(it.Thickness, U.Mm(4));
        foreach (var t in r.Tiles.Take(4000))
        {
            var color = colors[ToneIndex(t.Index, colors.Count)];
            pm.Polygon(t.Clipped, color, color, 0.2);
            pm.S3.Prism(floor, t.Clipped, 0, h, color);
        }
        pm.Info = Loc.F("Amostra de 2,40 × 2,40 m · {0} peças · {1} tonalidade(s)", "2.40 × 2.40 m sample · {0} pieces · {1} tone(s)", r.Tiles.Count, colors.Count);
        return pm;
    }

    /// <summary>Tonalidade de uma peça: distribuição estável e sem padrão aparente.</summary>
    public static int ToneIndex(int index, int count) => count <= 1 ? 0 : (int)((uint)(index * 2654435761u) % (uint)count);
}

// ============================================================ Janela do catálogo

internal sealed class FinishCatalogWindow : Window
{
    private readonly ToolContext _ctx;
    private readonly ListBox _list = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _count;
    private readonly TextBlock _errors;

    public FinishCatalog Catalog { get; }

    public FinishCatalogWindow(ToolContext ctx, FinishCatalog catalog)
    {
        _ctx = ctx;
        Catalog = catalog;
        Title = "Téra Tools — " + Loc.T("Catálogo de acabamentos", "Finish catalog");
        Width = 900;
        Height = 640;
        MinWidth = 640;
        MinHeight = 420;
        Theme.Apply(this, ctx.Owner);

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var head = UiKit.Col(2,
            UiKit.Title(Loc.T("Catálogo de acabamentos do projeto", "Project finish catalog")),
            UiKit.Muted(Loc.T("Cadastre pisos, revestimentos, pinturas, rodapés e forros com medidas, paginação, tonalidades, fornecedor e preço. O catálogo fica salvo dentro deste arquivo .rvt; use a planilha (CSV) para levar a outros projetos ou editar no Excel.",
                "Register floors, wall finishes, paints, baseboards and ceilings. The catalog is saved inside this .rvt; use the CSV sheet to reuse it or edit in Excel.")));
        head.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(head.At(0, 0));

        var sg = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        var ph = UiKit.Muted(Loc.T("Buscar por código, nome, tipo ou fornecedor…", "Search…"), wrap: false, margin: new Thickness(8, 0, 0, 0));
        ph.IsHitTestVisible = false;
        _search.TextChanged += (_, _) =>
        {
            ph.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        };
        sg.Children.Add(_search);
        sg.Children.Add(ph);
        root.Children.Add(sg.At(0, 1));

        var body = UiKit.Grid(UiKit.Star(), UiKit.Px(190));
        _list.MouseDoubleClick += (_, _) => Safe(Edit);
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Safe(Edit);
            if (e.Key == Key.Delete) Safe(Delete);
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        body.Children.Add(_list.At(0));
        var side = UiKit.Col(6,
            UiKit.Button(Loc.T("Novo acabamento", "New finish"), () => Safe(New), accent: true),
            UiKit.Button(Loc.T("Editar…", "Edit…"), () => Safe(Edit)),
            UiKit.Button(Loc.T("Duplicar", "Duplicate"), () => Safe(Duplicate)),
            UiKit.Button(Loc.T("Excluir", "Delete"), () => Safe(Delete)),
            UiKit.Divider(new Thickness(0, 6, 0, 6)),
            UiKit.Button(Loc.T("Importar planilha…", "Import sheet…"), () => Safe(Import), tooltip: Loc.T("CSV com as colunas do catálogo. Códigos existentes são atualizados; novos são acrescentados.", "Existing codes are updated; new ones added.")),
            UiKit.Button(Loc.T("Exportar planilha…", "Export sheet…"), () => Safe(Export)),
            UiKit.Button(Loc.T("Acrescentar exemplos", "Add examples"), () => Safe(AddExamples)));
        side.Margin = new Thickness(10, 0, 0, 0);
        body.Children.Add(side.At(1));
        root.Children.Add(body.At(0, 2));

        _errors = UiKit.Text("", colorKey: "TT.Error", margin: new Thickness(0, 8, 0, 0));
        _errors.Visibility = Visibility.Collapsed;
        root.Children.Add(_errors.At(0, 3));

        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 10, 0, 0);
        _count = UiKit.Muted("", wrap: false);
        footer.Children.Add(_count.At(0));
        var save = UiKit.Button(Loc.T("Salvar no projeto", "Save to project"), () => Safe(Accept), accent: true, minWidth: 140);
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => DialogResult = false, minWidth: 90);
        cancel.IsCancel = true;
        footer.Children.Add(UiKit.Row(8, cancel, save).At(1));
        root.Children.Add(footer.At(0, 4));
        Content = root;
        Refresh();
        Loaded += (_, _) => _search.Focus();
    }

    private void Safe(Action a)
    {
        try
        {
            a();
        }
        catch (Exception ex)
        {
            Log.Error("Catálogo de acabamentos", ex);
            _errors.Text = Loc.T("Erro: ", "Error: ") + ex.Message;
            _errors.Visibility = Visibility.Visible;
        }
    }

    private FinishItem? Current => (_list.SelectedItem as FrameworkElement)?.Tag as FinishItem;

    private void Refresh(FinishItem? select = null)
    {
        var keep = select ?? Current;
        _list.Items.Clear();
        var q = TextUtil.NormalizeForCompare(_search.Text ?? "");
        foreach (var i in Catalog.Items.OrderBy(i => i.Use).ThenBy(i => i.Code, StringComparer.OrdinalIgnoreCase))
        {
            var detail = FinishSync.Detail(i);
            if (q.Length > 0 && !TextUtil.NormalizeForCompare($"{i.Code} {i.Name} {detail} {i.Line}").Contains(q)) continue;
            var swatch = UiKit.ColorSwatch(RgbColor.TryParse(i.ColorHex, out var c) ? c.Hex : "#999999", 26);
            swatch.Margin = new Thickness(0, 0, 10, 0);
            var row = UiKit.Row(0, swatch, UiKit.Col(0, UiKit.Text($"{i.Code} — {i.Name}", bold: true, wrap: false), UiKit.Muted(detail, 11, wrap: false)));
            row.Tag = i;
            row.Margin = new Thickness(2, 4, 2, 4);
            _list.Items.Add(row);
            if (ReferenceEquals(i, keep)) _list.SelectedItem = row;
        }
        if (_list.SelectedItem == null && _list.Items.Count > 0) _list.SelectedIndex = 0;
        _count.Text = Loc.F("{0} acabamento(s) no catálogo", "{0} finish(es) in the catalog", Catalog.Items.Count);
        var errors = Catalog.Validate();
        _errors.Text = string.Join("\n", errors.Take(6));
        _errors.Visibility = errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool EditItem(FinishItem item, string title)
    {
        var form = FinishItemForm.From(item);
        var ok = OptionsDialog.Show(_ctx, form, title, Loc.T("Medidas em mm. A pré-visualização mostra uma amostra de 2,40 × 2,40 m com a paginação, as juntas e as tonalidades.",
            "Sizes in mm. The preview shows a 2.40 × 2.40 m sample."), "mat.catalog.item", f => FinishItemForm.Preview(f.ToItem()), Loc.T("OK", "OK"));
        if (!ok) return false;
        form.ApplyTo(item);
        return true;
    }

    private void New()
    {
        var use = Current?.Use ?? FinishUse.Floor;
        var item = new FinishItem { Code = Catalog.NewCode(use), Use = use, Kind = use == FinishUse.Floor ? FinishKind.Porcelain : FinishKind.Other };
        if (!EditItem(item, Loc.T("Novo acabamento", "New finish"))) return;
        Catalog.Items.Add(item);
        Refresh(item);
    }

    private void Edit()
    {
        if (Current is not { } item) return;
        var copy = item.Clone();
        if (!EditItem(copy, item.MaterialName)) return;
        var idx = Catalog.Items.IndexOf(item);
        Catalog.Items[idx] = copy;
        Refresh(copy);
    }

    private void Duplicate()
    {
        if (Current is not { } item) return;
        var copy = item.Clone();
        copy.Code = Catalog.NewCode(item.Use);
        copy.Name = item.Name + Loc.T(" (cópia)", " (copy)");
        Catalog.Items.Add(copy);
        Refresh(copy);
    }

    private void Delete()
    {
        if (Current is not { } item) return;
        if (!Msg.Confirm(Loc.F("Excluir \"{0}\" do catálogo? O material e os tipos já criados no projeto não são apagados.", "Delete \"{0}\"? Materials and types already created are kept.", item.MaterialName))) return;
        Catalog.Items.Remove(item);
        Refresh();
    }

    private void Import()
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = Loc.T("Planilha CSV|*.csv;*.txt", "CSV sheet|*.csv;*.txt") };
        if (d.ShowDialog(this) != true) return;
        var (added, updated, errors) = Catalog.ImportCsv(File.ReadAllText(d.FileName));
        Refresh();
        var msg = Loc.F("{0} acabamento(s) novo(s) e {1} atualizado(s).", "{0} new and {1} updated.", added, updated);
        if (errors.Count > 0) msg += "\n\n" + Loc.T("Avisos:", "Warnings:") + "\n" + string.Join("\n", errors.Take(12));
        Msg.Info(msg, Loc.T("Importar planilha", "Import sheet"));
    }

    private void Export()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("Planilha CSV|*.csv", "CSV sheet|*.csv"), FileName = Loc.T("Catálogo de acabamentos.csv", "Finish catalog.csv") };
        if (d.ShowDialog(this) != true) return;
        CsvWriter.Write(Catalog.ToTable(), d.FileName, Services.Settings.CsvSeparator);
        Msg.Info(Loc.F("Planilha salva em:\n{0}", "Sheet saved to:\n{0}", d.FileName), Loc.T("Exportar planilha", "Export sheet"));
    }

    private void AddExamples()
    {
        var added = 0;
        foreach (var ex in FinishCatalog.Starter().Items)
        {
            if (Catalog.Find(ex.Code) != null) ex.Code = Catalog.NewCode(ex.Use);
            Catalog.Items.Add(ex);
            added++;
        }
        Refresh();
        _count.Text += Loc.F(" · {0} exemplo(s) acrescentado(s)", " · {0} example(s) added", added);
    }

    private void Accept()
    {
        var errors = Catalog.Validate();
        if (errors.Count > 0)
        {
            Msg.Warn(Loc.T("Corrija antes de salvar:", "Fix before saving:") + "\n\n" + string.Join("\n", errors.Take(12)));
            return;
        }
        DialogResult = true;
    }
}

// ============================================================ Comandos

[Transaction(TransactionMode.Manual)]
[Tool("mat.catalog", Modules.Materials, "Catálogo de acabamentos", "Finish catalog", Order = 5, Large = true,
    DescPt = "Catálogo próprio do projeto (dentro do .rvt): pisos, revestimentos, pinturas, rodapés e forros com medidas, paginação, tonalidades, fornecedor e preço. Cria os materiais e os tipos no Revit e importa/exporta planilha CSV.",
    DescEn = "The project's own catalog (inside the .rvt): floors, wall finishes, paints, baseboards and ceilings with sizes, layout, tones, supplier and price.",
    HelpPt = "1. Cadastre cada acabamento (Novo) ou importe uma planilha CSV. A pré-visualização mostra a amostra com paginação, juntas e tonalidades.\n2. Salve no projeto: o plugin cria/atualiza, para cada item, o material (cor, fornecedor, linha, código e preço; hachura com as juntas), os materiais das tonalidades e o tipo correspondente — piso, parede fina (revestimento), rodapé ou forro, com a espessura do item.\n3. Use Materiais ▸ Aplicar acabamento do catálogo para colocar o item nos ambientes, e Quantitativo e custo dos acabamentos para o orçamento.\n\nPara levar o catálogo a outro projeto: Exportar planilha aqui e Importar planilha lá. O CSV abre no Excel; as colunas têm nomes em português.",
    HelpEn = "Register finishes or import a CSV sheet. Saving creates/updates materials, tone materials and the matching floor/wall/baseboard/ceiling types.",
    Keywords = "catalogo acabamentos piso revestimento pintura rodape forro madeira porcelanato pedra vinilico fornecedor preco csv excel finish catalog")]
public sealed class FinishCatalogCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var cat = FinishStore.Load(doc);
        if (cat.Items.Count == 0 && Msg.Confirm(Loc.T(
                "O catálogo deste projeto está vazio. Começar com exemplos (porcelanato, madeira em espinha, vinílico, pedra, rodapé, pintura, revestimento e forro)? Você pode editar ou apagar depois.",
                "The catalog is empty. Start with examples?"), ctx.Tool.Name))
            cat = FinishCatalog.Starter();
        var w = new FinishCatalogWindow(ctx, cat);
        if (w.ShowDialog() != true) ctx.Cancel();
        FinishSync.Result r = null!;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            FinishStore.Save(doc, w.Catalog);
            r = FinishSync.Apply(ctx, w.Catalog.Items);
        });
        ctx.Report.Success(Loc.F("Catálogo salvo no projeto: {0} acabamento(s).", "Catalog saved: {0} finish(es).", w.Catalog.Items.Count));
        ctx.Report.Count(Loc.T("Materiais criados/atualizados", "Materials created/updated"), r.Materials);
        ctx.Report.Count(Loc.T("Tipos de piso/parede/forro", "Floor/wall/ceiling types"), r.Types);
        ctx.Report.Count(Loc.T("Hachuras de juntas", "Joint patterns"), r.Patterns);
        ctx.Report.Info(Loc.T("Os tipos criados começam com \"TT\" e o código do item (ex.: \"TT PI-01 - …\"). Para colocar nos ambientes: Materiais ▸ Aplicar acabamento do catálogo.",
            "Created types start with \"TT\" and the item code. To place them: Materials ▸ Apply catalog finish."));
        ctx.Report.Tables.Add(w.Catalog.ToTable());
        ctx.Report.ForceShow = true;
    }
}

public sealed class CatalogFloorApply
{
    [Opt("Contorno pela", "Outline at")]
    public BoundaryAt Boundary { get; set; } = BoundaryAt.Finish;

    [Opt("Deslocamento do nível", "Offset from level", Kind = OptKind.Length)]
    public double Offset { get; set; }

    [Opt("Ignorar ambientes que já têm piso de acabamento", "Skip rooms that already have a finish floor")]
    public bool SkipExisting { get; set; } = true;

    [Opt("Paginar com as peças do item", "Lay out with the item's pieces", Tip = "Divide o piso em peças reais (partes do Revit) com a paginação, as juntas e as tonalidades do catálogo.|Divides the floor into real pieces with the catalog layout, joints and tones.")]
    public bool Paging { get; set; } = true;

    [Opt("Ponto inicial da paginação", "Layout start point", VisibleIf = "Paging")]
    public TileStart Start { get; set; } = TileStart.Center;

    [Opt("Sugerir melhor ponto inicial (evitar filetes)", "Suggest best start (avoid slivers)", VisibleIf = "Paging")]
    public bool Optimize { get; set; } = true;
}

public enum WallApplyMode
{
    [Text("Pintar as faces das paredes (material)", "Paint wall faces (material)")] Paint,
    [Text("Revestimento (parede fina com o tipo do item)", "Covering (thin wall of the item type)")] Covering,
}

public sealed class CatalogWallApply
{
    [Opt("Como aplicar", "How to apply")]
    public WallApplyMode Mode { get; set; } = WallApplyMode.Paint;

    [Opt("Altura do revestimento", "Covering height", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Mode=Covering&!FullHeight")]
    public double Height { get; set; } = U.M(1.2);

    [Opt("Até o teto do ambiente", "Up to the room ceiling", VisibleIf = "Mode=Covering")]
    public bool FullHeight { get; set; }

    [Opt("Recortar portas e janelas", "Cut doors and windows", VisibleIf = "Mode=Covering")]
    public bool CutOpenings { get; set; } = true;
}

public sealed class CatalogCeilingApply
{
    [Opt("Altura do forro (a partir do nível)", "Ceiling height (from level)", Kind = OptKind.Length, Min = 0)]
    public double Height { get; set; } = U.M(2.6);

    [Opt("Afastamento das paredes (tabica)", "Gap from walls (shadow gap)", Kind = OptKind.Length, Min = 0)]
    public double Gap { get; set; }

    [Opt("Substituir forros existentes no ambiente", "Replace existing ceilings in the room")]
    public bool Replace { get; set; }
}

public sealed class CatalogBaseboardApply
{
    [Opt("Altura do rodapé", "Baseboard height", Kind = OptKind.Length, Min = 0.005)]
    public double Height { get; set; } = U.Mm(70);

    [Opt("Interromper nas portas", "Stop at doors")]
    public bool SkipDoors { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.catalogapply", Modules.Materials, "Aplicar acabamento do catálogo", "Apply catalog finish", Order = 6, Large = true,
    DescPt = "Escolha um acabamento do catálogo e os ambientes: o plugin cria o piso (e pagina com as peças e tonalidades), pinta ou reveste as paredes, cria o forro ou o rodapé — em um passo.",
    DescEn = "Pick a catalog finish and the rooms: the plugin creates the floor (and lays it out), paints or covers the walls, creates the ceiling or baseboard — in one step.",
    HelpPt = "O que é criado depende da Aplicação do item:\n• Piso: piso do tipo do item em cada ambiente e, se quiser, paginação com peças reais, juntas e tonalidades.\n• Parede: pintura das faces voltadas para o ambiente ou revestimento em parede fina (recortando vãos).\n• Forro: forro do tipo do item na altura escolhida.\n• Rodapé: rodapé do tipo do item, interrompido nas portas.\n• Bancada, marcenaria e outros: o material é aplicado aos elementos selecionados.\n\nTudo vira um único Desfazer.",
    HelpEn = "What is created depends on the item's use: floor, wall paint/covering, ceiling, baseboard or material on selected elements.",
    Keywords = "aplicar acabamento catalogo piso parede forro rodape ambiente apply finish catalog room")]
public sealed class FinishApplyCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var cat = FinishStore.Load(doc);
        if (cat.Items.Count == 0)
            ToolContext.Fail("O catálogo deste projeto está vazio. Abra Materiais ▸ Catálogo de acabamentos e cadastre os acabamentos.",
                "The catalog is empty. Open Materials ▸ Finish catalog first.");
        var item = Pick.One(cat.Items.OrderBy(i => i.Use).ThenBy(i => i.Code), i => $"{i.Code} — {i.Name}", Loc.T("Aplicar acabamento", "Apply finish"),
            Loc.T("Escolha o acabamento do catálogo", "Pick the catalog finish"), ctx.Owner, FinishSync.Detail);
        if (item == null) ctx.Cancel();
        var it = item!;
        switch (it.Use)
        {
            case FinishUse.Floor:
                ApplyFloor(ctx, it);
                break;
            case FinishUse.Wall:
                ApplyWall(ctx, it);
                break;
            case FinishUse.Ceiling:
                ApplyCeiling(ctx, it);
                break;
            case FinishUse.Baseboard:
                ApplyBaseboard(ctx, it);
                break;
            default:
                ApplyToElements(ctx, it);
                break;
        }
    }

    private static List<Room> Rooms(ToolContext ctx) => FloorUtil.PickRooms(ctx);

    /// <summary>Garante material e tipo do item (primeira etapa do mesmo Desfazer).</summary>
    private static Material Prepare(ToolContext ctx, FinishItem it)
    {
        Material m = null!;
        ctx.Modify(Loc.T("Preparar acabamento", "Prepare finish"), () =>
        {
            var r = new FinishSync.Result();
            m = FinishSync.EnsureMaterial(ctx.Doc, it, r);
            FinishSync.ToneMaterials(ctx.Doc, it, m, r);
            FinishSync.EnsureType(ctx.Doc, it, m, r);
        });
        return m;
    }

    private static void ApplyFloor(ToolContext ctx, FinishItem it)
    {
        var rooms = Rooms(ctx);
        var o = ctx.LoadOptions<CatalogFloorApply>();
        if (!it.HasPieces) o.Paging = false;
        var first = rooms.Select(r => RevitGeo.RoomRegion(r)).FirstOrDefault(r => r != null);
        o = ctx.Ask(o, title: it.MaterialName, preview: x =>
        {
            if (first == null || !x.Paging) return FinishItemForm.Preview(it);
            var s = it.ToTileSettings();
            s.Start = x.Start;
            return TileOptions.Preview(first, s, x.Optimize);
        }, okText: Loc.T("Aplicar", "Apply"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            Prepare(ctx, it);
            FloorByRoomCommand.Execute(ctx, rooms, new FloorByRoomOptions
            {
                FloorType = it.TypeName,
                Boundary = o.Boundary,
                Offset = o.Offset,
                SkipExisting = o.SkipExisting,
                FillFinish = true,
            });
            if (!o.Paging || !it.HasPieces) return;
            var floors = ctx.Report.ResultElementIds.Select(id => ctx.Doc.GetElement(new ElementId(id))).OfType<Floor>().ToList();
            var jobs = floors.Select(f => PagingCommand.JobFor(f, true)).Where(j => j != null).Select(j => j!.Value).ToList();
            if (jobs.Count == 0) return;
            var po = new PagingOptions
            {
                TileLength = it.Length,
                TileWidth = it.Width,
                Joint = it.Joint,
                Layout = it.Layout,
                BondOffset = it.BondOffset,
                Rotation = it.RotationDeg,
                Start = o.Start == TileStart.Custom ? TileStart.Center : o.Start,
                Optimize = o.Optimize,
                LossPercent = it.LossPercent,
                TilesPerBox = (int)Math.Round(it.PiecesPerBox),
                Material = it.MaterialName,
                CatalogCode = it.Code,
                CreateSchedule = true,
            };
            PagingCommand.Execute(ctx, jobs, po, null);
        });
    }

    private static void ApplyWall(ToolContext ctx, FinishItem it)
    {
        var rooms = Rooms(ctx);
        var o = ctx.Ask(ctx.LoadOptions<CatalogWallApply>(), title: it.MaterialName, preview: _ => FinishItemForm.Preview(it), okText: Loc.T("Aplicar", "Apply"));
        if (it.Kind == FinishKind.Paint && o.Mode == WallApplyMode.Covering)
            ctx.Report.Info(Loc.T("Pintura aplicada como revestimento fino: para pintura use \"Pintar as faces\".", "Paint applied as thin covering."));
        ctx.Group(ctx.Tool.Name, () =>
        {
            var m = Prepare(ctx, it);
            if (o.Mode == WallApplyMode.Paint)
            {
                ctx.Modify(ctx.Tool.Name, () =>
                {
                    foreach (var room in rooms)
                    {
                        var faces = RoomWallPainter.Paint(ctx.Doc, room, m, ctx.Report);
                        ctx.Report.Count(Loc.T("Faces", "Faces"), faces);
                        if (faces > 0)
                        {
                            RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_WALL, it.MaterialName);
                            ctx.Report.AddResult(room.Id.Value);
                        }
                        else
                        {
                            ctx.ItemFailed("Ambientes sem parede pintada", "Rooms with no wall painted", Loc.T("nenhuma face de parede voltada para o ambiente", "no wall face facing the room"), room.Id.Value);
                        }
                    }
                    ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Faces", "Faces")), "faces de parede", "wall faces");
                });
                return;
            }
            if (Lookup.Named<WallType>(ctx.Doc, it.TypeName) == null)
                ToolContext.Fail("Não foi possível criar o tipo de parede do item (o projeto precisa de um tipo de parede básica).", "Could not create the item's wall type.");
            WallFinishCommand.Execute(ctx, rooms, new WallFinishOptions
            {
                Type = it.TypeName,
                Height = o.Height,
                FullHeight = o.FullHeight,
                CutOpenings = o.CutOpenings,
                FillFinish = true,
            });
        });
    }

    private static void ApplyCeiling(ToolContext ctx, FinishItem it)
    {
        var rooms = Rooms(ctx);
        var o = ctx.Ask(ctx.LoadOptions<CatalogCeilingApply>(), title: it.MaterialName, preview: _ => FinishItemForm.Preview(it), okText: Loc.T("Aplicar", "Apply"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            Prepare(ctx, it);
            var typeName = Lookup.Named<CeilingType>(ctx.Doc, it.TypeName) != null ? it.TypeName : "";
            if (typeName.Length == 0)
                ctx.Report.Info(Loc.T("O projeto não tem forro com camadas para duplicar: foi usado o primeiro tipo de forro.", "No compound ceiling type to duplicate: the first ceiling type was used."));
            CeilingByRoomCommand.Execute(ctx, rooms, new CeilingByRoomOptions { Type = typeName, Height = o.Height, Gap = o.Gap, Replace = o.Replace, FillFinish = true });
        });
    }

    private static void ApplyBaseboard(ToolContext ctx, FinishItem it)
    {
        var rooms = Rooms(ctx);
        var o = ctx.LoadOptions<CatalogBaseboardApply>();
        if (it.WidthMm > 0) o.Height = it.Width;
        o = ctx.Ask(o, title: it.MaterialName, okText: Loc.T("Aplicar", "Apply"));
        ctx.Group(ctx.Tool.Name, () =>
        {
            Prepare(ctx, it);
            BaseboardCommand.Execute(ctx, rooms, new BaseboardOptions { WallType = it.TypeName, Height = o.Height, SkipDoors = o.SkipDoors, FillFinish = true });
        });
    }

    /// <summary>Bancada, marcenaria e outros: material nos elementos selecionados.</summary>
    private static void ApplyToElements(ToolContext ctx, FinishItem it)
    {
        var els = ctx.SelectedOrPick("Selecione os elementos que recebem o acabamento", "Select the elements", e => e.Category != null && e is not Room);
        ctx.Group(ctx.Tool.Name, () =>
        {
            var m = Prepare(ctx, it);
            ctx.Modify(ctx.Tool.Name, () =>
            {
                foreach (var e in els)
                {
                    // 1) parâmetro de material da peça (TT_Material, "Material"); 2) pintura das faces (elementos hospedeiros)
                    if (RevitGeo.Set(e, m.Id, SharedParams.Material, "Material", "Acabamento", "Finish"))
                    {
                        ctx.Report.Modified();
                        ctx.Report.AddResult(e.Id.Value);
                        continue;
                    }
                    var painted = 0;
                    string? reason = null;
                    foreach (var s in RevitGeo.Solids(e))
                        foreach (Face f in s.Faces)
                        {
                            try
                            {
                                ctx.Doc.Paint(e.Id, f, m.Id);
                                painted++;
                            }
                            catch (Exception ex)
                            {
                                reason ??= ex.Message;
                            }
                        }
                    if (painted > 0)
                    {
                        ctx.Report.Modified();
                        ctx.Report.AddResult(e.Id.Value);
                    }
                    else
                    {
                        ctx.ItemFailed("Elementos sem o acabamento", "Elements without the finish",
                            reason ?? Loc.T("o elemento não tem parâmetro de material nem faces pintáveis", "no material parameter or paintable faces"), e.Id.Value);
                    }
                }
                ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Modificados", "Modified")), "elementos com o acabamento", "elements with the finish");
            });
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.catalogqty", Modules.Materials, "Quantitativo e custo dos acabamentos", "Finish quantities and cost", Order = 7,
    DescPt = "Mede no modelo a área (ou o comprimento) de cada acabamento do catálogo, soma a perda, calcula peças, caixas e o custo — pronto para exportar ao Excel.",
    DescEn = "Measures each catalog finish in the model, adds waste, computes pieces, boxes and cost.",
    HelpPt = "Medição por item:\n• Piso, parede (revestimento) e forro: área dos elementos do tipo do item (\"TT código - nome\").\n• Pintura e outros: área das faces pintadas com o material.\n• Rodapé: comprimento dos rodapés do tipo do item.\n• Peças: partes da paginação com o material (ou área ÷ área da peça).\n\nCompra = quantidade × (1 + perda). Com m² por caixa ou peças por caixa, arredonda em caixas inteiras. O custo usa o preço e a unidade do item.",
    HelpEn = "Area/length per item, plus waste, pieces, boxes and cost.",
    Keywords = "quantitativo custo orcamento acabamento catalogo caixas pecas perda finish cost budget quantities")]
public sealed class FinishQuantitiesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var cat = FinishStore.Load(doc);
        if (cat.Items.Count == 0)
            ToolContext.Fail("O catálogo deste projeto está vazio. Abra Materiais ▸ Catálogo de acabamentos.", "The catalog is empty.");

        var floors = Lookup.Collect<Floor>(doc).ToList();
        var walls = Lookup.Collect<Wall>(doc).ToList();
        var ceilings = Lookup.Collect<Ceiling>(doc).ToList();
        var parts = Lookup.Collect<Part>(doc).ToList();
        var hosts = floors.Cast<Element>().Concat(walls).Concat(ceilings).ToList();

        var t = new TableData(Loc.T("Quantitativo e custo dos acabamentos", "Finish quantities and cost"));
        t.AddColumn(Loc.T("Código", "Code")).AddColumn(Loc.T("Acabamento", "Finish")).AddColumn(Loc.T("Aplicação", "Use"))
         .AddColumn(Loc.T("No modelo", "In model"), ColumnKind.Number).AddColumn(Loc.T("Unidade", "Unit"))
         .AddColumn(Loc.T("Perda (%)", "Waste (%)"), ColumnKind.Number, 0).AddColumn(Loc.T("Comprar", "To buy"), ColumnKind.Number)
         .AddColumn(Loc.T("Peças", "Pieces"), ColumnKind.Integer).AddColumn(Loc.T("Caixas", "Boxes"), ColumnKind.Integer)
         .AddColumn(Loc.T("Preço (R$)", "Price (R$)"), ColumnKind.Number).AddColumn(Loc.T("Total (R$)", "Total (R$)"), ColumnKind.Number);
        var unused = new List<string>();
        foreach (var it in cat.Items.OrderBy(i => i.Use).ThenBy(i => i.Code))
        {
            var mat = Lookup.Material(doc, it.MaterialName);
            var matIds = new HashSet<long>();
            if (mat != null) matIds.Add(mat.Id.Value);
            for (var k = 1; k < Math.Clamp(it.Tones, 1, 9); k++)
                if (Lookup.Material(doc, FinishSync.ToneName(it, k)) is { } tm) matIds.Add(tm.Id.Value);

            double area = 0, length = 0;
            var ids = new List<long>();
            if (it.Use == FinishUse.Baseboard)
            {
                foreach (var w in walls.Where(w => w.WallType.Name == it.TypeName))
                {
                    length += w.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
                    ids.Add(w.Id.Value);
                }
            }
            else
            {
                foreach (var e in hosts.Where(e => ctx.Doc.GetElement(e.GetTypeId())?.Name == it.TypeName))
                {
                    area += e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0;
                    ids.Add(e.Id.Value);
                }
                if (mat != null)
                {
                    foreach (var e in hosts)
                    {
                        double painted;
                        try
                        {
                            painted = e.GetMaterialArea(mat.Id, true);
                        }
                        catch
                        {
                            continue;
                        }
                        if (painted <= 1e-6) continue;
                        area += painted;
                        ids.Add(e.Id.Value);
                    }
                }
            }
            var pieces = parts.Count(p => p.get_Parameter(BuiltInParameter.DPART_MATERIAL_ID_PARAM)?.AsElementId() is { } pm && matIds.Contains(pm.Value));
            if (pieces == 0 && it.HasPieces && area > 0) pieces = (int)Math.Ceiling(area / (it.Length * it.Width));

            var loss = 1 + it.LossPercent / 100;
            var isLinear = it.Use == FinishUse.Baseboard || it.PriceUnit == PriceUnit.Meter;
            var qty = isLinear ? U.ToM(length) : U.SqFtToM2(area);
            if (qty <= 1e-6)
            {
                unused.Add(it.MaterialName);
                continue;
            }
            var buy = qty * loss;
            var piecesToBuy = pieces > 0 ? (int)Math.Ceiling(pieces * loss) : 0;
            var boxes = it.M2PerBox > 0 && !isLinear ? (int)Math.Ceiling(buy / it.M2PerBox)
                : it.PiecesPerBox > 0 && piecesToBuy > 0 ? (int)Math.Ceiling(piecesToBuy / it.PiecesPerBox) : 0;
            var total = it.Price <= 0 ? 0 : it.PriceUnit switch
            {
                PriceUnit.SquareMeter => it.Price * (boxes > 0 && it.M2PerBox > 0 ? boxes * it.M2PerBox : buy),
                PriceUnit.Meter => it.Price * buy,
                PriceUnit.Unit => it.Price * (piecesToBuy > 0 ? piecesToBuy : buy),
                PriceUnit.Box => it.Price * boxes,
                _ => 0,
            };
            t.AddRowWithIds(ids.Distinct(), it.Code, it.Name, FinishLabels.Label(it.Use), qty, isLinear ? "m" : "m²", it.LossPercent, buy,
                pieces, boxes, it.Price, total);
        }
        t.AddTotalsRow(Loc.T("TOTAL", "TOTAL"), 10);
        ctx.Report.Tables.Add(t);
        if (unused.Count > 0)
            ctx.Report.Info(Loc.F("Sem uso no modelo ({0}): {1}", "Not used in the model ({0}): {1}", unused.Count, string.Join("; ", unused)));
        if (cat.Items.Any(i => i.PriceUnit == PriceUnit.Liter))
            ctx.Report.Info(Loc.T("Itens com preço por litro mostram a área, sem custo (o rendimento por litro varia por produto).", "Per-liter items show area without cost."));
        ctx.Report.ForceShow = true;
    }
}
