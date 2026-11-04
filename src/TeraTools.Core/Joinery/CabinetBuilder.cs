using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

/// <summary>
/// Gera as peças (chapas), frentes e ferragens de um módulo de marcenaria a partir da especificação.
/// Sistema local: X = largura (esq.→dir.), Y = profundidade (0 = face frontal da caixa), Z = altura (0 = base do módulo).
/// Frentes sobrepostas ficam em Y negativo (na frente da caixa).
/// </summary>
public static class CabinetBuilder
{
    private enum Axis { X, Y, Z }

    public static CabinetModel Build(CabinetSpec s, JoineryRules? rules = null)
    {
        rules ??= new JoineryRules();
        var m = new CabinetModel { Spec = s };
        Validate(s);
        var counters = new Dictionary<PanelRole, int>();

        void Add(PanelRole role, double x, double y, double z, double sx, double sy, double sz, string material, bool grain, Axis thickAxis, params Axis[] bandedEdges)
        {
            if (sx <= 1e-6 || sy <= 1e-6 || sz <= 1e-6)
            {
                m.Warnings.Add(Loc.F("Peça \"{0}\" com dimensão nula foi ignorada.", "Board \"{0}\" with zero size was skipped.", JoineryNames.Role(role)));
                return;
            }
            counters.TryGetValue(role, out var n);
            counters[role] = ++n;
            var size = new Dictionary<Axis, double> { [Axis.X] = sx, [Axis.Y] = sy, [Axis.Z] = sz };
            var inPlane = new[] { Axis.X, Axis.Y, Axis.Z }.Where(a => a != thickAxis).ToArray();
            var lengthAxis = PreferredLengthAxis(role, thickAxis);
            if (!inPlane.Contains(lengthAxis) || !(grain))
                lengthAxis = size[inPlane[0]] >= size[inPlane[1]] ? inPlane[0] : inPlane[1];
            var widthAxis = inPlane.First(a => a != lengthAxis);
            var b = new Board
            {
                Id = $"{s.Code}-{JoineryNames.Abbrev(role)}{n:00}",
                Name = $"{JoineryNames.Role(role)} {n}",
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
            if (existing != null) { existing.Quantity += qty; return; }
            m.Hardware.Add(new HardwareItem { Kind = kind, Name = JoineryNames.Hardware(kind), Spec = spec, Quantity = qty, Length = length, Unit = unit });
        }

        double W = s.Width, H = s.Height, D = s.Depth, t = s.Thickness, ft = s.FrontThickness, bt = s.BackThickness, gap = s.FrontGap;
        var carcassGrain = s.CarcassHasGrain;
        var frontGrain = s.FrontHasGrain;

        if (s.Kind == CabinetKind.TvPanel)
        {
            Add(PanelRole.Panel, 0, D - t, 0, W, t, H, s.FrontMaterial, frontGrain, Axis.Y, Axis.X, Axis.X, Axis.Z, Axis.Z);
            var n = Math.Max(0, s.Shelves);
            for (var si = 0; si < n; si++)
            {
                var z = H * (si + 1) / (n + 1) - t / 2;
                Add(PanelRole.Shelf, 0, 0, z, W, D - t, t, s.FrontMaterial, frontGrain, Axis.Z, Axis.X);
                Hw(HardwareKind.ShelfSupport, 3, Loc.T("suporte invisível", "hidden support"));
            }
            return m;
        }

        var z0 = s.HasPlinth ? s.PlinthHeight : 0;
        var hb = H - z0;
        var overlayBack = s.Back == BackKind.Overlay;
        var carcassDepth = overlayBack ? D - bt : D;

        // Laterais
        Add(PanelRole.Side, 0, 0, z0, t, carcassDepth, hb, s.CarcassMaterial, carcassGrain, Axis.X, Axis.Z);
        Add(PanelRole.Side, W - t, 0, z0, t, carcassDepth, hb, s.CarcassMaterial, carcassGrain, Axis.X, Axis.Z);

        // Base
        Add(PanelRole.Bottom, t, 0, z0, W - 2 * t, carcassDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);

        // Tampo / travessas
        double topT = 0;
        var joints = 2;
        if (s.Top == TopKind.Full)
        {
            Add(PanelRole.Top, t, 0, H - t, W - 2 * t, carcassDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
            topT = t;
            joints += 2;
        }
        else if (s.Top == TopKind.Rails)
        {
            var rw = Math.Min(s.RailWidth, carcassDepth / 2);
            Add(PanelRole.Rail, t, 0, H - t, W - 2 * t, rw, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
            Add(PanelRole.Rail, t, carcassDepth - rw, H - t, W - 2 * t, rw, t, s.CarcassMaterial, carcassGrain, Axis.Z);
            topT = t;
            joints += 4;
        }

        // Fundo
        double backFront = D;
        if (s.Back == BackKind.Inset)
        {
            var g = s.BackGroove;
            backFront = D - s.BackRecess - bt;
            var zb0 = z0 + t - g;
            var zb1 = topT > 0 ? H - topT + g : H;
            Add(PanelRole.Back, t - g, backFront, zb0, W - 2 * t + 2 * g, bt, zb1 - zb0, s.BackMaterial, false, Axis.Y);
        }
        else if (overlayBack)
        {
            backFront = D - bt;
            Add(PanelRole.Back, 0, D - bt, z0, W, bt, hb, s.BackMaterial, false, Axis.Y);
        }

        var ix0 = t;
        var ix1 = W - t;
        var iz0 = z0 + t;
        var iz1 = H - topT;
        var interiorDepth = backFront;

        // Divisórias e compartimentos
        var k = Math.Max(0, s.Dividers);
        var cw = (ix1 - ix0 - k * t) / (k + 1);
        if (cw < U.Mm(100))
        {
            m.Warnings.Add(Loc.T("Compartimentos muito estreitos; divisórias removidas.", "Compartments too narrow; dividers removed."));
            k = 0;
            cw = ix1 - ix0;
        }
        var compartments = new List<(double X0, double X1)>();
        for (var j = 0; j <= k; j++)
            compartments.Add((ix0 + j * (cw + t), ix0 + j * (cw + t) + cw));
        for (var j = 0; j < k; j++)
        {
            Add(PanelRole.Divider, compartments[j].X1, 0, iz0, t, interiorDepth, iz1 - iz0, s.CarcassMaterial, carcassGrain, Axis.X, Axis.Z);
            joints += 2;
        }

        // Colunas de frente: limites nos eixos das divisórias.
        var columns = new List<(double X0, double X1)>();
        for (var j = 0; j <= k; j++)
        {
            var a = j == 0 ? 0 : compartments[j].X0 - t / 2;
            var b = j == k ? W : compartments[j].X1 + t / 2;
            columns.Add((a + gap / 2, b - gap / 2));
        }

        // Zonas de frente (gavetas / portas)
        var fz0 = z0 + gap / 2;
        var fz1 = H - gap / 2;
        var drawers = Math.Max(0, s.Drawers);
        var hasDoors = s.Doors != DoorKind.None;
        var doorZone = (A: fz0, B: fz1);
        var shelfZone = (A: iz0, B: iz1);
        var drawerFronts = new List<(double A, double B)>();

        if (drawers > 0)
        {
            if (!hasDoors)
            {
                var fh = (fz1 - fz0 - (drawers - 1) * gap) / drawers;
                for (var d = 0; d < drawers; d++)
                {
                    var b = fz1 - d * (fh + gap);
                    drawerFronts.Add((b - fh, b));
                }
                shelfZone = (iz0, iz0);
            }
            else
            {
                var dzH = drawers * s.DrawerFrontHeight + (drawers - 1) * gap;
                if (dzH > (fz1 - fz0) * 0.8)
                {
                    m.Warnings.Add(Loc.T("Gavetas ocupam altura demais; reduzidas para caber.", "Drawers take too much height; reduced to fit."));
                    dzH = (fz1 - fz0) * 0.5;
                }
                var fh = (dzH - (drawers - 1) * gap) / drawers;
                if (s.DrawerZone == DrawerZone.Top)
                {
                    for (var d = 0; d < drawers; d++)
                    {
                        var b = fz1 - d * (fh + gap);
                        drawerFronts.Add((b - fh, b));
                    }
                    doorZone = (fz0, fz1 - dzH - gap);
                    var shelfZ = doorZone.B + gap / 2 - t / 2;
                    foreach (var c in compartments)
                    {
                        Add(PanelRole.FixedShelf, c.X0, 0, shelfZ, c.X1 - c.X0, interiorDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
                        joints += 2;
                    }
                    shelfZone = (iz0, shelfZ);
                }
                else
                {
                    for (var d = 0; d < drawers; d++)
                    {
                        var a = fz0 + d * (fh + gap);
                        drawerFronts.Add((a, a + fh));
                    }
                    doorZone = (fz0 + dzH + gap, fz1);
                    var shelfZ = doorZone.A - gap / 2 - t / 2;
                    foreach (var c in compartments)
                    {
                        Add(PanelRole.FixedShelf, c.X0, 0, shelfZ, c.X1 - c.X0, interiorDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
                        joints += 2;
                    }
                    shelfZone = (shelfZ + t, iz1);
                }
            }
        }

        void HandleFor(double frontWidth)
        {
            switch (s.Handle)
            {
                case HandleKind.Bar:
                case HandleKind.Knob:
                case HandleKind.Profile:
                    var len = s.Handle == HandleKind.Profile ? frontWidth : s.Handle == HandleKind.Knob ? 0 : s.HandleLength;
                    Hw(HardwareKind.Handle, 1, s.Handle == HandleKind.Knob ? Loc.T("botão", "knob") : s.Handle == HandleKind.Profile ? Loc.T("perfil", "profile") : Loc.F("barra {0}", "bar {0}", U.FormatMm(s.HandleLength)), len);
                    break;
                case HandleKind.PushOpen:
                    Hw(HardwareKind.PushLatch, 1);
                    break;
            }
        }

        // Gavetas (frentes + caixas)
        var slideClear = U.Mm(rules.SlideSideClearanceMm);
        var slide = rules.SlideFor(interiorDepth);
        foreach (var (colIdx, col) in columns.Select((c, i) => (i, c)))
        {
            var comp = compartments[colIdx];
            foreach (var (a, b) in drawerFronts)
            {
                Add(PanelRole.DrawerFront, col.X0, -ft, a, col.X1 - col.X0, ft, b - a, s.FrontMaterial, frontGrain, Axis.Y, Axis.X, Axis.X, Axis.Z, Axis.Z);
                HandleFor(col.X1 - col.X0);
                if (slide <= 0)
                {
                    m.Warnings.Add(Loc.T("Profundidade insuficiente para corrediças comerciais.", "Not enough depth for standard drawer slides."));
                    continue;
                }
                var bw = comp.X1 - comp.X0 - 2 * slideClear;
                var bh = Math.Max(U.Mm(60), (b - a) - U.Mm(rules.DrawerBoxHeightReductionMm));
                var zb = Math.Max(iz0 + U.Mm(5), a + U.Mm(15));
                if (s.DrawerZone == DrawerZone.Top && hasDoors) zb = Math.Max(zb, shelfZone.B + t + U.Mm(5));
                var ds = s.DrawerSideThickness;
                var bx = comp.X0 + slideClear;
                Add(PanelRole.DrawerSide, bx, 0, zb, ds, slide, bh, s.DrawerMaterial, false, Axis.X, Axis.Y);
                Add(PanelRole.DrawerSide, bx + bw - ds, 0, zb, ds, slide, bh, s.DrawerMaterial, false, Axis.X, Axis.Y);
                Add(PanelRole.DrawerInnerFront, bx + ds, 0, zb, bw - 2 * ds, ds, bh, s.DrawerMaterial, false, Axis.Y, Axis.X);
                Add(PanelRole.DrawerBack, bx + ds, slide - ds, zb, bw - 2 * ds, ds, bh, s.DrawerMaterial, false, Axis.Y, Axis.X);
                var g = U.Mm(6);
                Add(PanelRole.DrawerBottom, bx + ds - g, ds - g, zb + U.Mm(8), bw - 2 * ds + 2 * g, slide - 2 * ds + 2 * g, s.DrawerBottomThickness, s.BackMaterial, false, Axis.Z);
                Hw(HardwareKind.Slide, 1, (s.SoftClose ? Loc.T("telescópica com amortecimento ", "soft-close telescopic ") : Loc.T("telescópica ", "telescopic ")) + U.FormatMm(slide), slide);
            }
        }

        // Portas
        var doorH = doorZone.B - doorZone.A;
        if (hasDoors && doorH > U.Mm(50))
        {
            if (s.Doors == DoorKind.Sliding)
            {
                var n = s.DoorCount > 0 ? s.DoorCount : (W > U.Mm(1800) ? 3 : 2);
                var dw = (W - gap + (n - 1) * s.SlidingOverlap) / n;
                for (var d = 0; d < n; d++)
                {
                    var x = gap / 2 + d * (dw - s.SlidingOverlap);
                    var y = (d % 2 == 0) ? -ft : -2 * ft - U.Mm(5);
                    Add(PanelRole.Door, x, y, doorZone.A, dw, ft, doorH, s.FrontMaterial, frontGrain, Axis.Y, Axis.X, Axis.X, Axis.Z, Axis.Z);
                    Hw(HardwareKind.SlidingRoller, 2);
                    HandleFor(dw);
                }
                Hw(HardwareKind.SlidingTrack, 1, Loc.F("{0} trilhos", "{0} tracks", Math.Min(n, 2)), W, "m");
            }
            else
            {
                foreach (var col in columns)
                {
                    var colW = col.X1 - col.X0;
                    int n = s.Doors switch
                    {
                        DoorKind.HingedDouble => 2,
                        DoorKind.FlipUp => 1,
                        _ => s.DoorCount > 0 ? s.DoorCount : (colW > U.Mm(rules.MaxSingleDoorWidthMm) ? 2 : 1),
                    };
                    var dw = (colW - (n - 1) * gap) / n;
                    if (dw < U.Mm(rules.MinDoorWidthMm))
                        m.Warnings.Add(Loc.F("Porta estreita ({0}).", "Narrow door ({0}).", U.FormatMm(dw)));
                    for (var d = 0; d < n; d++)
                    {
                        var x = col.X0 + d * (dw + gap);
                        Add(PanelRole.Door, x, -ft, doorZone.A, dw, ft, doorH, s.FrontMaterial, frontGrain, Axis.Y, Axis.X, Axis.X, Axis.Z, Axis.Z);
                        HandleFor(dw);
                        if (s.Doors == DoorKind.FlipUp)
                        {
                            Hw(HardwareKind.Hinge, dw > U.Mm(900) ? 3 : 2, Loc.T("dobradiça para basculante", "flap hinge"));
                            Hw(HardwareKind.Lift, 1, Loc.T("par de articuladores / pistões", "pair of lift struts"));
                        }
                        else
                        {
                            Hw(HardwareKind.Hinge, rules.HingesFor(doorH), s.SoftClose ? Loc.T("caneco 35 mm com amortecimento", "35 mm cup, soft-close") : Loc.T("caneco 35 mm", "35 mm cup"));
                        }
                    }
                }
            }
        }

        // Prateleiras
        var shelfCount = Math.Max(0, s.Shelves);
        for (var ci = 0; ci < compartments.Count; ci++)
        {
            var c = compartments[ci];
            var compW = c.X1 - c.X0;
            var zA = shelfZone.A;
            var zB = shelfZone.B;
            if (zB - zA < U.Mm(100)) continue;
            var shelfW = compW - (s.AdjustableShelves ? U.Mm(2) : 0);
            var shelfX = c.X0 + (s.AdjustableShelves ? U.Mm(1) : 0);
            var shelfDepth = interiorDepth - s.ShelfSetback;
            var hanging = s.HangingRail && (ci % 2 == 0);
            if (hanging)
            {
                var zs = Math.Max(zA, zB - U.Mm(400));
                Add(PanelRole.Shelf, shelfX, s.ShelfSetback, zs, shelfW, shelfDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
                if (s.AdjustableShelves) Hw(HardwareKind.ShelfSupport, rules.ShelfSupportsPerShelf);
                Hw(HardwareKind.HangingRail, 1, Loc.F("tubo oblongo {0}", "oval tube {0}", U.FormatMm(compW)), compW, "m");
                continue;
            }
            if (shelfCount == 0) continue;
            if (compW > U.Mm(rules.MaxShelfSpanMm))
                m.Warnings.Add(Loc.F("Vão de prateleira de {0} pode envergar; considere uma divisória.", "Shelf span of {0} may sag; consider a divider.", U.FormatMm(compW)));
            var spacing = (zB - zA - shelfCount * t) / (shelfCount + 1);
            for (var n = 0; n < shelfCount; n++)
            {
                var z = zA + spacing * (n + 1) + t * n;
                Add(PanelRole.Shelf, shelfX, s.ShelfSetback, z, shelfW, shelfDepth, t, s.CarcassMaterial, carcassGrain, Axis.Z, Axis.X);
                if (s.AdjustableShelves) Hw(HardwareKind.ShelfSupport, rules.ShelfSupportsPerShelf);
                else joints += 2;
            }
        }

        // Rodapé e pés
        if (s.HasPlinth && s.PlinthHeight > 0)
        {
            Add(PanelRole.Plinth, 0, s.PlinthSetback, 0, W, t, s.PlinthHeight, s.CarcassMaterial, false, Axis.Y);
            var perRow = Math.Max(2, (int)Math.Ceiling(U.ToMm(W) / rules.FootSpacingMaxMm) + 1);
            Hw(HardwareKind.Foot, 2 * perRow, Loc.F("altura {0}", "height {0}", U.FormatMm(s.PlinthHeight)));
        }

        // Tampo de bancada
        if (s.HasCountertop)
        {
            Add(PanelRole.Countertop, -s.CountertopOverhangSides, -s.CountertopOverhangFront - ft, H,
                W + 2 * s.CountertopOverhangSides, D + s.CountertopOverhangFront + ft + s.CountertopOverhangBack, s.CountertopThickness,
                s.CountertopMaterial, false, Axis.Z);
        }

        // Conectores de montagem
        var perJoint = rules.ConnectorsPerJoint + (D > U.Mm(600) ? 1 : 0);
        Hw(HardwareKind.Connector, joints * perJoint);

        return m;
    }

    private static Axis PreferredLengthAxis(PanelRole role, Axis thick) => role switch
    {
        PanelRole.Side or PanelRole.Divider or PanelRole.Door or PanelRole.Panel or PanelRole.Back => Axis.Z,
        PanelRole.DrawerSide => Axis.Y,
        _ => thick == Axis.X ? Axis.Y : Axis.X,
    };

    private static void Validate(CabinetSpec s)
    {
        var t = s.Thickness;
        if (s.Width < 2 * t + U.Mm(50))
            throw new ArgumentException(Loc.F("Largura muito pequena ({0}).", "Width too small ({0}).", U.FormatMm(s.Width)));
        var minH = (s.HasPlinth ? s.PlinthHeight : 0) + 2 * t + U.Mm(50);
        if (s.Height < minH)
            throw new ArgumentException(Loc.F("Altura muito pequena ({0}).", "Height too small ({0}).", U.FormatMm(s.Height)));
        if (s.Depth < U.Mm(100))
            throw new ArgumentException(Loc.F("Profundidade muito pequena ({0}).", "Depth too small ({0}).", U.FormatMm(s.Depth)));
        if (t <= 0 || s.FrontThickness <= 0)
            throw new ArgumentException(Loc.T("Espessuras devem ser maiores que zero.", "Thicknesses must be greater than zero."));
    }
}

/// <summary>Distribuição de módulos em um vão disponível ("ajuste automático às dimensões").</summary>
public sealed class ModuleSlot
{
    public string Name { get; set; } = "";
    public double? FixedWidth { get; set; }
    public double MinWidth { get; set; } = U.Mm(300);
    public double MaxWidth { get; set; } = U.Mm(1000);
    public double PreferredWidth { get; set; } = U.Mm(600);
}

public sealed class CompositionResult
{
    public bool Success { get; set; }
    public List<double> Widths { get; } = new();
    public double Filler { get; set; }
    public string? Message { get; set; }
}

public static class Composition
{
    /// <summary>Divide o vão em N módulos iguais, escolhendo N para ficar mais próximo do preferido.</summary>
    public static CompositionResult SplitEqual(double length, double min, double max, double preferred, double roundStep = 0, double fillerMax = 0)
    {
        var r = new CompositionResult();
        if (length <= 0 || min <= 0 || max < min)
        {
            r.Message = Loc.T("Parâmetros inválidos.", "Invalid parameters.");
            return r;
        }
        var nMin = Math.Max(1, (int)Math.Ceiling(length / max - 1e-9));
        var nMax = (int)Math.Floor(length / min + 1e-9);
        if (nMax < nMin)
        {
            if (fillerMax >= length)
            {
                r.Success = true;
                r.Filler = length;
                return r;
            }
            r.Message = Loc.T("O vão não comporta módulos dentro dos limites.", "The run cannot fit modules within the limits.");
            return r;
        }
        var best = nMin;
        for (var n = nMin; n <= nMax; n++)
            if (Math.Abs(length / n - preferred) < Math.Abs(length / best - preferred))
                best = n;
        var w = length / best;
        if (roundStep > 0 && fillerMax > 0)
        {
            var rw = Math.Floor(w / roundStep + 1e-9) * roundStep;
            var filler = length - rw * best;
            if (rw >= min && filler <= fillerMax + 1e-9)
            {
                w = rw;
                r.Filler = filler;
            }
        }
        for (var i = 0; i < best; i++) r.Widths.Add(w);
        r.Success = true;
        return r;
    }

    /// <summary>Encaixa módulos fixos + flexíveis no vão; a sobra vira complemento (se permitido).</summary>
    public static CompositionResult Fit(double length, IReadOnlyList<ModuleSlot> slots, double fillerMax = 0)
    {
        var r = new CompositionResult();
        var fixedSum = slots.Where(sl => sl.FixedWidth.HasValue).Sum(sl => sl.FixedWidth!.Value);
        var flex = slots.Where(sl => !sl.FixedWidth.HasValue).ToList();
        var remaining = length - fixedSum;
        if (remaining < -1e-9)
        {
            r.Message = Loc.T("Os módulos fixos excedem o vão disponível.", "Fixed modules exceed the available run.");
            return r;
        }
        var widths = new Dictionary<ModuleSlot, double>();
        if (flex.Count > 0)
        {
            var free = new List<ModuleSlot>(flex);
            var budget = remaining;
            for (var iter = 0; iter < flex.Count + 2 && free.Count > 0; iter++)
            {
                var pref = free.Sum(f => f.PreferredWidth);
                var clampedAny = false;
                foreach (var f in free.ToList())
                {
                    var w = budget * f.PreferredWidth / pref;
                    if (w < f.MinWidth) { widths[f] = f.MinWidth; budget -= f.MinWidth; free.Remove(f); clampedAny = true; }
                    else if (w > f.MaxWidth) { widths[f] = f.MaxWidth; budget -= f.MaxWidth; free.Remove(f); clampedAny = true; }
                }
                if (!clampedAny)
                {
                    foreach (var f in free) widths[f] = budget * f.PreferredWidth / pref;
                    free.Clear();
                }
            }
        }
        foreach (var sl in slots)
            r.Widths.Add(sl.FixedWidth ?? widths.GetValueOrDefault(sl, sl.MinWidth));
        var sum = r.Widths.Sum();
        var left = length - sum;
        if (left < -1e-6)
        {
            r.Message = Loc.T("Os módulos mínimos não cabem no vão.", "Minimum module widths do not fit the run.");
            return r;
        }
        if (left > 1e-6)
        {
            if (left <= fillerMax + 1e-9) r.Filler = left;
            else
            {
                r.Message = Loc.F("Sobra de {0} acima do complemento máximo.", "Leftover of {0} exceeds the maximum filler.", U.FormatMm(left));
                return r;
            }
        }
        r.Success = true;
        return r;
    }
}
