using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

/// <summary>Como um vão é dividido.</summary>
public enum FreeSplit
{
    None,
    /// <summary>Em colunas, com divisórias verticais.</summary>
    Columns,
    /// <summary>Em linhas (de cima para baixo), com prateleiras fixas.</summary>
    Rows,
}

/// <summary>Frente que cobre um vão (ou um grupo de vãos).</summary>
public enum FreeDoor
{
    None,
    HingedLeft,
    HingedRight,
    HingedDouble,
    FlipUp,
    FlipDown,
    Sliding,
    Bifold,
}

public enum FreeDrawerKind
{
    /// <summary>Frente aparente (fecha o vão).</summary>
    External,
    /// <summary>Gaveta interna, atrás de uma porta.</summary>
    Internal,
}

public enum FreeBase
{
    /// <summary>Rodapé recuado com pés reguláveis.</summary>
    Plinth,
    /// <summary>Pés aparentes (sem rodapé).</summary>
    Feet,
    /// <summary>Suspenso (fixado na parede).</summary>
    Suspended,
    /// <summary>Apoiado direto no piso.</summary>
    Floor,
}

public enum SlideKind
{
    Telescopic,
    Hidden,
    Roller,
}

/// <summary>
/// Um vão do móvel. Pode ser dividido (colunas/linhas) em vãos menores; o vão final recebe o conteúdo
/// (prateleiras, cabideiro, gavetas). Qualquer vão — final ou um grupo — pode receber uma frente.
/// Medidas em pés (unidade interna); a interface mostra em mm.
/// </summary>
public sealed class FreeCell
{
    public string Id { get; set; } = NewId();
    public FreeSplit Split { get; set; } = FreeSplit.None;
    public List<FreeCell> Children { get; set; } = new();

    /// <summary>Medida fixa ao longo da divisão do vão pai (largura numa coluna, altura numa linha). 0 = automática.</summary>
    public double Size { get; set; }

    public int Shelves { get; set; }
    public bool FixedShelves { get; set; }
    public bool HangingRail { get; set; }
    public int Drawers { get; set; }
    public FreeDrawerKind DrawerKind { get; set; } = FreeDrawerKind.External;

    public FreeDoor Door { get; set; } = FreeDoor.None;
    public bool Glass { get; set; }

    /// <summary>Material da frente deste vão (vazio = o material de frentes do móvel).</summary>
    public string FrontMaterial { get; set; } = "";

    public bool IsLeaf => Split == FreeSplit.None || Children.Count == 0;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    public IEnumerable<FreeCell> All()
    {
        yield return this;
        foreach (var c in Children)
            foreach (var d in c.All())
                yield return d;
    }

    public FreeCell? Find(string id) => All().FirstOrDefault(c => c.Id == id);

    public FreeCell? ParentOf(string id) => All().FirstOrDefault(c => c.Children.Any(x => x.Id == id));

    /// <summary>Divide o vão em <paramref name="n"/> partes iguais. O conteúdo vai para a primeira parte; a frente continua no vão.</summary>
    public void SplitInto(FreeSplit dir, int n)
    {
        n = Math.Clamp(n, 2, 20);
        var first = new FreeCell
        {
            Shelves = Shelves, FixedShelves = FixedShelves, HangingRail = HangingRail, Drawers = Drawers, DrawerKind = DrawerKind,
        };
        Shelves = 0;
        HangingRail = false;
        Drawers = 0;
        Split = dir;
        Children = new List<FreeCell> { first };
        for (var i = 1; i < n; i++) Children.Add(new FreeCell());
    }

    /// <summary>Desfaz a divisão (os vãos internos são apagados).</summary>
    public void Merge()
    {
        Split = FreeSplit.None;
        Children.Clear();
    }

    public FreeCell DeepClone()
    {
        var c = (FreeCell)MemberwiseClone();
        c.Children = Children.Select(x => x.DeepClone()).ToList();
        return c;
    }
}

/// <summary>Móvel livre: caixa + árvore de vãos.</summary>
public sealed class FreeDesign
{
    public string Code { get; set; } = "MV1";
    public string Name { get; set; } = "";

    public double Width { get; set; } = U.Mm(1200);
    public double Height { get; set; } = U.Mm(2100);
    public double Depth { get; set; } = U.Mm(550);
    public double Elevation { get; set; }

    public double Thickness { get; set; } = U.Mm(15);
    public double FrontThickness { get; set; } = U.Mm(18);
    public double BackThickness { get; set; } = U.Mm(6);
    public BackKind Back { get; set; } = BackKind.Inset;
    public double BackGroove { get; set; } = U.Mm(8);
    public double BackRecess { get; set; } = U.Mm(15);

    public FreeBase Base { get; set; } = FreeBase.Plinth;
    public double PlinthHeight { get; set; } = U.Mm(100);
    public double PlinthSetback { get; set; } = U.Mm(50);
    public double FeetHeight { get; set; } = U.Mm(150);

    public TopKind Top { get; set; } = TopKind.Full;
    public double RailWidth { get; set; } = U.Mm(100);

    /// <summary>Frentes sobrepostas (cobrem a caixa) ou embutidas (dentro dos vãos, rente à caixa).</summary>
    public bool OverlayFronts { get; set; } = true;
    public double FrontGap { get; set; } = U.Mm(3);
    public double SlidingOverlap { get; set; } = U.Mm(30);

    public HandleKind Handle { get; set; } = HandleKind.Bar;
    public double HandleLength { get; set; } = U.Mm(160);
    public bool SoftClose { get; set; } = true;
    public SlideKind Slides { get; set; } = SlideKind.Telescopic;
    public double ShelfSetback { get; set; } = U.Mm(20);

    public bool HasCountertop { get; set; }
    public double CountertopThickness { get; set; } = U.Mm(20);
    public double CountertopOverhangFront { get; set; } = U.Mm(20);
    public double CountertopOverhangBack { get; set; }
    public double CountertopOverhangSides { get; set; }

    public string CarcassMaterial { get; set; } = "MDF Branco TX 15";
    public string FrontMaterial { get; set; } = "MDF Amadeirado 18";
    public string BackMaterial { get; set; } = "MDF Branco 6";
    public string DrawerMaterial { get; set; } = "MDF Branco TX 15";
    public string CountertopMaterial { get; set; } = "Quartzo Branco 20";
    public string GlassMaterial { get; set; } = "Vidro incolor 4 mm";
    public bool CarcassHasGrain { get; set; }
    public bool FrontHasGrain { get; set; } = true;

    public FreeCell Root { get; set; } = new();

    public FreeDesign DeepClone()
    {
        var d = (FreeDesign)MemberwiseClone();
        d.Root = Root.DeepClone();
        return d;
    }

    /// <summary>Altura onde começa a caixa (acima do rodapé ou dos pés).</summary>
    public double CarcassBottom => Base switch
    {
        FreeBase.Plinth => PlinthHeight,
        FreeBase.Feet => FeetHeight,
        _ => 0,
    };

    // ------------------------------------------------------------------ Modelos para começar

    public static readonly string[] Templates =
    {
        "Só a caixa (vazio)",
        "Armário inferior com gavetas e portas",
        "Armário aéreo",
        "Roupeiro com portas de correr",
        "Estante com nichos",
        "Rack / painel baixo",
        "Torre de forno e micro-ondas",
    };

    public static FreeDesign FromTemplate(int index)
    {
        var d = new FreeDesign();
        var r = d.Root;
        switch (index)
        {
            case 1:
                d.Width = U.Mm(1200); d.Height = U.Mm(870); d.Depth = U.Mm(580); d.Top = TopKind.Rails; d.HasCountertop = true;
                r.SplitInto(FreeSplit.Columns, 2);
                r.Children[0].Size = U.Mm(500);
                r.Children[0].Drawers = 3;
                r.Children[1].Door = FreeDoor.HingedDouble;
                r.Children[1].Shelves = 1;
                break;
            case 2:
                d.Width = U.Mm(1200); d.Height = U.Mm(700); d.Depth = U.Mm(350); d.Base = FreeBase.Suspended; d.Elevation = U.Mm(1500);
                r.SplitInto(FreeSplit.Columns, 2);
                r.Children[0].Door = FreeDoor.HingedLeft;
                r.Children[1].Door = FreeDoor.HingedRight;
                foreach (var c in r.Children) c.Shelves = 1;
                break;
            case 3:
                d.Width = U.Mm(2000); d.Height = U.Mm(2500); d.Depth = U.Mm(620);
                r.Door = FreeDoor.Sliding;
                r.SplitInto(FreeSplit.Columns, 3);
                foreach (var col in r.Children)
                {
                    col.SplitInto(FreeSplit.Rows, 2);
                    col.Children[0].Size = U.Mm(400); // maleiro
                }
                r.Children[0].Children[1].HangingRail = true;
                r.Children[1].Children[1].Shelves = 5;
                r.Children[2].Children[1].SplitInto(FreeSplit.Rows, 2);
                r.Children[2].Children[1].Children[0].HangingRail = true;
                r.Children[2].Children[1].Children[1].Size = U.Mm(600);
                r.Children[2].Children[1].Children[1].Drawers = 3;
                r.Children[2].Children[1].Children[1].DrawerKind = FreeDrawerKind.Internal;
                break;
            case 4:
                d.Width = U.Mm(1600); d.Height = U.Mm(2000); d.Depth = U.Mm(350); d.Base = FreeBase.Floor; d.Back = BackKind.Overlay;
                r.SplitInto(FreeSplit.Columns, 3);
                foreach (var col in r.Children) col.SplitInto(FreeSplit.Rows, 5);
                break;
            case 5:
                d.Width = U.Mm(2000); d.Height = U.Mm(500); d.Depth = U.Mm(450); d.Base = FreeBase.Suspended; d.Elevation = U.Mm(300);
                d.Handle = HandleKind.PushOpen;
                r.SplitInto(FreeSplit.Columns, 3);
                r.Children[0].Door = FreeDoor.FlipDown;
                r.Children[2].Door = FreeDoor.FlipDown;
                break;
            case 6:
                d.Width = U.Mm(600); d.Height = U.Mm(2200); d.Depth = U.Mm(580);
                r.SplitInto(FreeSplit.Rows, 5);
                r.Children[0].Size = U.Mm(400); r.Children[0].Door = FreeDoor.FlipUp;
                r.Children[1].Size = U.Mm(400); // micro-ondas
                r.Children[2].Size = U.Mm(600); // forno
                r.Children[3].Size = U.Mm(300); r.Children[3].Drawers = 1;
                r.Children[4].Door = FreeDoor.HingedLeft; r.Children[4].Shelves = 1;
                break;
        }
        return d;
    }
}

/// <summary>Retângulo de um vão (interior) e da área de frente que o cobre (antes das folgas), na vista frontal.</summary>
public readonly record struct FreeCellRect(string Id, double X0, double X1, double Z0, double Z1, double FX0, double FX1, double FZ0, double FZ1, bool Leaf, int Level)
{
    public double Width => X1 - X0;
    public double Height => Z1 - Z0;
}

/// <summary>Gera chapas, frentes e ferragens do móvel livre (mesmo modelo dos módulos: plano de corte, ferragens e nesting funcionam igual).</summary>
public static class FreeBuilder
{
    private enum Axis { X, Y, Z }

    /// <summary>Medidas de cada vão (para desenhar a elevação e clicar nos vãos).</summary>
    public static List<FreeCellRect> Layout(FreeDesign d, List<string>? warnings = null)
    {
        var list = new List<FreeCellRect>();
        var (ix0, ix1, iz0, iz1) = Interior(d);
        var t = d.Thickness;
        void Walk(FreeCell c, double x0, double x1, double z0, double z1, double fx0, double fx1, double fz0, double fz1, int level)
        {
            list.Add(new FreeCellRect(c.Id, x0, x1, z0, z1, fx0, fx1, fz0, fz1, c.IsLeaf, level));
            if (c.IsLeaf) return;
            var span = c.Split == FreeSplit.Columns ? x1 - x0 : z1 - z0;
            var sizes = Sizes(c, span, t, warnings);
            if (c.Split == FreeSplit.Columns)
            {
                var x = x0;
                for (var i = 0; i < c.Children.Count; i++)
                {
                    var a = x;
                    var b = x + sizes[i];
                    var fa = i == 0 ? fx0 : a - t / 2;
                    var fb = i == c.Children.Count - 1 ? fx1 : b + t / 2;
                    Walk(c.Children[i], a, b, z0, z1, fa, fb, fz0, fz1, level + 1);
                    x = b + t;
                }
            }
            else
            {
                var z = z1; // linhas de cima para baixo
                for (var i = 0; i < c.Children.Count; i++)
                {
                    var b = z;
                    var a = z - sizes[i];
                    var fb = i == 0 ? fz1 : b + t / 2;
                    var fa = i == c.Children.Count - 1 ? fz0 : a - t / 2;
                    Walk(c.Children[i], x0, x1, a, b, fx0, fx1, fa, fb, level + 1);
                    z = a - t;
                }
            }
        }
        Walk(d.Root, ix0, ix1, iz0, iz1, 0, d.Width, d.CarcassBottom, d.Height, 0);
        return list;
    }

    /// <summary>Vão interno da caixa: entre as laterais, acima da base e abaixo do tampo.</summary>
    public static (double X0, double X1, double Z0, double Z1) Interior(FreeDesign d)
    {
        var t = d.Thickness;
        var topT = d.Top == TopKind.None ? 0 : t;
        return (t, d.Width - t, d.CarcassBottom + t, d.Height - topT);
    }

    /// <summary>Medidas dos vãos filhos: fixas onde indicadas; o restante dividido igualmente.</summary>
    private static double[] Sizes(FreeCell c, double span, double t, List<string>? warnings)
    {
        var n = c.Children.Count;
        var avail = span - (n - 1) * t;
        var sizes = new double[n];
        var fixedSum = c.Children.Where(x => x.Size > 0).Sum(x => x.Size);
        var autoCount = c.Children.Count(x => x.Size <= 0);
        var auto = autoCount > 0 ? (avail - fixedSum) / autoCount : 0;
        var min = U.Mm(40);
        if (fixedSum > avail + 1e-9 || (autoCount > 0 && auto < min))
        {
            warnings?.Add(Loc.F("As medidas fixas ({0}) não cabem no vão de {1}; os vãos foram ajustados proporcionalmente.",
                "Fixed sizes ({0}) do not fit the {1} space; adjusted proportionally.", U.FormatMm(fixedSum), U.FormatMm(avail)));
            var weights = c.Children.Select(x => x.Size > 0 ? x.Size : avail / n).ToArray();
            var sum = weights.Sum();
            for (var i = 0; i < n; i++) sizes[i] = avail * weights[i] / sum;
            return sizes;
        }
        for (var i = 0; i < n; i++) sizes[i] = c.Children[i].Size > 0 ? c.Children[i].Size : auto;
        if (autoCount == 0 && Math.Abs(fixedSum - avail) > 1e-9)
        {
            // todas fixas: a última absorve a diferença
            sizes[n - 1] += avail - fixedSum;
            if (Math.Abs(avail - fixedSum) > U.Mm(1))
                warnings?.Add(Loc.F("Todas as medidas são fixas; o último vão foi ajustado em {0} para fechar a conta.",
                    "All sizes are fixed; the last space was adjusted by {0}.", U.FormatMm(avail - fixedSum)));
        }
        return sizes;
    }

    public static CabinetModel Build(FreeDesign d, JoineryRules? rules = null)
    {
        rules ??= new JoineryRules();
        Validate(d);
        var spec = new CabinetSpec
        {
            Code = d.Code, Name = d.Name, Kind = CabinetKind.Custom, Width = d.Width, Height = d.Height, Depth = d.Depth, Elevation = d.Elevation,
            Thickness = d.Thickness, FrontThickness = d.FrontThickness, BackThickness = d.BackThickness, Back = d.Back,
            CarcassMaterial = d.CarcassMaterial, FrontMaterial = d.FrontMaterial, BackMaterial = d.BackMaterial, DrawerMaterial = d.DrawerMaterial,
            CountertopMaterial = d.CountertopMaterial, HasCountertop = d.HasCountertop, HasPlinth = d.Base == FreeBase.Plinth, PlinthHeight = d.PlinthHeight,
        };
        var m = new CabinetModel { Spec = spec };
        var counters = new Dictionary<PanelRole, int>();

        void Add(PanelRole role, double x, double y, double z, double sx, double sy, double sz, string material, bool grain, Axis thickAxis, string? name = null, params Axis[] bandedEdges)
        {
            if (sx <= 1e-6 || sy <= 1e-6 || sz <= 1e-6)
            {
                m.Warnings.Add(Loc.F("Peça \"{0}\" com dimensão nula foi ignorada.", "Board \"{0}\" with zero size was skipped.", name ?? JoineryNames.Role(role)));
                return;
            }
            counters.TryGetValue(role, out var n);
            counters[role] = ++n;
            var size = new Dictionary<Axis, double> { [Axis.X] = sx, [Axis.Y] = sy, [Axis.Z] = sz };
            var inPlane = new[] { Axis.X, Axis.Y, Axis.Z }.Where(a => a != thickAxis).ToArray();
            var lengthAxis = role is PanelRole.Side or PanelRole.Divider or PanelRole.Door or PanelRole.Back or PanelRole.Panel ? Axis.Z
                : role == PanelRole.DrawerSide ? Axis.Y : thickAxis == Axis.X ? Axis.Y : Axis.X;
            if (!inPlane.Contains(lengthAxis) || !grain)
                lengthAxis = size[inPlane[0]] >= size[inPlane[1]] ? inPlane[0] : inPlane[1];
            var widthAxis = inPlane.First(a => a != lengthAxis);
            var b = new Board
            {
                Id = $"{d.Code}-{JoineryNames.Abbrev(role)}{n:00}",
                Name = name ?? $"{JoineryNames.Role(role)} {n}",
                Role = role,
                Length = size[lengthAxis],
                Width = size[widthAxis],
                Thickness = size[thickAxis],
                Material = material,
                HasGrain = grain,
                Placement = new OrientedBox(new Frame3(new Vec3(x, y, z), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), sx, sy, sz),
            };
            foreach (var e in bandedEdges)
            {
                if (e == lengthAxis)
                {
                    if (!b.EdgeL1) b.EdgeL1 = true; else b.EdgeL2 = true;
                }
                else
                {
                    if (!b.EdgeC1) b.EdgeC1 = true; else b.EdgeC2 = true;
                }
            }
            m.Boards.Add(b);
        }

        void Hw(HardwareKind kind, int qty, string spec = "", double length = 0, string unit = "un")
        {
            if (qty <= 0) return;
            var existing = m.Hardware.FirstOrDefault(h => h.Kind == kind && h.Spec == spec && Math.Abs(h.Length - length) < 1e-9);
            if (existing != null)
            {
                existing.Quantity += qty;
                existing.Length += length;
                return;
            }
            m.Hardware.Add(new HardwareItem { Kind = kind, Name = JoineryNames.Hardware(kind), Spec = spec, Quantity = qty, Length = length, Unit = unit });
        }

        double W = d.Width, H = d.Height, D = d.Depth, t = d.Thickness, ft = d.FrontThickness, bt = d.BackThickness, gap = d.FrontGap;
        var z0 = d.CarcassBottom;
        var hb = H - z0;
        var overlayBack = d.Back == BackKind.Overlay;
        var carcassDepth = overlayBack ? D - bt : D;
        var joints = 0;

        // ---------- Caixa
        Add(PanelRole.Side, 0, 0, z0, t, carcassDepth, hb, d.CarcassMaterial, d.CarcassHasGrain, Axis.X, Loc.T("Lateral esquerda", "Left side"), Axis.Z);
        Add(PanelRole.Side, W - t, 0, z0, t, carcassDepth, hb, d.CarcassMaterial, d.CarcassHasGrain, Axis.X, Loc.T("Lateral direita", "Right side"), Axis.Z);
        Add(PanelRole.Bottom, t, 0, z0, W - 2 * t, carcassDepth, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, null, Axis.X);
        joints += 2;
        double topT = 0;
        if (d.Top == TopKind.Full)
        {
            Add(PanelRole.Top, t, 0, H - t, W - 2 * t, carcassDepth, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, null, Axis.X);
            topT = t;
            joints += 2;
        }
        else if (d.Top == TopKind.Rails)
        {
            var rw = Math.Min(d.RailWidth, carcassDepth / 2);
            Add(PanelRole.Rail, t, 0, H - t, W - 2 * t, rw, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, Loc.T("Travessa frontal", "Front rail"), Axis.X);
            Add(PanelRole.Rail, t, carcassDepth - rw, H - t, W - 2 * t, rw, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, Loc.T("Travessa traseira", "Back rail"));
            topT = t;
            joints += 4;
        }
        double backFront = D;
        if (d.Back == BackKind.Inset)
        {
            var g = d.BackGroove;
            backFront = D - d.BackRecess - bt;
            var zb0 = z0 + t - g;
            var zb1 = topT > 0 ? H - topT + g : H;
            Add(PanelRole.Back, t - g, backFront, zb0, W - 2 * t + 2 * g, bt, zb1 - zb0, d.BackMaterial, false, Axis.Y);
        }
        else if (overlayBack)
        {
            backFront = D - bt;
            Add(PanelRole.Back, 0, D - bt, z0, W, bt, hb, d.BackMaterial, false, Axis.Y);
        }
        var interiorDepth = backFront;

        // ---------- Vãos: divisórias, prateleiras fixas e conteúdo
        var warnings = new List<string>();
        var rects = Layout(d, warnings).ToDictionary(r => r.Id);
        m.Warnings.AddRange(warnings.Distinct());

        // vãos cobertos por uma porta (do próprio vão ou de um vão acima na árvore)
        var underDoor = new HashSet<string>();
        void MarkDoors(FreeCell c, bool covered)
        {
            if (covered && c.Door != FreeDoor.None)
            {
                m.Warnings.Add(Loc.T("Um vão dentro de outro com porta também tinha porta; a porta interna foi ignorada.", "A space inside another with a door also had a door; the inner door was ignored."));
                c.Door = FreeDoor.None;
            }
            var cov = covered || c.Door != FreeDoor.None;
            if (cov) underDoor.Add(c.Id);
            foreach (var ch in c.Children) MarkDoors(ch, cov);
        }
        var root = d.Root.DeepClone();
        MarkDoors(root, false);

        // recuo interno quando as frentes são embutidas (portas dentro do vão)
        double FrontInset(string id) => !d.OverlayFronts && underDoor.Contains(id) ? ft : 0;

        void Handle(double frontWidth)
        {
            switch (d.Handle)
            {
                case HandleKind.Bar:
                case HandleKind.Knob:
                case HandleKind.Profile:
                    var len = d.Handle == HandleKind.Profile ? frontWidth : d.Handle == HandleKind.Knob ? 0 : d.HandleLength;
                    Hw(HardwareKind.Handle, 1, d.Handle == HandleKind.Knob ? Loc.T("botão", "knob") : d.Handle == HandleKind.Profile ? Loc.T("perfil", "profile") : Loc.F("barra {0}", "bar {0}", U.FormatMm(d.HandleLength)), len);
                    break;
                case HandleKind.PushOpen:
                    Hw(HardwareKind.PushLatch, 1);
                    break;
                case HandleKind.Cava:
                    Hw(HardwareKind.Handle, 1, Loc.T("cava usinada na frente", "routed finger pull"));
                    break;
            }
        }

        string SlideSpec(double len) => d.Slides switch
        {
            SlideKind.Hidden => Loc.F("oculta (tandem) {0}{1}", "hidden (undermount) {0}{1}", U.FormatMm(len), d.SoftClose ? Loc.T(" com amortecimento", " soft-close") : ""),
            SlideKind.Roller => Loc.F("roldana {0}", "roller {0}", U.FormatMm(len)),
            _ => Loc.F("telescópica {0}{1}", "telescopic {0}{1}", U.FormatMm(len), d.SoftClose ? Loc.T(" com amortecimento", " soft-close") : ""),
        };
        var slideClear = d.Slides == SlideKind.Hidden ? U.Mm(5) : U.Mm(rules.SlideSideClearanceMm);

        void DrawerBox(double x0, double x1, double zb, double bh, double depthFrom, double depth)
        {
            var slide = rules.SlideFor(depth);
            if (slide <= 0)
            {
                m.Warnings.Add(Loc.T("Profundidade insuficiente para corrediças comerciais.", "Not enough depth for standard drawer slides."));
                return;
            }
            var bw = x1 - x0 - 2 * slideClear;
            var ds = U.Mm(15);
            var bx = x0 + slideClear;
            Add(PanelRole.DrawerSide, bx, depthFrom, zb, ds, slide, bh, d.DrawerMaterial, false, Axis.X, null, Axis.Y);
            Add(PanelRole.DrawerSide, bx + bw - ds, depthFrom, zb, ds, slide, bh, d.DrawerMaterial, false, Axis.X, null, Axis.Y);
            Add(PanelRole.DrawerInnerFront, bx + ds, depthFrom, zb, bw - 2 * ds, ds, bh, d.DrawerMaterial, false, Axis.Y, null, Axis.X);
            Add(PanelRole.DrawerBack, bx + ds, depthFrom + slide - ds, zb, bw - 2 * ds, ds, bh, d.DrawerMaterial, false, Axis.Y, null, Axis.X);
            var g = U.Mm(6);
            Add(PanelRole.DrawerBottom, bx + ds - g, depthFrom + ds - g, zb + U.Mm(8), bw - 2 * ds + 2 * g, slide - 2 * ds + 2 * g, U.Mm(6), d.BackMaterial, false, Axis.Z);
            Hw(HardwareKind.Slide, 1, SlideSpec(slide), slide);
        }

        void Walk(FreeCell c)
        {
            var r = rects[c.Id];
            var inset = FrontInset(c.Id);
            if (!c.IsLeaf)
            {
                // divisórias (colunas) ou prateleiras fixas (linhas) entre os vãos filhos
                for (var i = 0; i < c.Children.Count - 1; i++)
                {
                    var a = rects[c.Children[i].Id];
                    if (c.Split == FreeSplit.Columns)
                        Add(PanelRole.Divider, a.X1, 0, r.Z0, t, interiorDepth, r.Height, d.CarcassMaterial, d.CarcassHasGrain, Axis.X, null, Axis.Z);
                    else
                        Add(PanelRole.FixedShelf, r.X0, 0, a.Z0 - t, r.Width, interiorDepth, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, null, Axis.X);
                    joints += 2;
                }
                foreach (var ch in c.Children) Walk(ch);
                return;
            }

            // conteúdo do vão final
            var drawers = Math.Max(0, c.Drawers);
            var external = drawers > 0 && c.DrawerKind == FreeDrawerKind.External;
            if (external && underDoor.Contains(c.Id))
            {
                m.Warnings.Add(Loc.T("Gavetas com frente aparente dentro de um vão com porta viraram gavetas internas.", "External drawers inside a door space became internal drawers."));
                external = false;
            }
            if (drawers > 0 && !external && !underDoor.Contains(c.Id))
                m.Warnings.Add(Loc.T("Gaveta interna sem porta na frente: ela fica aparente.", "Internal drawer without a door in front: it stays visible."));

            if (external)
            {
                var fx0 = (d.OverlayFronts ? r.FX0 : r.X0) + gap / 2;
                var fx1 = (d.OverlayFronts ? r.FX1 : r.X1) - gap / 2;
                var fz0 = (d.OverlayFronts ? r.FZ0 : r.Z0) + gap / 2;
                var fz1 = (d.OverlayFronts ? r.FZ1 : r.Z1) - gap / 2;
                var fh = (fz1 - fz0 - (drawers - 1) * gap) / drawers;
                var y = d.OverlayFronts ? -ft : 0;
                var depthFrom = d.OverlayFronts ? 0 : ft;
                var mat = string.IsNullOrWhiteSpace(c.FrontMaterial) ? d.FrontMaterial : c.FrontMaterial;
                for (var k = 0; k < drawers; k++)
                {
                    var top = fz1 - k * (fh + gap);
                    Add(PanelRole.DrawerFront, fx0, y, top - fh, fx1 - fx0, ft, fh, mat, d.FrontHasGrain, Axis.Y, null, Axis.X, Axis.X, Axis.Z, Axis.Z);
                    Handle(fx1 - fx0);
                    var zb = Math.Max(r.Z0 + U.Mm(5), top - fh + U.Mm(15));
                    var bh = Math.Max(U.Mm(60), Math.Min(fh - U.Mm(rules.DrawerBoxHeightReductionMm), r.Z1 - zb - U.Mm(10)));
                    DrawerBox(r.X0, r.X1, zb, bh, depthFrom, interiorDepth - depthFrom);
                }
                return;
            }
            var zA = r.Z0;
            if (drawers > 0)
            {
                // gavetas internas na parte de baixo do vão
                var zoneH = Math.Min(r.Height * 0.6, drawers * U.Mm(180));
                var each = zoneH / drawers;
                for (var k = 0; k < drawers; k++)
                    DrawerBox(r.X0, r.X1, r.Z0 + k * each + U.Mm(10), Math.Max(U.Mm(60), each - U.Mm(30)), inset + U.Mm(20), interiorDepth - inset - U.Mm(20));
                zA = r.Z0 + zoneH;
                if (c.Shelves > 0 || c.HangingRail) Add(PanelRole.FixedShelf, r.X0, inset, zA, r.Width, interiorDepth - inset, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, null, Axis.X);
                zA += t;
            }
            if (c.HangingRail)
            {
                if (c.Shelves > 0)
                    m.Warnings.Add(Loc.T("Cabideiro e prateleiras no mesmo vão: as prateleiras foram ignoradas (divida o vão em linhas para ter os dois).", "Rail and shelves in the same space: shelves ignored (split into rows)."));
                Hw(HardwareKind.HangingRail, 1, Loc.F("tubo oblongo {0}", "oval tube {0}", U.FormatMm(r.Width)), r.Width, "m");
                Hw(HardwareKind.ShelfSupport, 2, Loc.T("suporte de cabideiro", "rail bracket"));
                if (r.Z1 - zA < U.Mm(800))
                    m.Warnings.Add(Loc.F("Vão do cabideiro com {0} de altura (pouco para roupas).", "Rail space is only {0} high.", U.FormatMm(r.Z1 - zA)));
                return;
            }
            var n = Math.Max(0, c.Shelves);
            if (n > 0)
            {
                if (r.Width > U.Mm(rules.MaxShelfSpanMm))
                    m.Warnings.Add(Loc.F("Prateleira com vão de {0} pode envergar; considere uma divisória.", "Shelf span of {0} may sag; consider a divider.", U.FormatMm(r.Width)));
                var spacing = (r.Z1 - zA - n * t) / (n + 1);
                var adjustable = !c.FixedShelves;
                var sx = r.X0 + (adjustable ? U.Mm(1) : 0);
                var sw = r.Width - (adjustable ? U.Mm(2) : 0);
                var sy = inset + d.ShelfSetback;
                for (var k = 0; k < n; k++)
                {
                    var z = zA + spacing * (k + 1) + t * k;
                    Add(adjustable ? PanelRole.Shelf : PanelRole.FixedShelf, sx, sy, z, sw, interiorDepth - sy, t, d.CarcassMaterial, d.CarcassHasGrain, Axis.Z, null, Axis.X);
                    if (adjustable) Hw(HardwareKind.ShelfSupport, rules.ShelfSupportsPerShelf);
                    else joints += 2;
                }
            }
        }
        Walk(root);

        // ---------- Frentes (portas) de qualquer vão
        void Doors(FreeCell c)
        {
            if (c.Door != FreeDoor.None)
            {
                var r = rects[c.Id];
                var x0 = (d.OverlayFronts ? r.FX0 : r.X0) + gap / 2;
                var x1 = (d.OverlayFronts ? r.FX1 : r.X1) - gap / 2;
                var za = (d.OverlayFronts ? r.FZ0 : r.Z0) + gap / 2;
                var zb = (d.OverlayFronts ? r.FZ1 : r.Z1) - gap / 2;
                var w = x1 - x0;
                var h = zb - za;
                var y = d.OverlayFronts ? -ft : 0;
                var mat = c.Glass ? d.GlassMaterial : string.IsNullOrWhiteSpace(c.FrontMaterial) ? d.FrontMaterial : c.FrontMaterial;
                var grain = !c.Glass && d.FrontHasGrain;
                void Leaf(double lx, double lw, double ly, string name)
                {
                    Add(PanelRole.Door, lx, ly, za, lw, ft, h, mat, grain, Axis.Y, name, c.Glass ? Array.Empty<Axis>() : new[] { Axis.X, Axis.X, Axis.Z, Axis.Z });
                    if (c.Glass) Hw(HardwareKind.GlassProfile, 1, Loc.T("perfil de alumínio para porta de vidro", "aluminium frame for glass door"), 2 * (lw + h), "m");
                    Handle(lw);
                }
                string Hinge() => d.SoftClose ? Loc.T("caneco 35 mm com amortecimento", "35 mm cup, soft-close") : Loc.T("caneco 35 mm", "35 mm cup");
                switch (c.Door)
                {
                    case FreeDoor.HingedLeft:
                    case FreeDoor.HingedRight:
                        Leaf(x0, w, y, c.Door == FreeDoor.HingedLeft ? Loc.T("Porta (abre à esquerda)", "Door (left hinged)") : Loc.T("Porta (abre à direita)", "Door (right hinged)"));
                        Hw(HardwareKind.Hinge, rules.HingesFor(h), Hinge());
                        if (w > U.Mm(rules.MaxSingleDoorWidthMm))
                            m.Warnings.Add(Loc.F("Porta de abrir com {0} de largura; considere duas folhas.", "Hinged door {0} wide; consider two leaves.", U.FormatMm(w)));
                        break;
                    case FreeDoor.HingedDouble:
                    {
                        var lw = (w - gap) / 2;
                        Leaf(x0, lw, y, Loc.T("Porta esquerda", "Left door"));
                        Leaf(x0 + lw + gap, lw, y, Loc.T("Porta direita", "Right door"));
                        Hw(HardwareKind.Hinge, 2 * rules.HingesFor(h), Hinge());
                        if (lw < U.Mm(rules.MinDoorWidthMm)) m.Warnings.Add(Loc.F("Porta estreita ({0}).", "Narrow door ({0}).", U.FormatMm(lw)));
                        break;
                    }
                    case FreeDoor.FlipUp:
                        Leaf(x0, w, y, Loc.T("Porta basculante (abre para cima)", "Flap door (opens up)"));
                        Hw(HardwareKind.Hinge, w > U.Mm(900) ? 3 : 2, Loc.T("dobradiça para basculante", "flap hinge"));
                        Hw(HardwareKind.Lift, 1, Loc.T("par de articuladores / pistões a gás", "pair of lift struts"));
                        break;
                    case FreeDoor.FlipDown:
                        Leaf(x0, w, y, Loc.T("Porta escamoteável (abre para baixo)", "Drop-down door"));
                        Hw(HardwareKind.Hinge, w > U.Mm(900) ? 3 : 2, Loc.T("dobradiça para escamoteável", "drop-down hinge"));
                        Hw(HardwareKind.Lift, 1, Loc.T("par de braços / compassos de sustentação", "pair of stay arms"));
                        break;
                    case FreeDoor.Sliding:
                    {
                        var n = w > U.Mm(1800) ? 3 : 2;
                        var lw = (w + (n - 1) * d.SlidingOverlap) / n;
                        for (var k = 0; k < n; k++)
                        {
                            var ly = (k % 2 == 0) ? y : y - ft - U.Mm(5);
                            Leaf(x0 + k * (lw - d.SlidingOverlap), lw, ly, Loc.F("Porta de correr {0}", "Sliding door {0}", k + 1));
                            Hw(HardwareKind.SlidingRoller, 2, Loc.T("roldana / carrinho", "roller"));
                        }
                        Hw(HardwareKind.SlidingTrack, 1, Loc.T("trilho superior + inferior (2 vias)", "top + bottom track (2 ways)"), w, "m");
                        break;
                    }
                    case FreeDoor.Bifold:
                    {
                        var lw = (w - gap) / 2;
                        Leaf(x0, lw, y, Loc.T("Porta camarão (folha 1)", "Bifold door (leaf 1)"));
                        Leaf(x0 + lw + gap, lw, y, Loc.T("Porta camarão (folha 2)", "Bifold door (leaf 2)"));
                        Hw(HardwareKind.Hinge, 2 * rules.HingesFor(h), Hinge());
                        Hw(HardwareKind.BifoldKit, 1, Loc.T("kit de porta camarão (guia e pivô)", "bifold kit (guide and pivot)"));
                        break;
                    }
                }
            }
            foreach (var ch in c.Children) Doors(ch);
        }
        Doors(root);

        // ---------- Base, tampo de bancada e conectores
        switch (d.Base)
        {
            case FreeBase.Plinth when d.PlinthHeight > 0:
            {
                Add(PanelRole.Plinth, 0, d.PlinthSetback, 0, W, t, d.PlinthHeight, d.CarcassMaterial, false, Axis.Y);
                var perRow = Math.Max(2, (int)Math.Ceiling(U.ToMm(W) / rules.FootSpacingMaxMm) + 1);
                Hw(HardwareKind.Foot, 2 * perRow, Loc.F("regulável, altura {0}", "adjustable, height {0}", U.FormatMm(d.PlinthHeight)));
                break;
            }
            case FreeBase.Feet:
            {
                var perRow = Math.Max(2, (int)Math.Ceiling(U.ToMm(W) / rules.FootSpacingMaxMm) + 1);
                Hw(HardwareKind.Foot, 2 * perRow, Loc.F("aparente, altura {0}", "visible, height {0}", U.FormatMm(d.FeetHeight)));
                break;
            }
            case FreeBase.Suspended:
                Hw(HardwareKind.WallBracket, 1, Loc.T("par de suportes de fixação na parede", "pair of wall hanging brackets"));
                break;
        }
        if (d.HasCountertop)
        {
            var front = d.OverlayFronts ? ft : 0;
            Add(PanelRole.Countertop, -d.CountertopOverhangSides, -d.CountertopOverhangFront - front, H,
                W + 2 * d.CountertopOverhangSides, D + d.CountertopOverhangFront + front + d.CountertopOverhangBack, d.CountertopThickness,
                d.CountertopMaterial, false, Axis.Z);
        }
        var perJoint = rules.ConnectorsPerJoint + (D > U.Mm(600) ? 1 : 0);
        Hw(HardwareKind.Connector, joints * perJoint);
        return m;
    }

    private static void Validate(FreeDesign d)
    {
        var t = d.Thickness;
        if (t <= 0 || d.FrontThickness <= 0)
            throw new ArgumentException(Loc.T("Espessuras devem ser maiores que zero.", "Thicknesses must be greater than zero."));
        if (d.Width < 2 * t + U.Mm(50))
            throw new ArgumentException(Loc.F("Largura muito pequena ({0}).", "Width too small ({0}).", U.FormatMm(d.Width)));
        if (d.Height < d.CarcassBottom + 2 * t + U.Mm(50))
            throw new ArgumentException(Loc.F("Altura muito pequena ({0}).", "Height too small ({0}).", U.FormatMm(d.Height)));
        if (d.Depth < U.Mm(100))
            throw new ArgumentException(Loc.F("Profundidade muito pequena ({0}).", "Depth too small ({0}).", U.FormatMm(d.Depth)));
    }
}
