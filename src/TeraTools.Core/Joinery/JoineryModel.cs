using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

public enum CabinetKind
{
    Base,
    Wall,
    Tall,
    OpenNiche,
    ClosedNiche,
    Wardrobe,
    Vanity,
    Counter,
    Island,
    TvPanel,
    Bookcase,
    Custom,
}

public enum DoorKind
{
    None,
    Hinged,
    HingedDouble,
    Sliding,
    FlipUp,
}

public enum HandleKind
{
    None,
    Bar,
    Knob,
    Profile,
    Cava,
    PushOpen,
}

public enum BackKind
{
    None,
    Inset,
    Overlay,
}

public enum TopKind
{
    Full,
    Rails,
    None,
}

public enum DrawerZone
{
    Top,
    Bottom,
}

public enum PanelRole
{
    Side,
    Bottom,
    Top,
    Rail,
    Back,
    Shelf,
    FixedShelf,
    Divider,
    Door,
    DrawerFront,
    DrawerSide,
    DrawerBack,
    DrawerInnerFront,
    DrawerBottom,
    Plinth,
    Countertop,
    Panel,
    Filler,
}

public enum HardwareKind
{
    Hinge,
    Slide,
    Handle,
    ShelfSupport,
    Foot,
    Connector,
    SlidingTrack,
    SlidingRoller,
    Lift,
    HangingRail,
    PushLatch,
    Damper,
}

/// <summary>Especificação de um módulo de marcenaria (pés internamente; valores padrão em mm).</summary>
public sealed class CabinetSpec
{
    public string Code { get; set; } = "M1";
    public string Name { get; set; } = "";
    public CabinetKind Kind { get; set; } = CabinetKind.Base;

    public double Width { get; set; } = U.Mm(800);
    public double Height { get; set; } = U.Mm(870);
    public double Depth { get; set; } = U.Mm(550);
    public double Elevation { get; set; }

    public double Thickness { get; set; } = U.Mm(15);
    public double FrontThickness { get; set; } = U.Mm(18);
    public double BackThickness { get; set; } = U.Mm(6);
    public BackKind Back { get; set; } = BackKind.Inset;
    public double BackGroove { get; set; } = U.Mm(8);
    public double BackRecess { get; set; } = U.Mm(15);

    public TopKind Top { get; set; } = TopKind.Rails;
    public double RailWidth { get; set; } = U.Mm(100);

    public bool HasPlinth { get; set; } = true;
    public double PlinthHeight { get; set; } = U.Mm(100);
    public double PlinthSetback { get; set; } = U.Mm(50);

    public int Shelves { get; set; } = 1;
    public bool AdjustableShelves { get; set; } = true;
    public double ShelfSetback { get; set; } = U.Mm(20);
    public int Dividers { get; set; }

    public int Drawers { get; set; }
    public DrawerZone DrawerZone { get; set; } = DrawerZone.Top;
    public double DrawerFrontHeight { get; set; } = U.Mm(180);
    public double DrawerSideThickness { get; set; } = U.Mm(15);
    public double DrawerBottomThickness { get; set; } = U.Mm(6);

    public DoorKind Doors { get; set; } = DoorKind.Hinged;
    public int DoorCount { get; set; }
    public double FrontGap { get; set; } = U.Mm(3);
    public double SlidingOverlap { get; set; } = U.Mm(30);

    public HandleKind Handle { get; set; } = HandleKind.Bar;
    public double HandleLength { get; set; } = U.Mm(160);
    public bool SoftClose { get; set; } = true;

    public bool HasCountertop { get; set; }
    public double CountertopThickness { get; set; } = U.Mm(20);
    public double CountertopOverhangFront { get; set; } = U.Mm(20);
    public double CountertopOverhangBack { get; set; }
    public double CountertopOverhangSides { get; set; }

    public bool HangingRail { get; set; }

    public string CarcassMaterial { get; set; } = "MDF Branco TX 15";
    public string FrontMaterial { get; set; } = "MDF Amadeirado 18";
    public string BackMaterial { get; set; } = "MDF Branco 6";
    public string DrawerMaterial { get; set; } = "MDF Branco TX 15";
    public string CountertopMaterial { get; set; } = "Quartzo Branco 20";

    public bool CarcassHasGrain { get; set; }
    public bool FrontHasGrain { get; set; } = true;

    /// <summary>Aplica valores típicos para o tipo de módulo (altura, profundidade, portas...).</summary>
    public static CabinetSpec Defaults(CabinetKind kind)
    {
        var s = new CabinetSpec { Kind = kind };
        switch (kind)
        {
            case CabinetKind.Wall:
                s.Height = U.Mm(700); s.Depth = U.Mm(350); s.HasPlinth = false; s.Top = TopKind.Full;
                s.Elevation = U.Mm(1500); s.Shelves = 1;
                break;
            case CabinetKind.Tall:
                s.Height = U.Mm(2200); s.Depth = U.Mm(580); s.Top = TopKind.Full; s.Shelves = 4;
                break;
            case CabinetKind.OpenNiche:
                s.Width = U.Mm(600); s.Height = U.Mm(400); s.Depth = U.Mm(300); s.HasPlinth = false;
                s.Top = TopKind.Full; s.Doors = DoorKind.None; s.Shelves = 0; s.Elevation = U.Mm(1400); s.Handle = HandleKind.None;
                s.Back = BackKind.Overlay;
                break;
            case CabinetKind.ClosedNiche:
                s.Width = U.Mm(600); s.Height = U.Mm(400); s.Depth = U.Mm(350); s.HasPlinth = false;
                s.Top = TopKind.Full; s.Doors = DoorKind.FlipUp; s.Shelves = 0; s.Elevation = U.Mm(1400);
                break;
            case CabinetKind.Wardrobe:
                s.Width = U.Mm(1800); s.Height = U.Mm(2500); s.Depth = U.Mm(600); s.Top = TopKind.Full;
                s.Doors = DoorKind.Sliding; s.DoorCount = 2; s.Dividers = 1; s.Shelves = 2; s.HangingRail = true;
                break;
            case CabinetKind.Vanity:
                s.Width = U.Mm(800); s.Height = U.Mm(550); s.Depth = U.Mm(480); s.HasPlinth = false;
                s.Elevation = U.Mm(250); s.Drawers = 2; s.Doors = DoorKind.None; s.Shelves = 0; s.HasCountertop = true;
                s.Handle = HandleKind.Cava;
                break;
            case CabinetKind.Counter:
                s.HasCountertop = true; s.Shelves = 1;
                break;
            case CabinetKind.Island:
                s.Width = U.Mm(1800); s.Depth = U.Mm(600); s.HasCountertop = true; s.CountertopOverhangBack = U.Mm(300);
                s.CountertopOverhangSides = U.Mm(20); s.Drawers = 3; s.Dividers = 1;
                break;
            case CabinetKind.TvPanel:
                s.Width = U.Mm(2200); s.Height = U.Mm(1800); s.Depth = U.Mm(250); s.HasPlinth = false;
                s.Doors = DoorKind.None; s.Shelves = 1; s.Thickness = U.Mm(18); s.Handle = HandleKind.None;
                s.Elevation = U.Mm(300); s.Back = BackKind.None; s.Top = TopKind.None;
                break;
            case CabinetKind.Bookcase:
                s.Width = U.Mm(1200); s.Height = U.Mm(2000); s.Depth = U.Mm(350); s.Top = TopKind.Full;
                s.Doors = DoorKind.None; s.Shelves = 4; s.Dividers = 1; s.Handle = HandleKind.None; s.AdjustableShelves = false;
                s.Back = BackKind.Overlay;
                break;
        }
        return s;
    }
}

/// <summary>Uma peça (chapa) do móvel, com dimensões de corte e posição no móvel.</summary>
public sealed class Board
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public PanelRole Role { get; set; }

    /// <summary>Dimensão no sentido do veio (ou a maior, sem veio).</summary>
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public string Material { get; set; } = "";
    public bool HasGrain { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>Fitas de borda: L1/L2 = bordas longas (ao longo do comprimento), C1/C2 = bordas curtas.</summary>
    public bool EdgeL1 { get; set; }
    public bool EdgeL2 { get; set; }
    public bool EdgeC1 { get; set; }
    public bool EdgeC2 { get; set; }

    /// <summary>Caixa no sistema local do móvel (X largura, Y profundidade a partir da frente, Z altura).</summary>
    public OrientedBox Placement { get; set; }

    public double Area => Length * Width;

    public double EdgeLength =>
        (EdgeL1 ? Length : 0) + (EdgeL2 ? Length : 0) + (EdgeC1 ? Width : 0) + (EdgeC2 ? Width : 0);

    public string EdgeCode =>
        (EdgeL1 ? "L1 " : "") + (EdgeL2 ? "L2 " : "") + (EdgeC1 ? "C1 " : "") + (EdgeC2 ? "C2" : "");
}

public sealed class HardwareItem
{
    public HardwareKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Spec { get; set; } = "";
    public int Quantity { get; set; }
    public double Length { get; set; }
    public string Unit { get; set; } = "un";
}

public sealed class CabinetModel
{
    public CabinetSpec Spec { get; set; } = new();
    public List<Board> Boards { get; } = new();
    public List<HardwareItem> Hardware { get; } = new();
    public List<string> Warnings { get; } = new();

    public double EdgeBandLength(string? material = null) =>
        Boards.Where(b => material == null || b.Material == material).Sum(b => b.EdgeLength * b.Quantity);

    public IEnumerable<(string Material, double Thickness, double Area, int Count)> SheetUsage() =>
        Boards.GroupBy(b => (b.Material, T: Math.Round(U.ToMm(b.Thickness), 1)))
              .Select(g => (g.Key.Material, U.Mm(g.Key.T), g.Sum(b => b.Area * b.Quantity), g.Sum(b => b.Quantity)));
}

/// <summary>Regras de ferragens e construção, editáveis pelo usuário.</summary>
public sealed class JoineryRules
{
    /// <summary>Quantidade de dobradiças conforme a altura da porta (até a altura indicada, em mm).</summary>
    public List<HingeRule> Hinges { get; set; } = new()
    {
        new HingeRule { MaxHeightMm = 900, Count = 2 },
        new HingeRule { MaxHeightMm = 1500, Count = 3 },
        new HingeRule { MaxHeightMm = 2100, Count = 4 },
        new HingeRule { MaxHeightMm = 99999, Count = 5 },
    };

    /// <summary>Comprimentos comerciais de corrediças (mm).</summary>
    public List<double> SlideLengthsMm { get; set; } = new() { 250, 300, 350, 400, 450, 500, 550 };

    public double SlideSideClearanceMm { get; set; } = 12.7;
    public double DrawerRearClearanceMm { get; set; } = 20;
    public double DrawerBoxHeightReductionMm { get; set; } = 50;
    public int ShelfSupportsPerShelf { get; set; } = 4;
    public double MaxShelfSpanMm { get; set; } = 900;
    public double FootSpacingMaxMm { get; set; } = 800;
    public int ConnectorsPerJoint { get; set; } = 2;
    public double EdgeBandWastePercent { get; set; } = 10;
    public double MinDoorWidthMm { get; set; } = 200;
    public double MaxSingleDoorWidthMm { get; set; } = 600;

    public int HingesFor(double doorHeightFt)
    {
        var mm = U.ToMm(doorHeightFt);
        foreach (var r in Hinges.OrderBy(h => h.MaxHeightMm))
            if (mm <= r.MaxHeightMm) return r.Count;
        return Hinges.Count > 0 ? Hinges.Max(h => h.Count) : 2;
    }

    /// <summary>Maior corrediça comercial que cabe na profundidade disponível (pés). 0 se nenhuma.</summary>
    public double SlideFor(double availableDepthFt)
    {
        var avail = U.ToMm(availableDepthFt) - DrawerRearClearanceMm;
        var fit = SlideLengthsMm.Where(l => l <= avail + 1e-6).DefaultIfEmpty(0).Max();
        return U.Mm(fit);
    }
}

public sealed class HingeRule
{
    public double MaxHeightMm { get; set; }
    public int Count { get; set; }
}

public static class JoineryNames
{
    public static string Role(PanelRole r) => r switch
    {
        PanelRole.Side => Loc.T("Lateral", "Side"),
        PanelRole.Bottom => Loc.T("Base", "Bottom"),
        PanelRole.Top => Loc.T("Tampo", "Top"),
        PanelRole.Rail => Loc.T("Travessa", "Rail"),
        PanelRole.Back => Loc.T("Fundo", "Back"),
        PanelRole.Shelf => Loc.T("Prateleira", "Shelf"),
        PanelRole.FixedShelf => Loc.T("Prateleira fixa", "Fixed shelf"),
        PanelRole.Divider => Loc.T("Divisória", "Divider"),
        PanelRole.Door => Loc.T("Porta", "Door"),
        PanelRole.DrawerFront => Loc.T("Frente de gaveta", "Drawer front"),
        PanelRole.DrawerSide => Loc.T("Lateral de gaveta", "Drawer side"),
        PanelRole.DrawerBack => Loc.T("Traseira de gaveta", "Drawer back"),
        PanelRole.DrawerInnerFront => Loc.T("Contrafrente de gaveta", "Drawer inner front"),
        PanelRole.DrawerBottom => Loc.T("Fundo de gaveta", "Drawer bottom"),
        PanelRole.Plinth => Loc.T("Rodapé", "Plinth"),
        PanelRole.Countertop => Loc.T("Tampo de bancada", "Countertop"),
        PanelRole.Panel => Loc.T("Painel", "Panel"),
        _ => Loc.T("Complemento", "Filler"),
    };

    public static string Abbrev(PanelRole r) => r switch
    {
        PanelRole.Side => "LAT",
        PanelRole.Bottom => "BAS",
        PanelRole.Top => "TAM",
        PanelRole.Rail => "TRV",
        PanelRole.Back => "FUN",
        PanelRole.Shelf => "PRA",
        PanelRole.FixedShelf => "PRF",
        PanelRole.Divider => "DIV",
        PanelRole.Door => "POR",
        PanelRole.DrawerFront => "FGV",
        PanelRole.DrawerSide => "LGV",
        PanelRole.DrawerBack => "TGV",
        PanelRole.DrawerInnerFront => "CFG",
        PanelRole.DrawerBottom => "FDG",
        PanelRole.Plinth => "ROD",
        PanelRole.Countertop => "TPB",
        PanelRole.Panel => "PNL",
        _ => "CPL",
    };

    public static string Hardware(HardwareKind k) => k switch
    {
        HardwareKind.Hinge => Loc.T("Dobradiça", "Hinge"),
        HardwareKind.Slide => Loc.T("Corrediça (par)", "Drawer slide (pair)"),
        HardwareKind.Handle => Loc.T("Puxador", "Handle"),
        HardwareKind.ShelfSupport => Loc.T("Suporte de prateleira", "Shelf support"),
        HardwareKind.Foot => Loc.T("Pé regulável", "Adjustable foot"),
        HardwareKind.Connector => Loc.T("Conector (minifix/cavilha)", "Connector (cam/dowel)"),
        HardwareKind.SlidingTrack => Loc.T("Trilho de correr", "Sliding track"),
        HardwareKind.SlidingRoller => Loc.T("Roldana / carrinho", "Sliding roller"),
        HardwareKind.Lift => Loc.T("Articulador / pistão", "Lift / gas strut"),
        HardwareKind.HangingRail => Loc.T("Cabideiro", "Hanging rail"),
        HardwareKind.PushLatch => Loc.T("Fecho toque", "Push latch"),
        _ => Loc.T("Amortecedor", "Damper"),
    };

    public static string Kind(CabinetKind k) => k switch
    {
        CabinetKind.Base => Loc.T("Armário inferior", "Base cabinet"),
        CabinetKind.Wall => Loc.T("Armário superior", "Wall cabinet"),
        CabinetKind.Tall => Loc.T("Torre / coluna", "Tall cabinet"),
        CabinetKind.OpenNiche => Loc.T("Nicho aberto", "Open niche"),
        CabinetKind.ClosedNiche => Loc.T("Nicho fechado", "Closed niche"),
        CabinetKind.Wardrobe => Loc.T("Roupeiro / closet", "Wardrobe"),
        CabinetKind.Vanity => Loc.T("Gabinete de banheiro", "Bathroom vanity"),
        CabinetKind.Counter => Loc.T("Bancada", "Counter"),
        CabinetKind.Island => Loc.T("Ilha", "Island"),
        CabinetKind.TvPanel => Loc.T("Painel de TV", "TV panel"),
        CabinetKind.Bookcase => Loc.T("Estante / biblioteca", "Bookcase"),
        _ => Loc.T("Módulo personalizado", "Custom module"),
    };
}
