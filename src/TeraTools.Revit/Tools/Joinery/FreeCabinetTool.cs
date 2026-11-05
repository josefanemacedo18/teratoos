using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Joinery;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;
using Color = System.Windows.Media.Color;
using FormattedText = System.Windows.Media.FormattedText;
using ComboBox = System.Windows.Controls.ComboBox;
using Grid = System.Windows.Controls.Grid;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using TextBox = System.Windows.Controls.TextBox;
using Visibility = System.Windows.Visibility;

namespace TeraTools.Revit.Tools.Joinery;

// ============================================================ Caixa, base e materiais (formulário padrão)

public sealed class FreeBoxForm
{
    [Opt("Código", "Code", Group = "Geral|General", Required = true)]
    public string Code { get; set; } = "MV1";

    [Opt("Nome", "Name", Group = "Geral|General")]
    public string Name { get; set; } = "";

    [Opt("Largura", "Width", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Width { get; set; }

    [Opt("Altura", "Height", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Height { get; set; }

    [Opt("Profundidade", "Depth", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Depth { get; set; }

    [Opt("Altura de instalação (do piso)", "Mounting height (from floor)", Kind = OptKind.Length, Min = 0, Group = "Geral|General")]
    public double Elevation { get; set; }

    [Opt("Espessura da caixa", "Carcass thickness", Kind = OptKind.Length, Min = 0.005, Group = "Caixa|Carcass")]
    public double Thickness { get; set; }

    [Opt("Fundo", "Back", Group = "Caixa|Carcass")]
    public BackKind Back { get; set; }

    [Opt("Espessura do fundo", "Back thickness", Kind = OptKind.Length, Min = 0.001, Group = "Caixa|Carcass", VisibleIf = "Back!=None")]
    public double BackThickness { get; set; }

    [Opt("Recuo do fundo", "Back recess", Kind = OptKind.Length, Min = 0, Group = "Caixa|Carcass", VisibleIf = "Back=Inset")]
    public double BackRecess { get; set; }

    [Opt("Tampo da caixa", "Carcass top", Group = "Caixa|Carcass")]
    public TopKind Top { get; set; }

    [Opt("Largura das travessas", "Rail width", Kind = OptKind.Length, Min = 0.01, Group = "Caixa|Carcass", VisibleIf = "Top=Rails")]
    public double RailWidth { get; set; }

    [Opt("Base", "Base", Group = "Base|Base")]
    public FreeBase Base { get; set; }

    [Opt("Altura do rodapé", "Plinth height", Kind = OptKind.Length, Min = 0, Group = "Base|Base", VisibleIf = "Base=Plinth")]
    public double PlinthHeight { get; set; }

    [Opt("Recuo do rodapé", "Plinth setback", Kind = OptKind.Length, Min = 0, Group = "Base|Base", VisibleIf = "Base=Plinth")]
    public double PlinthSetback { get; set; }

    [Opt("Altura dos pés", "Feet height", Kind = OptKind.Length, Min = 0, Group = "Base|Base", VisibleIf = "Base=Feet")]
    public double FeetHeight { get; set; }

    [Opt("Frentes sobrepostas (desmarque para embutidas)", "Overlay fronts (untick for inset)", Group = "Frentes|Fronts",
        Tip = "Sobrepostas cobrem a caixa; embutidas ficam dentro dos vãos, rentes à caixa.|Overlay fronts cover the carcass; inset fronts sit inside the spaces.")]
    public bool OverlayFronts { get; set; }

    [Opt("Espessura das frentes", "Front thickness", Kind = OptKind.Length, Min = 0.005, Group = "Frentes|Fronts")]
    public double FrontThickness { get; set; }

    [Opt("Folga entre frentes", "Gap between fronts", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts")]
    public double FrontGap { get; set; }

    [Opt("Transpasse das portas de correr", "Sliding door overlap", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts")]
    public double SlidingOverlap { get; set; }

    [Opt("Puxador", "Handle", Group = "Frentes|Fronts")]
    public HandleKind Handle { get; set; }

    [Opt("Comprimento do puxador", "Handle length", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts", VisibleIf = "Handle=Bar")]
    public double HandleLength { get; set; }

    [Opt("Amortecimento (dobradiças e corrediças)", "Soft close (hinges and slides)", Group = "Frentes|Fronts")]
    public bool SoftClose { get; set; }

    [Opt("Corrediças das gavetas", "Drawer slides", Group = "Frentes|Fronts")]
    public SlideKind Slides { get; set; }

    [Opt("Recuo das prateleiras", "Shelf setback", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts")]
    public double ShelfSetback { get; set; }

    [Opt("Tampo de bancada (pedra/madeira)", "Countertop", Group = "Tampo de bancada|Countertop")]
    public bool HasCountertop { get; set; }

    [Opt("Espessura do tampo", "Countertop thickness", Kind = OptKind.Length, Min = 0.005, Group = "Tampo de bancada|Countertop", VisibleIf = "HasCountertop")]
    public double CountertopThickness { get; set; }

    [Opt("Avanço na frente", "Front overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo de bancada|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangFront { get; set; }

    [Opt("Avanço atrás", "Back overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo de bancada|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangBack { get; set; }

    [Opt("Avanço nas laterais", "Side overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo de bancada|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangSides { get; set; }

    [Opt("Material da caixa", "Carcass material", Group = "Materiais|Materials", Tip = "Criado automaticamente se não existir.|Created automatically if missing.")]
    public string CarcassMaterial { get; set; } = "";

    [Opt("Veio na caixa", "Grain on carcass", Group = "Materiais|Materials")]
    public bool CarcassHasGrain { get; set; }

    [Opt("Material das frentes", "Front material", Group = "Materiais|Materials")]
    public string FrontMaterial { get; set; } = "";

    [Opt("Veio nas frentes", "Grain on fronts", Group = "Materiais|Materials")]
    public bool FrontHasGrain { get; set; }

    [Opt("Material do fundo", "Back material", Group = "Materiais|Materials")]
    public string BackMaterial { get; set; } = "";

    [Opt("Material das gavetas", "Drawer material", Group = "Materiais|Materials")]
    public string DrawerMaterial { get; set; } = "";

    [Opt("Material do tampo de bancada", "Countertop material", Group = "Materiais|Materials")]
    public string CountertopMaterial { get; set; } = "";

    [Opt("Material das portas de vidro", "Glass door material", Group = "Materiais|Materials")]
    public string GlassMaterial { get; set; } = "";

    [Opt("Material dos puxadores", "Handle material", Group = "Materiais|Materials")]
    public string HandleMaterial { get; set; } = "";

    [Opt("Forma livre (sem família, mais leve)", "Free form (no family, lighter)", Group = "Revit|Revit",
        Tip = "Desmarcado: cada peça é uma família TT_Chapa com parâmetros de corte (recomendado).|Unticked: each piece is a TT_Chapa family with cut parameters (recommended).")]
    public bool FreeForm { get; set; }

    public static FreeBoxForm From(FreeDesign d, bool freeForm) => new()
    {
        Code = d.Code, Name = d.Name, Width = d.Width, Height = d.Height, Depth = d.Depth, Elevation = d.Elevation,
        Thickness = d.Thickness, Back = d.Back, BackThickness = d.BackThickness, BackRecess = d.BackRecess, Top = d.Top, RailWidth = d.RailWidth,
        Base = d.Base, PlinthHeight = d.PlinthHeight, PlinthSetback = d.PlinthSetback, FeetHeight = d.FeetHeight,
        OverlayFronts = d.OverlayFronts, FrontThickness = d.FrontThickness, FrontGap = d.FrontGap, SlidingOverlap = d.SlidingOverlap,
        Handle = d.Handle, HandleLength = d.HandleLength, SoftClose = d.SoftClose, Slides = d.Slides, ShelfSetback = d.ShelfSetback,
        HasCountertop = d.HasCountertop, CountertopThickness = d.CountertopThickness, OverhangFront = d.CountertopOverhangFront,
        OverhangBack = d.CountertopOverhangBack, OverhangSides = d.CountertopOverhangSides,
        CarcassMaterial = d.CarcassMaterial, CarcassHasGrain = d.CarcassHasGrain, FrontMaterial = d.FrontMaterial, FrontHasGrain = d.FrontHasGrain,
        BackMaterial = d.BackMaterial, DrawerMaterial = d.DrawerMaterial, CountertopMaterial = d.CountertopMaterial, GlassMaterial = d.GlassMaterial,
        HandleMaterial = d.HandleMaterial, FreeForm = freeForm,
    };

    public void ApplyTo(FreeDesign d)
    {
        d.Code = Code.Trim(); d.Name = Name.Trim(); d.Width = Width; d.Height = Height; d.Depth = Depth; d.Elevation = Elevation;
        d.Thickness = Thickness; d.Back = Back; d.BackThickness = BackThickness; d.BackRecess = BackRecess; d.Top = Top; d.RailWidth = RailWidth;
        d.Base = Base; d.PlinthHeight = PlinthHeight; d.PlinthSetback = PlinthSetback; d.FeetHeight = FeetHeight;
        d.OverlayFronts = OverlayFronts; d.FrontThickness = FrontThickness; d.FrontGap = FrontGap; d.SlidingOverlap = SlidingOverlap;
        d.Handle = Handle; d.HandleLength = HandleLength; d.SoftClose = SoftClose; d.Slides = Slides; d.ShelfSetback = ShelfSetback;
        d.HasCountertop = HasCountertop; d.CountertopThickness = CountertopThickness; d.CountertopOverhangFront = OverhangFront;
        d.CountertopOverhangBack = OverhangBack; d.CountertopOverhangSides = OverhangSides;
        d.CarcassMaterial = CarcassMaterial; d.CarcassHasGrain = CarcassHasGrain; d.FrontMaterial = FrontMaterial; d.FrontHasGrain = FrontHasGrain;
        d.BackMaterial = BackMaterial; d.DrawerMaterial = DrawerMaterial; d.CountertopMaterial = CountertopMaterial; d.GlassMaterial = GlassMaterial;
        d.HandleMaterial = HandleMaterial;
    }
}

// ============================================================ Desenho da elevação (vista frontal clicável)

internal sealed class FreeElevation : FrameworkElement
{
    public FreeDesign? Design { get; set; }
    public CabinetModel? Model { get; set; }
    public List<FreeCellRect> Cells { get; set; } = new();
    public string? Selected { get; set; }
    /// <summary>Peças que se sobrepõem (desenhadas em vermelho).</summary>
    public HashSet<string> Clashing { get; set; } = new();
    private string? _hover;
    private double _scale = 1, _ox, _oy;

    public event Action<string>? CellClicked;

    public FreeElevation()
    {
        ClipToBounds = true;
        Cursor = Cursors.Hand;
        ToolTip = Loc.T("Clique num vão para escolher. Clique de novo no vão escolhido para escolher o grupo em volta dele.",
            "Click a space to select it. Click it again to select the group around it.");
    }

    private Brush B(string key, string fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));

    private static Brush Hex(string hex, byte alpha = 255)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        c.A = alpha;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private Point P(double x, double z) => new(_ox + x * _scale, _oy - z * _scale);

    private Rect R(double x0, double z0, double x1, double z1)
    {
        var a = P(x0, z0);
        var b = P(x1, z1);
        return new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(B("TT.Surface", "#F5F6F8"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (Design == null) return;
        var d = Design;
        var top = d.Height + (d.HasCountertop ? d.CountertopThickness : 0);
        const double m = 34;
        var w = Math.Max(10, ActualWidth - 2 * m);
        var h = Math.Max(10, ActualHeight - 2 * m);
        var extX = d.Width + 2 * (d.HasCountertop ? d.CountertopOverhangSides : 0);
        _scale = Math.Min(w / extX, h / top);
        _ox = (ActualWidth - d.Width * _scale) / 2;
        _oy = (ActualHeight + top * _scale) / 2;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = B("TT.Text", "#1F2933");
        var muted = B("TT.Muted", "#5F6B7A");
        var accent = B("TT.Accent", "#B5652B");
        var line = new Pen(Hex("#6B4A33"), 1);
        var thin = new Pen(Hex("#6B4A33"), 0.6);
        var dashed = new Pen(Hex("#6B4A33", 170), 0.8) { DashStyle = DashStyles.Dash };

        // piso
        dc.DrawLine(new Pen(muted, 1.2), P(-d.Width * 0.05, 0), P(d.Width * 1.05, 0));

        // peças vistas de frente (sem fundo e sem caixas de gaveta)
        if (Model != null)
        {
            foreach (var b in Model.Boards.OrderBy(b => b.Role is PanelRole.Door or PanelRole.DrawerFront ? 2 : 1))
            {
                if (b.Role is PanelRole.Back or PanelRole.DrawerSide or PanelRole.DrawerBack or PanelRole.DrawerBottom or PanelRole.DrawerInnerFront) continue;
                var o = b.Placement.Frame.Origin;
                var r = R(o.X, o.Z, o.X + b.Placement.SizeX, o.Z + b.Placement.SizeZ);
                Brush fill = b.Role switch
                {
                    PanelRole.Door or PanelRole.DrawerFront => b.Material.Contains("vidro", StringComparison.OrdinalIgnoreCase) ? Hex("#9EC7D6", 120) : Hex("#C98F5A", 115),
                    PanelRole.Countertop => Hex("#BFBDB8"),
                    PanelRole.Shelf => Hex("#F1EAE1"),
                    PanelRole.Plinth => Hex("#8A8A8A"),
                    _ => Hex("#E8DFD2"),
                };
                dc.DrawRectangle(fill, b.Role is PanelRole.Door or PanelRole.DrawerFront ? line : thin, r);
                if (Clashing.Contains(b.Id)) dc.DrawRectangle(Hex("#D93025", 90), new Pen(Hex("#D93025"), 1.6), r);
            }
            foreach (var a in Model.Accessories)
            {
                var o = a.Placement.Frame.Origin;
                var r = R(o.X, o.Z, o.X + a.Placement.SizeX, o.Z + a.Placement.SizeZ);
                dc.DrawRectangle(Clashing.Contains(a.Id) ? Hex("#D93025") : Hex("#3A3F47"), null, r);
            }
        }

        // símbolos de abertura das portas (convenção de elevação: as linhas apontam para o lado da dobradiça)
        foreach (var c in d.Root.All().Where(c => c.Door != FreeDoor.None))
        {
            var cr = Cells.FirstOrDefault(x => x.Id == c.Id);
            if (cr.Id == null) continue;
            var x0 = d.OverlayFronts ? cr.FX0 : cr.X0;
            var x1 = d.OverlayFronts ? cr.FX1 : cr.X1;
            var z0 = d.OverlayFronts ? cr.FZ0 : cr.Z0;
            var z1 = d.OverlayFronts ? cr.FZ1 : cr.Z1;
            var zm = (z0 + z1) / 2;
            var xm = (x0 + x1) / 2;
            void Hinged(double a, double b, bool hingeLeft)
            {
                var hx = hingeLeft ? a : b;
                var fx = hingeLeft ? b : a;
                dc.DrawLine(dashed, P(fx, z0), P(hx, zm));
                dc.DrawLine(dashed, P(fx, z1), P(hx, zm));
            }
            switch (c.Door)
            {
                case FreeDoor.HingedLeft: Hinged(x0, x1, true); break;
                case FreeDoor.HingedRight: Hinged(x0, x1, false); break;
                case FreeDoor.HingedDouble:
                case FreeDoor.Bifold:
                    Hinged(x0, xm, true);
                    Hinged(xm, x1, c.Door == FreeDoor.Bifold);
                    break;
                case FreeDoor.FlipUp:
                    dc.DrawLine(dashed, P(x0, z0), P(xm, z1));
                    dc.DrawLine(dashed, P(x1, z0), P(xm, z1));
                    break;
                case FreeDoor.FlipDown:
                    dc.DrawLine(dashed, P(x0, z1), P(xm, z0));
                    dc.DrawLine(dashed, P(x1, z1), P(xm, z0));
                    break;
                case FreeDoor.Sliding:
                {
                    var a = P(x0 + (x1 - x0) * 0.2, zm);
                    var b = P(x1 - (x1 - x0) * 0.2, zm);
                    var pen = new Pen(Hex("#6B4A33", 200), 1.2);
                    dc.DrawLine(pen, a, b);
                    dc.DrawLine(pen, a, new Point(a.X + 7, a.Y - 5));
                    dc.DrawLine(pen, a, new Point(a.X + 7, a.Y + 5));
                    dc.DrawLine(pen, b, new Point(b.X - 7, b.Y - 5));
                    dc.DrawLine(pen, b, new Point(b.X - 7, b.Y + 5));
                    break;
                }
            }
        }

        // vãos: passagem do mouse, seleção e medidas
        foreach (var c in Cells.Where(c => c.Leaf))
        {
            var r = R(c.X0, c.Z0, c.X1, c.Z1);
            if (c.Id == _hover && c.Id != Selected) dc.DrawRectangle(Hex("#3B7DD8", 28), null, r);
            if (r.Width > 46 && r.Height > 18)
            {
                var ft = new FormattedText($"{U.ToMm(c.Width):0} × {U.ToMm(c.Height):0}", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, muted, dpi);
                dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
            }
        }
        if (Selected != null && Cells.FirstOrDefault(x => x.Id == Selected) is { Id: not null } s)
        {
            var r = R(s.X0, s.Z0, s.X1, s.Z1);
            var fill = accent.Clone();
            fill.Opacity = 0.16;
            dc.DrawRectangle(fill, new Pen(accent, 2.5), r);
        }

        // medidas gerais
        var dims = new FormattedText(Loc.F("{0} × {1} × {2} mm (L × A × P)", "{0} × {1} × {2} mm (W × H × D)", U.ToMm(d.Width).ToString("0"), U.ToMm(d.Height).ToString("0"), U.ToMm(d.Depth).ToString("0")),
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 11, text, dpi);
        dc.DrawText(dims, new Point(8, 6));
    }

    private string? HitTest(Point p)
    {
        if (_scale <= 0) return null;
        var x = (p.X - _ox) / _scale;
        var z = (_oy - p.Y) / _scale;
        // o vão mais interno que contém o ponto
        return Cells.Where(c => x >= c.X0 && x <= c.X1 && z >= c.Z0 && z <= c.Z1).OrderByDescending(c => c.Level).Select(c => c.Id).FirstOrDefault();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = HitTest(e.GetPosition(this));
        if (h == _hover) return;
        _hover = h;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var h = HitTest(e.GetPosition(this));
        if (h != null) CellClicked?.Invoke(h);
    }
}

// ============================================================ Janela do construtor

internal sealed class FreeDesignerWindow : Window
{
    private readonly ToolContext _ctx;
    private readonly JoineryRules _rules;
    private readonly FreeElevation _elev = new();
    private readonly Preview3D _view3d = new();
    private readonly Stack<string> _undo = new();
    private readonly TextBlock _info;
    private readonly TextBlock _selTitle = UiKit.Text("", 13, true);
    private readonly TextBlock _selInfo = UiKit.Muted("");
    private readonly StackPanel _splitPanel = new();
    private readonly Button _merge, _remove, _parent, _auto;
    private readonly TextBlock _sizeLabel = UiKit.Text("");
    private readonly TextBox _sizeBox = new() { MinWidth = 80 };
    private readonly StackPanel _sizeRow;
    private readonly StackPanel _contentPanel;
    private readonly TextBlock _shelvesText = UiKit.Text("0", 13, true), _drawersText = UiKit.Text("0", 13, true);
    private readonly CheckBox _fixedShelves = new() { Content = Loc.T("Prateleiras fixas (em vez de reguláveis)", "Fixed shelves") };
    private readonly CheckBox _rail = new() { Content = Loc.T("Cabideiro", "Hanging rail") };
    private readonly ComboBox _drawerKind = new();
    private readonly ComboBox _door = new();
    private readonly CheckBox _glass = new() { Content = Loc.T("Porta de vidro (com perfil)", "Glass door (framed)") };
    private readonly TextBox _frontMat = new();
    private bool _updating;
    private CabinetModel? _model;

    private static readonly (FreeDoor Value, string Label)[] DoorLabels =
    {
        (FreeDoor.None, Loc.T("Sem frente (vão aberto)", "No front (open)")),
        (FreeDoor.HingedLeft, Loc.T("Porta de abrir — dobradiça à esquerda", "Hinged door — left")),
        (FreeDoor.HingedRight, Loc.T("Porta de abrir — dobradiça à direita", "Hinged door — right")),
        (FreeDoor.HingedDouble, Loc.T("Duas portas de abrir", "Double hinged doors")),
        (FreeDoor.FlipUp, Loc.T("Basculante (abre para cima)", "Flap (opens up)")),
        (FreeDoor.FlipDown, Loc.T("Escamoteável (abre para baixo)", "Drop-down (opens down)")),
        (FreeDoor.Sliding, Loc.T("Portas de correr", "Sliding doors")),
        (FreeDoor.Bifold, Loc.T("Porta camarão", "Bifold door")),
    };

    public FreeDesign Design { get; private set; }
    public bool FreeForm { get; private set; }
    public string? Selected { get; private set; }

    public FreeDesignerWindow(ToolContext ctx, FreeDesign design, JoineryRules rules, string okText, bool freeForm = false)
    {
        _ctx = ctx;
        _rules = rules;
        Design = design.DeepClone();
        FreeForm = freeForm;
        Selected = Design.Root.Id;
        Title = "Téra Tools — " + Loc.T("Marcenaria livre", "Free joinery");
        Width = 1280;
        Height = 780;
        MinWidth = 900;
        MinHeight = 560;
        Theme.Apply(this, ctx.Owner);

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---------- Barra superior
        var templates = new ComboBox { MinWidth = 260 };
        foreach (var t in FreeDesign.Templates) templates.Items.Add(t);
        templates.SelectedIndex = 0;
        var bar = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        bar.Margin = new Thickness(0, 0, 0, 8);
        bar.Children.Add(UiKit.Col(2,
            UiKit.Title(Loc.T("Marcenaria livre", "Free joinery")),
            UiKit.Muted(Loc.T("Clique num vão para escolher; divida em colunas ou linhas; coloque prateleiras, cabideiro, gavetas e portas. Medidas em mm.",
                "Click a space; split it into columns or rows; add shelves, rail, drawers and doors. Sizes in mm."))).At(0));
        bar.Children.Add(UiKit.Row(6,
            UiKit.Button(Loc.T("Caixa, base e materiais…", "Box, base and materials…"), () => Safe(EditBox), accent: true),
            templates,
            UiKit.Button(Loc.T("Usar modelo", "Use template"), () => Safe(() => UseTemplate(templates.SelectedIndex))),
            UiKit.Button(Loc.T("Desfazer", "Undo"), () => Safe(Undo), tooltip: "Ctrl+Z")).At(1));
        root.Children.Add(bar.At(0, 0));

        // ---------- Corpo: elevação | 3D | propriedades
        var body = UiKit.Grid(UiKit.Star(1.1), UiKit.Star(), UiKit.Px(310));
        var elevBorder = new Border { Child = _elev, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
        elevBorder.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        body.Children.Add(elevBorder.At(0));
        var view3dBorder = new Border { Child = _view3d, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
        view3dBorder.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        body.Children.Add(view3dBorder.At(1));
        _elev.CellClicked += id => Safe(() => Click(id));

        // painel de propriedades do vão
        var panel = new StackPanel();
        panel.Children.Add(_selTitle);
        panel.Children.Add(_selInfo);
        panel.Children.Add(Section(Loc.T("Dividir este vão", "Split this space")));
        _splitPanel.Children.Add(SplitRow(Loc.T("Colunas", "Columns"), FreeSplit.Columns, new[] { 2, 3, 4, 5 }));
        _splitPanel.Children.Add(SplitRow(Loc.T("Linhas", "Rows"), FreeSplit.Rows, new[] { 2, 3, 4, 5, 6 }));
        panel.Children.Add(_splitPanel);
        _merge = UiKit.Button(Loc.T("Juntar (desfazer a divisão)", "Merge (undo split)"), () => Safe(() => Change(c => c.Merge())));
        _remove = UiKit.Button(Loc.T("Remover este vão", "Remove this space"), () => Safe(RemoveCell));
        _parent = UiKit.Button(Loc.T("Escolher o grupo (vão de fora)", "Select the group (outer space)"), () => Safe(SelectParent));
        panel.Children.Add(UiKit.Col(4, _merge, _remove, _parent));

        _sizeRow = UiKit.Col(4);
        panel.Children.Add(Section(Loc.T("Medida", "Size")));
        _auto = UiKit.Button(Loc.T("Automática", "Automatic"), () => Safe(() => Change(c => c.Size = 0)));
        _sizeBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Safe(ApplySize); };
        _sizeBox.LostKeyboardFocus += (_, _) => Safe(ApplySize);
        _sizeRow.Children.Add(_sizeLabel);
        _sizeRow.Children.Add(UiKit.Row(6, _sizeBox, UiKit.Muted("mm", wrap: false), _auto));
        _sizeRow.Children.Add(UiKit.Muted(Loc.T("Vazio ou automática = divide o espaço que sobra igualmente entre os vãos automáticos.", "Empty/automatic = shares the remaining space."), 10.5));
        panel.Children.Add(_sizeRow);

        _contentPanel = UiKit.Col(6);
        _contentPanel.Children.Add(Section(Loc.T("Conteúdo do vão", "Space contents")));
        _contentPanel.Children.Add(Counter(Loc.T("Prateleiras", "Shelves"), _shelvesText, d => Change(c => c.Shelves = Math.Clamp(c.Shelves + d, 0, 20))));
        _contentPanel.Children.Add(_fixedShelves);
        _contentPanel.Children.Add(_rail);
        _contentPanel.Children.Add(Counter(Loc.T("Gavetas", "Drawers"), _drawersText, d => Change(c => c.Drawers = Math.Clamp(c.Drawers + d, 0, 12))));
        _drawerKind.Items.Add(Loc.T("Gaveta com frente aparente", "Drawer with visible front"));
        _drawerKind.Items.Add(Loc.T("Gaveta interna (atrás da porta)", "Internal drawer (behind door)"));
        _contentPanel.Children.Add(_drawerKind);
        panel.Children.Add(_contentPanel);
        _fixedShelves.Checked += (_, _) => Toggle(c => c.FixedShelves = true);
        _fixedShelves.Unchecked += (_, _) => Toggle(c => c.FixedShelves = false);
        _rail.Checked += (_, _) => Toggle(c => c.HangingRail = true);
        _rail.Unchecked += (_, _) => Toggle(c => c.HangingRail = false);
        _drawerKind.SelectionChanged += (_, _) => Toggle(c => c.DrawerKind = _drawerKind.SelectedIndex == 1 ? FreeDrawerKind.Internal : FreeDrawerKind.External);

        panel.Children.Add(Section(Loc.T("Frente (porta) deste vão", "Front (door) of this space")));
        foreach (var (_, label) in DoorLabels) _door.Items.Add(label);
        _door.SelectionChanged += (_, _) => Toggle(c => c.Door = DoorLabels[Math.Max(0, _door.SelectedIndex)].Value);
        panel.Children.Add(_door);
        _glass.Margin = new Thickness(0, 6, 0, 0);
        _glass.Checked += (_, _) => Toggle(c => c.Glass = true);
        _glass.Unchecked += (_, _) => Toggle(c => c.Glass = false);
        panel.Children.Add(_glass);
        panel.Children.Add(UiKit.Muted(Loc.T("Material desta frente (vazio = o das frentes):", "Front material (empty = default):"), 11, margin: new Thickness(0, 6, 0, 2)));
        _frontMat.LostKeyboardFocus += (_, _) => Safe(() =>
        {
            if (_updating || Cell == null || Cell.FrontMaterial == _frontMat.Text.Trim()) return;
            Change(c => c.FrontMaterial = _frontMat.Text.Trim());
        });
        panel.Children.Add(_frontMat);
        panel.Children.Add(UiKit.Muted(Loc.T("Uma frente colocada num grupo cobre todos os vãos dele (ex.: portas de correr no roupeiro inteiro).",
            "A front on a group covers all its spaces."), 10.5, margin: new Thickness(0, 6, 0, 0)));
        body.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(4, 0, 4, 0) }.At(2));
        root.Children.Add(body.At(0, 1));

        _info = UiKit.Muted("", 11.5, margin: new Thickness(0, 8, 0, 0));
        root.Children.Add(_info.At(0, 2));

        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 10, 0, 0);
        footer.Children.Add(UiKit.Muted(Loc.T("Dica: clique de novo no vão escolhido para escolher o grupo em volta dele (para pôr uma porta que cobre vários vãos).",
            "Tip: click the selected space again to select its group."), 11).At(0));
        var ok = UiKit.Button(okText, () => Safe(Accept), accent: true, minWidth: 170);
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => DialogResult = false, minWidth: 90);
        cancel.IsCancel = true;
        footer.Children.Add(UiKit.Row(8, cancel, ok).At(1));
        root.Children.Add(footer.At(0, 3));
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.OriginalSource is not TextBox)
            {
                Safe(Undo);
                e.Handled = true;
            }
        };
        Loaded += (_, _) => Safe(() =>
        {
            Rebuild();
            _view3d.Fit();
        });
    }

    // ---------- utilitários de interface

    private static FrameworkElement Section(string title)
    {
        var t = UiKit.Text(title, 12.5, true, "TT.Accent");
        var b = new Border { Child = t, Margin = new Thickness(0, 14, 0, 6), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 3) };
        b.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        return b;
    }

    private FrameworkElement SplitRow(string label, FreeSplit dir, int[] counts)
    {
        var row = UiKit.Row(4, UiKit.Text(label + ":", margin: new Thickness(0, 4, 4, 4)));
        ((TextBlock)row.Children[0]).MinWidth = 62;
        foreach (var n in counts)
            row.Children.Add(UiKit.Button(n.ToString(CultureInfo.InvariantCulture), () => Safe(() => Change(c => c.SplitInto(dir, n))), minWidth: 34));
        row.Margin = new Thickness(0, 2, 0, 2);
        return row;
    }

    private FrameworkElement Counter(string label, TextBlock value, Action<int> change)
    {
        var minus = UiKit.Button("−", () => Safe(() => change(-1)), minWidth: 32);
        var plus = UiKit.Button("+", () => Safe(() => change(+1)), minWidth: 32);
        value.MinWidth = 28;
        value.TextAlignment = TextAlignment.Center;
        value.VerticalAlignment = VerticalAlignment.Center;
        var l = UiKit.Text(label, margin: new Thickness(0, 4, 8, 4));
        l.MinWidth = 90;
        return UiKit.Row(4, l, minus, value, plus);
    }

    private void Safe(Action a)
    {
        try
        {
            a();
        }
        catch (Exception ex)
        {
            Log.Error("Marcenaria livre", ex);
            _info.Text = Loc.T("Erro: ", "Error: ") + ex.Message;
        }
    }

    private FreeCell? Cell => Selected == null ? null : Design.Root.Find(Selected);

    // ---------- edição

    private void Snapshot()
    {
        _undo.Push(Json.Serialize(Design, typeof(FreeDesign), indented: false));
        if (_undo.Count > 80)
        {
            var keep = _undo.Take(60).Reverse().ToList();
            _undo.Clear();
            foreach (var k in keep) _undo.Push(k);
        }
    }

    private void Change(Action<FreeCell> action)
    {
        if (Cell is not { } c) return;
        Snapshot();
        action(c);
        if (Design.Root.Find(Selected!) == null) Selected = Design.Root.Id;
        Rebuild();
    }

    private void Toggle(Action<FreeCell> action)
    {
        if (_updating) return;
        Safe(() => Change(action));
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        if (Json.Deserialize<FreeDesign>(_undo.Pop()) is { } d)
        {
            Design = d;
            if (Selected == null || Design.Root.Find(Selected) == null) Selected = Design.Root.Id;
            Rebuild();
        }
    }

    private void Click(string id)
    {
        // clicar de novo no vão escolhido sobe para o grupo em volta dele
        if (id == Selected && Design.Root.ParentOf(id) is { } p) Selected = p.Id;
        else Selected = id;
        Rebuild();
    }

    private void SelectParent()
    {
        if (Selected != null && Design.Root.ParentOf(Selected) is { } p)
        {
            Selected = p.Id;
            Rebuild();
        }
    }

    private void RemoveCell()
    {
        if (Selected == null || Design.Root.ParentOf(Selected) is not { } p) return;
        Snapshot();
        p.Children.RemoveAll(c => c.Id == Selected);
        if (p.Children.Count < 2) p.Merge();
        Selected = p.Id;
        Rebuild();
    }

    private void ApplySize()
    {
        if (_updating || Cell is not { } c) return;
        var text = _sizeBox.Text.Trim();
        double size = 0;
        if (text.Length > 0 && !U.TryParseLength(text, LengthUnit.Millimeters, out size))
        {
            _info.Text = Loc.T("Medida inválida.", "Invalid size.");
            return;
        }
        if (Math.Abs(size - c.Size) < 1e-9) return;
        Change(x => x.Size = Math.Max(0, size));
    }

    private void EditBox()
    {
        var form = FreeBoxForm.From(Design, FreeForm);
        var ok = OptionsDialog.Show(_ctx, form, Loc.T("Caixa, base e materiais", "Box, base and materials"),
            Loc.T("Medidas gerais, construção da caixa, base, frentes, puxadores, corrediças, tampo de bancada e materiais.", "Overall sizes, construction and materials."),
            "join.free.box", f =>
            {
                var d = Design.DeepClone();
                f.ApplyTo(d);
                return FreePreview.Model3D(d, _rules);
            }, Loc.T("OK", "OK"));
        if (!ok) return;
        Snapshot();
        form.ApplyTo(Design);
        FreeForm = form.FreeForm;
        Rebuild();
        _view3d.Fit();
    }

    private void UseTemplate(int index)
    {
        if (Design.Root.Children.Count > 0 && !Msg.Confirm(Loc.T("Trocar o desenho atual pelo modelo escolhido? (dá para desfazer)", "Replace the current design with the template? (undo is available)"))) return;
        Snapshot();
        var d = FreeDesign.FromTemplate(index);
        d.Code = Design.Code;
        d.Name = Design.Name;
        d.CarcassMaterial = Design.CarcassMaterial;
        d.FrontMaterial = Design.FrontMaterial;
        d.BackMaterial = Design.BackMaterial;
        d.DrawerMaterial = Design.DrawerMaterial;
        Design = d;
        Selected = Design.Root.Id;
        Rebuild();
        _view3d.Fit();
    }

    private void Accept()
    {
        try
        {
            _model = FreeBuilder.Build(Design, _rules);
        }
        catch (Exception ex)
        {
            Msg.Warn(ex.Message, Loc.T("Marcenaria livre", "Free joinery"));
            return;
        }
        var overlaps = JoineryCheck.Overlaps(_model);
        if (overlaps.Count > 0 && !Msg.Confirm(Loc.F("Há {0} sobreposição(ões) entre peças (em vermelho no desenho):\n\n{1}\n\nGerar mesmo assim?",
                "There are {0} overlap(s) (red in the drawing):\n\n{1}\n\nGenerate anyway?", overlaps.Count, string.Join("\n", overlaps.Take(8).Select(o => "• " + o.Describe()))),
                Loc.T("Marcenaria livre", "Free joinery")))
            return;
        if (string.IsNullOrWhiteSpace(Design.Code))
        {
            Msg.Warn(Loc.T("Informe o código do móvel em \"Caixa, base e materiais\".", "Set the code in \"Box, base and materials\"."));
            return;
        }
        DialogResult = true;
    }

    // ---------- atualização da tela

    private void Rebuild()
    {
        var warnings = new List<string>();
        List<FreeCellRect> cells;
        try
        {
            cells = FreeBuilder.Layout(Design, warnings);
            _model = FreeBuilder.Build(Design, _rules);
        }
        catch (Exception ex)
        {
            _model = null;
            cells = new List<FreeCellRect>();
            warnings.Add(ex.Message);
        }
        var overlaps = _model == null ? new List<JoineryOverlap>() : JoineryCheck.Overlaps(_model);
        var clashing = overlaps.SelectMany(o => new[] { o.A, o.B }).ToHashSet();
        _elev.Design = Design;
        _elev.Model = _model;
        _elev.Cells = cells;
        _elev.Selected = Selected;
        _elev.Clashing = clashing;
        _elev.InvalidateVisual();
        _view3d.Scene = _model == null ? null : FreePreview.Scene(Design, _model, Selected, cells, clashing);

        if (_model != null)
        {
            var area = _model.Boards.Sum(b => b.Area * b.Quantity);
            // sobreposições primeiro: o usuário decide se gera mesmo assim
            var all = overlaps.Select(o => Loc.T("Sobreposição: ", "Overlap: ") + o.Describe())
                .Concat(_model.Warnings).Concat(warnings).Distinct().ToList();
            _info.Text = Loc.F("{0} peças · {1} de chapa · {2} de fita de borda · {3} ferragens", "{0} pieces · {1} of board · {2} of edge band · {3} hardware",
                _model.Boards.Count, U.FormatArea(area), U.FormatM(_model.EdgeBandLength()), _model.Hardware.Sum(h => h.Quantity))
                + (all.Count > 0 ? "   ⚠ " + all[0] + (all.Count > 1 ? Loc.F(" (+{0})", " (+{0})", all.Count - 1) : "") : "");
            _info.ToolTip = all.Count > 1 ? string.Join("\n", all) : null;
        }
        else
        {
            _info.Text = "⚠ " + string.Join(" · ", warnings);
        }
        UpdatePanel(cells);
    }

    private void UpdatePanel(List<FreeCellRect> cells)
    {
        _updating = true;
        try
        {
            var c = Cell;
            var parent = Selected == null ? null : Design.Root.ParentOf(Selected);
            var rect = cells.FirstOrDefault(x => x.Id == Selected);
            if (c == null)
            {
                _selTitle.Text = Loc.T("Nenhum vão escolhido", "No space selected");
                return;
            }
            _selTitle.Text = parent == null ? Loc.T("Vão principal (o móvel inteiro)", "Main space (whole piece)")
                : c.IsLeaf ? Loc.T("Vão escolhido", "Selected space") : Loc.T("Grupo de vãos escolhido", "Selected group of spaces");
            _selInfo.Text = rect.Id == null ? "" : Loc.F("{0} × {1} mm (largura × altura livres){2}", "{0} × {1} mm (clear width × height){2}",
                U.ToMm(rect.Width).ToString("0"), U.ToMm(rect.Height).ToString("0"),
                c.IsLeaf ? "" : Loc.F(" · dividido em {0} {1}", " · split into {0} {1}", c.Children.Count, c.Split == FreeSplit.Columns ? Loc.T("colunas", "columns") : Loc.T("linhas", "rows")));

            _merge.Visibility = c.IsLeaf ? Visibility.Collapsed : Visibility.Visible;
            _remove.Visibility = parent != null && parent.Children.Count > 2 ? Visibility.Visible : Visibility.Collapsed;
            _parent.Visibility = parent != null ? Visibility.Visible : Visibility.Collapsed;

            _sizeRow.Visibility = parent != null ? Visibility.Visible : Visibility.Collapsed;
            if (parent != null)
            {
                _sizeLabel.Text = parent.Split == FreeSplit.Columns ? Loc.T("Largura fixa deste vão", "Fixed width of this space") : Loc.T("Altura fixa deste vão", "Fixed height of this space");
                _sizeBox.Text = c.Size > 0 ? U.ToMm(c.Size).ToString("0.#", Loc.Culture) : "";
            }

            _contentPanel.Visibility = c.IsLeaf ? Visibility.Visible : Visibility.Collapsed;
            _shelvesText.Text = c.Shelves.ToString(CultureInfo.InvariantCulture);
            _drawersText.Text = c.Drawers.ToString(CultureInfo.InvariantCulture);
            _fixedShelves.IsChecked = c.FixedShelves;
            _rail.IsChecked = c.HangingRail;
            _drawerKind.SelectedIndex = c.DrawerKind == FreeDrawerKind.Internal ? 1 : 0;
            _drawerKind.IsEnabled = c.Drawers > 0;

            _door.SelectedIndex = Math.Max(0, Array.FindIndex(DoorLabels, x => x.Value == c.Door));
            _glass.IsChecked = c.Glass;
            _glass.IsEnabled = c.Door != FreeDoor.None;
            _frontMat.Text = c.FrontMaterial;
        }
        finally
        {
            _updating = false;
        }
    }
}

/// <summary>Cenas 3D do móvel livre (no diálogo e no formulário da caixa).</summary>
internal static class FreePreview
{
    public static PreviewModel Model3D(FreeDesign d, JoineryRules rules)
    {
        var pm = new PreviewModel();
        try
        {
            var m = FreeBuilder.Build(d, rules);
            pm.Scene = Scene(d, m, null, FreeBuilder.Layout(d));
            pm.Info = Loc.F("{0} peças", "{0} pieces", m.Boards.Count) + (m.Warnings.Count > 0 ? " · ⚠ " + m.Warnings[0] : "");
        }
        catch (Exception ex)
        {
            pm.Info = ex.Message;
        }
        return pm;
    }

    public static Scene3 Scene(FreeDesign d, CabinetModel m, string? selected, List<FreeCellRect> cells, HashSet<string>? clashing = null)
    {
        var sc = new Scene3();
        sc.Face(Frame3.World, Polygon2.Rectangle(new Vec2(-U.Mm(300), -U.Mm(700)), d.Width + U.Mm(600), d.Depth + U.Mm(1000)), "#D9D4CC", 0.25);
        foreach (var b in m.Boards)
        {
            var p = b.Placement;
            var o = p.Frame.Origin;
            var glass = b.Material.Contains("vidro", StringComparison.OrdinalIgnoreCase);
            var (color, opacity) = b.Role switch
            {
                PanelRole.Door or PanelRole.DrawerFront => glass ? ("#9EC7D6", 0.45) : ("#C98F5A", 0.92),
                PanelRole.Back => ("#EFE9E0", 1.0),
                PanelRole.Countertop => ("#BFBDB8", 1.0),
                PanelRole.Plinth => ("#7A7A7A", 1.0),
                PanelRole.DrawerSide or PanelRole.DrawerBack or PanelRole.DrawerInnerFront or PanelRole.DrawerBottom => ("#D8CCBB", 1.0),
                _ => ("#E8DFD2", 1.0),
            };
            if (clashing != null && clashing.Contains(b.Id)) (color, opacity) = ("#D93025", 1.0);
            sc.Box(new OrientedBox(new Frame3(new Vec3(o.X, o.Y, o.Z + d.Elevation), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), p.SizeX, p.SizeY, p.SizeZ), color, opacity);
        }
        foreach (var a in m.Accessories)
        {
            var p = a.Placement;
            var o = p.Frame.Origin;
            sc.Box(new OrientedBox(new Frame3(new Vec3(o.X, o.Y, o.Z + d.Elevation), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), p.SizeX, p.SizeY, p.SizeZ),
                clashing != null && clashing.Contains(a.Id) ? "#D93025" : "#4A4F57");
        }
        // vão escolhido: moldura na frente
        if (selected != null && cells.FirstOrDefault(c => c.Id == selected) is { Id: not null } s)
        {
            var y = -d.FrontThickness - U.Mm(20);
            var z = d.Elevation;
            var a = new Vec3(s.X0, y, s.Z0 + z);
            var b2 = new Vec3(s.X1, y, s.Z0 + z);
            var c2 = new Vec3(s.X1, y, s.Z1 + z);
            var d2 = new Vec3(s.X0, y, s.Z1 + z);
            sc.Line(a, b2, "#B5652B");
            sc.Line(b2, c2, "#B5652B");
            sc.Line(c2, d2, "#B5652B");
            sc.Line(d2, a, "#B5652B");
        }
        return sc;
    }
}

// ============================================================ Comando

[Transaction(TransactionMode.Manual)]
[Tool("join.free", Modules.Joinery, "Marcenaria livre (construtor)", "Free joinery (builder)", Order = 9, Large = true, Group = "Módulos|Modules",
    DescPt = "Monte qualquer móvel: divida a caixa em vãos (colunas e linhas, com medidas fixas ou automáticas) e coloque prateleiras, cabideiro, gavetas e todo tipo de porta — vendo a elevação e o 3D na hora.",
    DescEn = "Build any piece of furniture: split the box into spaces and add shelves, rails, drawers and every kind of door — with live elevation and 3D.",
    HelpPt = "1. Escolha um modelo para começar ou parta da caixa vazia; em \"Caixa, base e materiais\" ajuste medidas, base (rodapé, pés, suspenso), tampo, fundo, frentes (sobrepostas ou embutidas), puxadores, corrediças, tampo de bancada e materiais.\n2. Clique num vão da elevação e divida-o em colunas ou linhas. Cada vão pode ter largura/altura fixa ou automática.\n3. No vão final, escolha prateleiras (reguláveis ou fixas), cabideiro ou gavetas (com frente aparente ou internas).\n4. Coloque a frente: porta de abrir (esquerda/direita), duas portas, basculante, escamoteável, de correr ou camarão, com ou sem vidro. Clique de novo no vão para escolher o grupo em volta dele e pôr uma frente que cobre vários vãos.\n5. Posicione no projeto: cada peça vira uma família TT_Chapa com medidas de corte, fitas e veio, agrupadas num conjunto.\n\nDepois: Marcenaria ▸ Editar módulo reabre o desenho; Plano de corte, Nesting e Detalhamento funcionam igual aos módulos.",
    HelpEn = "Split the box into spaces, add contents and fronts, then place it. Each piece becomes a TT_Chapa family; edit later with Edit module.",
    Keywords = "marcenaria livre construtor movel armario roupeiro estante gaveta porta correr basculante camarao nicho vao divisoria free joinery builder cabinet wardrobe")]
public sealed class FreeCabinetCommand : ToolCommand
{
    private const string LastKey = "join.free.design";

    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var rules = JoineryGen.Rules(doc);
        var design = LoadLast() ?? FreeDesign.FromTemplate(1);
        design.Code = JoineryUi.NextCode(doc, "MV");
        var w = new FreeDesignerWindow(ctx, design, rules, Loc.T("Posicionar no projeto", "Place in project"));
        if (w.ShowDialog() != true) ctx.Cancel();
        SaveLast(w.Design);
        var mc = JoineryUi.PickPlacement(ctx, w.Design.Depth, out _);
        ctx.Group(ctx.Tool.Name, () =>
        {
            var ids = JoineryGen.Generate(ctx, new FreeRecord { Design = w.Design, FreeForm = w.FreeForm, Assembly = true }, mc, GenSystem.NewId());
            ctx.Report.Created(ids.Count);
            foreach (var id in ids) ctx.Report.AddResult(id.Value);
        });
        AddTables(ctx, FreeBuilder.Build(w.Design, rules));
    }

    internal static void Edit(ToolContext ctx, string systemId, FreeRecord rec, ModuleContext mc)
    {
        var rules = JoineryGen.Rules(ctx.Doc);
        var w = new FreeDesignerWindow(ctx, rec.Design, rules, Loc.T("Atualizar móvel", "Update furniture"), rec.FreeForm);
        if (w.ShowDialog() != true) ctx.Cancel();
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover versão anterior", "Remove previous version"), () => JoineryUi.DeleteModule(ctx.Doc, systemId));
            var ids = JoineryGen.Generate(ctx, new FreeRecord { Design = w.Design, FreeForm = w.FreeForm, Assembly = rec.Assembly }, mc, systemId);
            ctx.Report.Modified();
            foreach (var id in ids) ctx.Report.AddResult(id.Value);
        });
        AddTables(ctx, FreeBuilder.Build(w.Design, rules));
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var rec = Json.Deserialize<FreeRecord>(optionsJson) ?? throw new ToolException(Loc.T("Registro do móvel inválido.", "Invalid record."));
        var mc = Json.Deserialize<ModuleContext>(contextJson) ?? throw new ToolException(Loc.T("Posição do móvel inválida.", "Invalid position."));
        Edit(ctx, systemId, rec, mc);
    }

    /// <summary>Lista de peças e de ferragens no relatório (exportável para Excel).</summary>
    private static void AddTables(ToolContext ctx, CabinetModel m)
    {
        var t = new TableData(Loc.F("Peças — {0}", "Pieces — {0}", m.Spec.Code));
        t.AddColumn(Loc.T("Peça", "Piece")).AddColumn(Loc.T("Nome", "Name")).AddColumn(Loc.T("Material", "Material"))
         .AddColumn(Loc.T("Comprimento (mm)", "Length (mm)"), ColumnKind.Number, 0).AddColumn(Loc.T("Largura (mm)", "Width (mm)"), ColumnKind.Number, 0)
         .AddColumn(Loc.T("Espessura (mm)", "Thickness (mm)"), ColumnKind.Number, 0).AddColumn(Loc.T("Fitas", "Edges")).AddColumn(Loc.T("Veio", "Grain"));
        foreach (var b in m.Boards)
            t.AddRow(b.Id, b.Name, b.Material, U.ToMm(b.Length), U.ToMm(b.Width), U.ToMm(b.Thickness), b.EdgeCode.Trim(), b.HasGrain ? Loc.T("sim", "yes") : "");
        ctx.Report.Tables.Add(t);
        var h = new TableData(Loc.F("Ferragens — {0}", "Hardware — {0}", m.Spec.Code));
        h.AddColumn(Loc.T("Item", "Item")).AddColumn(Loc.T("Especificação", "Specification")).AddColumn(Loc.T("Quantidade", "Quantity"), ColumnKind.Integer)
         .AddColumn(Loc.T("Comprimento total (m)", "Total length (m)"), ColumnKind.Number);
        foreach (var x in m.Hardware) h.AddRow(x.Name, x.Spec, x.Quantity, x.Length > 0 ? U.ToM(x.Length) : null);
        ctx.Report.Tables.Add(h);
        ctx.Report.ForceShow = true;
    }

    private static FreeDesign? LoadLast()
    {
        try
        {
            var json = Services.Presets.GetLastUsed(LastKey);
            return string.IsNullOrWhiteSpace(json) ? null : Json.Deserialize<FreeDesign>(json);
        }
        catch (Exception ex)
        {
            Log.Warn("Último móvel livre: " + ex.Message);
            return null;
        }
    }

    private static void SaveLast(FreeDesign d)
    {
        try
        {
            Services.Presets.SetLastUsed(LastKey, Json.Serialize(d, typeof(FreeDesign), indented: false));
            Services.SaveSettings();
        }
        catch (Exception ex)
        {
            Log.Warn("Salvar móvel livre: " + ex.Message);
        }
    }
}
