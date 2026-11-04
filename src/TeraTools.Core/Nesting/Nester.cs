using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Nesting;

public enum NestingMethod
{
    /// <summary>Cortes guilhotinados (seccionadora / serra esquadrejadeira).</summary>
    Guillotine,
    /// <summary>Encaixe livre (MaxRects) — para CNC/nesting.</summary>
    MaxRects,
}

/// <summary>Chapa comercial. Comprimento = sentido do veio.</summary>
public sealed class SheetSpec
{
    public string Material { get; set; } = "";
    public double Length { get; set; } = U.Mm(2750);
    public double Width { get; set; } = U.Mm(1850);
    public double Thickness { get; set; } = U.Mm(15);
    public bool HasGrain { get; set; }
    public double Cost { get; set; }
    public string? Code { get; set; }
}

public sealed class PartRequest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Material { get; set; } = "";
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>A peça tem veio: o comprimento deve seguir o comprimento da chapa (sem girar).</summary>
    public bool HasGrain { get; set; }

    public string? Edges { get; set; }
    public string? Module { get; set; }
}

public sealed class NestingSettings
{
    public NestingMethod Method { get; set; } = NestingMethod.Guillotine;
    public double Kerf { get; set; } = U.Mm(4);
    public double Trim { get; set; } = U.Mm(10);
    public bool AllowRotation { get; set; } = true;
    public int RandomRestarts { get; set; } = 12;
    public int Seed { get; set; } = 11;
    public double MinReusableOffcut { get; set; } = U.Mm(300);
}

public sealed class PlacedPart
{
    public PartRequest Part { get; set; } = new();
    public int Instance { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Dimensão ao longo do comprimento da chapa (X).</summary>
    public double SizeX { get; set; }
    public double SizeY { get; set; }
    public bool Rotated { get; set; }
    public Rect2 Rect => Rect2.FromSize(X, Y, SizeX, SizeY);
}

public sealed class SheetLayout
{
    public SheetSpec Sheet { get; set; } = new();
    public int Index { get; set; }
    public List<PlacedPart> Parts { get; } = new();
    public List<Rect2> Offcuts { get; } = new();
    public List<Segment2> Cuts { get; } = new();
    public double SheetArea => Sheet.Length * Sheet.Width;
    public double UsedArea => Parts.Sum(p => p.SizeX * p.SizeY);
    public double Utilization => SheetArea <= 0 ? 0 : UsedArea / SheetArea;
}

public sealed class NestingResult
{
    public List<SheetLayout> Sheets { get; } = new();
    public List<PartRequest> Unplaced { get; } = new();

    public double TotalSheetArea => Sheets.Sum(s => s.SheetArea);
    public double TotalUsedArea => Sheets.Sum(s => s.UsedArea);
    public double Utilization => TotalSheetArea <= 0 ? 0 : TotalUsedArea / TotalSheetArea;
    public double WasteArea => TotalSheetArea - TotalUsedArea;

    public TableData Summary()
    {
        var t = new TableData(Loc.T("Plano de corte — resumo", "Cutting plan — summary"));
        t.AddColumn(Loc.T("Material", "Material"))
         .AddColumn(Loc.T("Espessura (mm)", "Thickness (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Chapas", "Sheets"), ColumnKind.Integer)
         .AddColumn(Loc.T("Peças", "Parts"), ColumnKind.Integer)
         .AddColumn(Loc.T("Aproveitamento", "Yield"), ColumnKind.Percent, 1)
         .AddColumn(Loc.T("Desperdício (m²)", "Waste (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Custo", "Cost"), ColumnKind.Money);
        foreach (var g in Sheets.GroupBy(s => (s.Sheet.Material, s.Sheet.Thickness)))
        {
            var area = g.Sum(s => s.SheetArea);
            var used = g.Sum(s => s.UsedArea);
            t.AddRow(g.Key.Material, U.ToMm(g.Key.Thickness), g.Count(), g.Sum(s => s.Parts.Count),
                area <= 0 ? 0 : used / area, U.SqFtToM2(area - used), g.Sum(s => s.Sheet.Cost));
        }
        return t;
    }

    public TableData PartList()
    {
        var t = new TableData(Loc.T("Plano de corte — peças por chapa", "Cutting plan — parts by sheet"));
        t.AddColumn(Loc.T("Chapa", "Sheet"), ColumnKind.Integer)
         .AddColumn(Loc.T("Material", "Material"))
         .AddColumn(Loc.T("Peça", "Part"))
         .AddColumn(Loc.T("Nome", "Name"))
         .AddColumn(Loc.T("Comprimento (mm)", "Length (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Largura (mm)", "Width (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("X (mm)", "X (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Y (mm)", "Y (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Girada", "Rotated"));
        foreach (var s in Sheets)
            foreach (var p in s.Parts)
                t.AddRow(s.Index + 1, s.Sheet.Material, p.Part.Id, p.Part.Name, U.ToMm(p.Part.Length), U.ToMm(p.Part.Width),
                    U.ToMm(p.X), U.ToMm(p.Y), p.Rotated ? Loc.T("sim", "yes") : Loc.T("não", "no"));
        return t;
    }
}

public static class Nester
{
    /// <summary>Material padrão quando não há chapa cadastrada (MDF 2750 × 1850 mm).</summary>
    public static SheetSpec DefaultSheet(string material, double thickness) => new()
    {
        Material = material,
        Length = U.Mm(2750),
        Width = U.Mm(1850),
        Thickness = thickness,
    };

    public static NestingResult Nest(IEnumerable<PartRequest> parts, IEnumerable<SheetSpec> sheets, NestingSettings s)
    {
        var result = new NestingResult();
        var sheetList = sheets.ToList();
        var groups = parts.Where(p => p.Quantity > 0)
            .GroupBy(p => (p.Material, T: Math.Round(U.ToMm(p.Thickness), 1)));
        var sheetIndex = 0;
        foreach (var g in groups)
        {
            var spec = sheetList.FirstOrDefault(sp => string.Equals(sp.Material, g.Key.Material, StringComparison.OrdinalIgnoreCase)
                                                      && Math.Abs(U.ToMm(sp.Thickness) - g.Key.T) < 0.6)
                       ?? sheetList.FirstOrDefault(sp => string.Equals(sp.Material, g.Key.Material, StringComparison.OrdinalIgnoreCase))
                       ?? DefaultSheet(g.Key.Material, U.Mm(g.Key.T));
            var layouts = NestGroup(g.ToList(), spec, s, result.Unplaced);
            foreach (var l in layouts)
            {
                l.Index = sheetIndex++;
                result.Sheets.Add(l);
            }
        }
        return result;
    }

    private static List<SheetLayout> NestGroup(List<PartRequest> parts, SheetSpec sheet, NestingSettings s, List<PartRequest> unplaced)
    {
        var usableX = sheet.Length - 2 * s.Trim + s.Kerf;
        var usableY = sheet.Width - 2 * s.Trim + s.Kerf;
        var instances = new List<(PartRequest Part, int Inst)>();
        foreach (var p in parts)
        {
            var fitsNormal = p.Length + s.Kerf <= usableX + 1e-9 && p.Width + s.Kerf <= usableY + 1e-9;
            var canRotate = s.AllowRotation && !(p.HasGrain && sheet.HasGrain);
            var fitsRot = canRotate && p.Width + s.Kerf <= usableX + 1e-9 && p.Length + s.Kerf <= usableY + 1e-9;
            if (!fitsNormal && !fitsRot)
            {
                unplaced.Add(p);
                continue;
            }
            for (var i = 0; i < p.Quantity; i++) instances.Add((p, i));
        }
        if (instances.Count == 0) return new List<SheetLayout>();

        var orders = new List<List<(PartRequest Part, int Inst)>>
        {
            instances.OrderByDescending(x => x.Part.Length * x.Part.Width).ToList(),
            instances.OrderByDescending(x => Math.Max(x.Part.Length, x.Part.Width)).ThenByDescending(x => Math.Min(x.Part.Length, x.Part.Width)).ToList(),
            instances.OrderByDescending(x => x.Part.Length + x.Part.Width).ToList(),
            instances.OrderByDescending(x => x.Part.Length).ToList(),
            instances.OrderByDescending(x => x.Part.Width).ToList(),
        };
        var rng = new Rng(s.Seed);
        for (var r = 0; r < s.RandomRestarts; r++)
        {
            var o = orders[0].ToList();
            // Embaralha levemente mantendo as peças grandes primeiro.
            for (var i = 0; i < o.Count - 1; i++)
                if (rng.NextDouble() < 0.3) (o[i], o[i + 1]) = (o[i + 1], o[i]);
            orders.Add(o);
        }

        List<SheetLayout>? best = null;
        List<PartRequest>? bestUnplaced = null;
        var bestScore = double.MaxValue;
        foreach (var order in orders)
        {
            var bins = new List<IBin>();
            var layouts = new List<SheetLayout>();
            var orderUnplaced = new List<PartRequest>();
            foreach (var (part, inst) in order)
            {
                var placed = false;
                for (var b = 0; b < bins.Count && !placed; b++)
                    placed = TryPlace(bins[b], layouts[b], part, inst, sheet, s);
                if (!placed)
                {
                    IBin bin = s.Method == NestingMethod.Guillotine
                        ? new GuillotineBin(usableX, usableY)
                        : new MaxRectsBin(usableX, usableY);
                    bins.Add(bin);
                    var layout = new SheetLayout { Sheet = sheet };
                    layouts.Add(layout);
                    if (!TryPlace(bin, layout, part, inst, sheet, s))
                    {
                        orderUnplaced.Add(part);
                        bins.RemoveAt(bins.Count - 1);
                        layouts.RemoveAt(layouts.Count - 1);
                    }
                }
            }
            for (var b = 0; b < bins.Count; b++)
            {
                foreach (var fr in bins[b].FreeRects)
                {
                    var rr = Rect2.FromSize(fr.MinX + s.Trim, fr.MinY + s.Trim, Math.Max(0, fr.Width - s.Kerf), Math.Max(0, fr.Height - s.Kerf));
                    if (Math.Min(rr.Width, rr.Height) >= s.MinReusableOffcut) layouts[b].Offcuts.Add(rr);
                }
                if (bins[b] is GuillotineBin gb)
                    foreach (var c in gb.Cuts)
                        layouts[b].Cuts.Add(new Segment2(c.A + new Vec2(s.Trim, s.Trim), c.B + new Vec2(s.Trim, s.Trim)));
            }
            var last = layouts.Count > 0 ? layouts[^1].Utilization : 0;
            var score = orderUnplaced.Count * 1e6 + layouts.Count * 1000.0 + last;
            if (score < bestScore)
            {
                bestScore = score;
                best = layouts;
                bestUnplaced = orderUnplaced;
            }
        }
        if (bestUnplaced != null) unplaced.AddRange(bestUnplaced.Distinct());
        return best ?? new List<SheetLayout>();
    }

    private static bool TryPlace(IBin bin, SheetLayout layout, PartRequest part, int inst, SheetSpec sheet, NestingSettings s)
    {
        var canRotate = s.AllowRotation && !(part.HasGrain && sheet.HasGrain);
        if (!bin.Insert(part.Length + s.Kerf, part.Width + s.Kerf, canRotate, out var rect, out var rotated))
            return false;
        layout.Parts.Add(new PlacedPart
        {
            Part = part,
            Instance = inst,
            X = rect.MinX + s.Trim,
            Y = rect.MinY + s.Trim,
            SizeX = rect.Width - s.Kerf,
            SizeY = rect.Height - s.Kerf,
            Rotated = rotated,
        });
        return true;
    }

    private interface IBin
    {
        bool Insert(double w, double h, bool allowRotate, out Rect2 placed, out bool rotated);
        IEnumerable<Rect2> FreeRects { get; }
    }

    /// <summary>MaxRects com heurística "Best Short Side Fit".</summary>
    private sealed class MaxRectsBin : IBin
    {
        private readonly List<Rect2> _free = new();

        public MaxRectsBin(double w, double h) => _free.Add(Rect2.FromSize(0, 0, w, h));

        public IEnumerable<Rect2> FreeRects => _free;

        public bool Insert(double w, double h, bool allowRotate, out Rect2 placed, out bool rotated)
        {
            var bestRect = Rect2.Empty;
            var bestRot = false;
            var bestShort = double.MaxValue;
            var bestLong = double.MaxValue;
            foreach (var f in _free)
            {
                Try(f, w, h, false);
                if (allowRotate) Try(f, h, w, true);
            }
            placed = bestRect;
            rotated = bestRot;
            if (bestRect.IsEmpty) return false;
            Split(bestRect);
            return true;

            void Try(Rect2 f, double pw, double ph, bool rot)
            {
                if (pw > f.Width + 1e-9 || ph > f.Height + 1e-9) return;
                var lw = f.Width - pw;
                var lh = f.Height - ph;
                var sh = Math.Min(lw, lh);
                var lg = Math.Max(lw, lh);
                if (sh < bestShort - 1e-12 || (Math.Abs(sh - bestShort) <= 1e-12 && lg < bestLong))
                {
                    bestShort = sh;
                    bestLong = lg;
                    bestRect = Rect2.FromSize(f.MinX, f.MinY, pw, ph);
                    bestRot = rot;
                }
            }
        }

        private void Split(Rect2 used)
        {
            var newFree = new List<Rect2>();
            for (var i = _free.Count - 1; i >= 0; i--)
            {
                var f = _free[i];
                if (!f.Intersects(used, 1e-9)) continue;
                _free.RemoveAt(i);
                if (used.MinX > f.MinX + 1e-9) newFree.Add(new Rect2(f.MinX, f.MinY, used.MinX, f.MaxY));
                if (used.MaxX < f.MaxX - 1e-9) newFree.Add(new Rect2(used.MaxX, f.MinY, f.MaxX, f.MaxY));
                if (used.MinY > f.MinY + 1e-9) newFree.Add(new Rect2(f.MinX, f.MinY, f.MaxX, used.MinY));
                if (used.MaxY < f.MaxY - 1e-9) newFree.Add(new Rect2(f.MinX, used.MaxY, f.MaxX, f.MaxY));
            }
            _free.AddRange(newFree);
            // Remove retângulos livres contidos em outros.
            for (var i = 0; i < _free.Count; i++)
            {
                for (var j = i + 1; j < _free.Count; j++)
                {
                    if (_free[j].Contains(_free[i]))
                    {
                        _free.RemoveAt(i);
                        i--;
                        break;
                    }
                    if (_free[i].Contains(_free[j]))
                    {
                        _free.RemoveAt(j);
                        j--;
                    }
                }
            }
        }
    }

    /// <summary>Guilhotina com "Best Area Fit" e divisão pelo menor eixo de sobra; registra os cortes.</summary>
    private sealed class GuillotineBin : IBin
    {
        private readonly List<Rect2> _free = new();

        public List<Segment2> Cuts { get; } = new();

        public GuillotineBin(double w, double h) => _free.Add(Rect2.FromSize(0, 0, w, h));

        public IEnumerable<Rect2> FreeRects => _free;

        public bool Insert(double w, double h, bool allowRotate, out Rect2 placed, out bool rotated)
        {
            var bestRect = Rect2.Empty;
            var bestRot = false;
            var bestIdx = -1;
            var bestArea = double.MaxValue;
            var bestShort = double.MaxValue;
            for (var i = 0; i < _free.Count; i++)
            {
                var f = _free[i];
                Consider(i, f, w, h, false);
                if (allowRotate) Consider(i, f, h, w, true);
            }
            placed = bestRect;
            rotated = bestRot;
            if (bestIdx < 0) return false;

            var fr = _free[bestIdx];
            _free.RemoveAt(bestIdx);
            var lw = fr.Width - placed.Width;
            var lh = fr.Height - placed.Height;
            // Divide pelo eixo de menor sobra (mantém a sobra maior inteira).
            if (lw < lh)
            {
                // Corte horizontal primeiro (atravessa a largura toda do retângulo livre).
                if (lh > 1e-9)
                {
                    _free.Add(new Rect2(fr.MinX, placed.MaxY, fr.MaxX, fr.MaxY));
                    Cuts.Add(new Segment2(new Vec2(fr.MinX, placed.MaxY), new Vec2(fr.MaxX, placed.MaxY)));
                }
                if (lw > 1e-9)
                {
                    _free.Add(new Rect2(placed.MaxX, fr.MinY, fr.MaxX, placed.MaxY));
                    Cuts.Add(new Segment2(new Vec2(placed.MaxX, fr.MinY), new Vec2(placed.MaxX, placed.MaxY)));
                }
            }
            else
            {
                if (lw > 1e-9)
                {
                    _free.Add(new Rect2(placed.MaxX, fr.MinY, fr.MaxX, fr.MaxY));
                    Cuts.Add(new Segment2(new Vec2(placed.MaxX, fr.MinY), new Vec2(placed.MaxX, fr.MaxY)));
                }
                if (lh > 1e-9)
                {
                    _free.Add(new Rect2(fr.MinX, placed.MaxY, placed.MaxX, fr.MaxY));
                    Cuts.Add(new Segment2(new Vec2(fr.MinX, placed.MaxY), new Vec2(placed.MaxX, placed.MaxY)));
                }
            }
            return true;

            void Consider(int idx, Rect2 f, double pw, double ph, bool rot)
            {
                if (pw > f.Width + 1e-9 || ph > f.Height + 1e-9) return;
                var area = f.Area - pw * ph;
                var sh = Math.Min(f.Width - pw, f.Height - ph);
                if (area < bestArea - 1e-12 || (Math.Abs(area - bestArea) <= 1e-12 && sh < bestShort))
                {
                    bestArea = area;
                    bestShort = sh;
                    bestIdx = idx;
                    bestRect = Rect2.FromSize(f.MinX, f.MinY, pw, ph);
                    bestRot = rot;
                }
            }
        }
    }
}
