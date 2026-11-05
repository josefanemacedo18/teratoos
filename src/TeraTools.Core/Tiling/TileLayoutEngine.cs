using System.Globalization;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Tiling;

public enum TileLayout
{
    /// <summary>Paginação alinhada (junta a prumo).</summary>
    Grid,
    /// <summary>Amarração (junta deslocada, ex.: 1/2 ou 1/3).</summary>
    RunningBond,
    /// <summary>Diagonal (45°).</summary>
    Diagonal,
    /// <summary>Espinha de peixe (90°).</summary>
    Herringbone,
    /// <summary>Trama / cestaria (blocos alternados).</summary>
    Basketweave,
    /// <summary>Modular com três tamanhos (2u×2u, u×2u, 2u×u, u×u).</summary>
    ModularThreeSizes,
    /// <summary>Modular pitagórico (peça grande + peça pequena).</summary>
    ModularPythagorean,
    /// <summary>Chevron: réguas com pontas em ângulo formando "V".</summary>
    Chevron,
    /// <summary>Hexágono (tamanho = ponta a ponta).</summary>
    Hexagon,
    /// <summary>Octógono com taco (losango pequeno nos cantos).</summary>
    OctagonDot,
    /// <summary>Opus romano / Versailles com 4 tamanhos (unidade = largura da peça).</summary>
    OpusRomano,
    /// <summary>Madeira em réguas com comprimentos e desencontros aleatórios.</summary>
    PlankRandom,
    /// <summary>Losangos 3D (cubos em perspectiva).</summary>
    Rhombus3D,
}

public enum TileStart
{
    /// <summary>Começa no canto inferior esquerdo do ambiente.</summary>
    Corner,
    /// <summary>Peça centralizada no ambiente.</summary>
    Center,
    /// <summary>Junta centralizada no ambiente.</summary>
    CenterJoint,
    /// <summary>Ponto definido pelo usuário.</summary>
    Custom,
}

public enum TileStatus
{
    Full,
    Cut,
}

/// <summary>Parâmetros de paginação (pés, graus).</summary>
public sealed class TileSettings
{
    public double TileLength { get; set; } = U.Mm(600);
    public double TileWidth { get; set; } = U.Mm(600);
    public double Joint { get; set; } = U.Mm(2);
    public TileLayout Layout { get; set; } = TileLayout.Grid;
    public double BondOffset { get; set; } = 0.5;
    /// <summary>Chevron: ângulo das pontas em relação ao eixo das colunas.</summary>
    public double ChevronAngleDeg { get; set; } = 45;
    /// <summary>Madeira aleatória: menor régua como fração do comprimento (0,2 a 1).</summary>
    public double MinPlankRatio { get; set; } = 0.4;
    /// <summary>Semente dos padrões aleatórios (mesma semente = mesmo resultado).</summary>
    public int Seed { get; set; } = 1;
    public double RotationDeg { get; set; }
    public TileStart Start { get; set; } = TileStart.Center;
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double MinCutSize { get; set; } = U.Mm(50);
    public double LossPercent { get; set; } = 10;
    public int TilesPerBox { get; set; }
    public bool ReuseOffcuts { get; set; } = true;
}

public sealed class Tile
{
    public int Index { get; set; }
    public Polygon2 Outline { get; set; } = Polygon2.Empty;
    public Polygon2 Clipped { get; set; } = Polygon2.Empty;
    public TileStatus Status { get; set; }
    public double Area { get; set; }
    public double FullArea { get; set; }
    public double CutRatio => FullArea <= 0 ? 0 : Area / FullArea;
    public double MinPieceSize { get; set; }
    public string SizeKey { get; set; } = "";
    public double Length { get; set; }
    public double Width { get; set; }
    public double RotationRad { get; set; }
}

public sealed class TileSizeSummary
{
    public string SizeKey { get; set; } = "";
    public double Length { get; set; }
    public double Width { get; set; }
    public int Full { get; set; }
    public int Cut { get; set; }
    public int Needed { get; set; }
    public int ToBuy { get; set; }
    public int Boxes { get; set; }
}

public sealed class TileLayoutResult
{
    public List<Tile> Tiles { get; } = new();
    public double RegionArea { get; set; }
    public double CoveredArea { get; set; }
    public int FullCount => Tiles.Count(t => t.Status == TileStatus.Full);
    public int CutCount => Tiles.Count(t => t.Status == TileStatus.Cut);
    public List<Tile> Slivers { get; } = new();
    public List<TileSizeSummary> BySize { get; } = new();
    public List<Segment2> JointLines { get; } = new();
    public int TilesToBuy => BySize.Sum(b => b.ToBuy);
    public int Boxes => BySize.Sum(b => b.Boxes);
    public double PurchasedArea => BySize.Sum(b => b.ToBuy * b.Length * b.Width);
    public double WastePercent => PurchasedArea <= 0 ? 0 : Math.Max(0, (PurchasedArea - CoveredArea) / PurchasedArea * 100);

    public TableData Summary(string title)
    {
        var t = new TableData(title);
        t.AddColumn(Loc.T("Peça", "Tile"))
         .AddColumn(Loc.T("Inteiras", "Full"), ColumnKind.Integer)
         .AddColumn(Loc.T("Recortadas", "Cut"), ColumnKind.Integer)
         .AddColumn(Loc.T("Necessárias", "Needed"), ColumnKind.Integer)
         .AddColumn(Loc.T("Comprar (c/ perdas)", "To buy (with losses)"), ColumnKind.Integer)
         .AddColumn(Loc.T("Caixas", "Boxes"), ColumnKind.Integer)
         .AddColumn(Loc.T("Área comprada (m²)", "Purchased area (m²)"), ColumnKind.Number);
        foreach (var b in BySize)
            t.AddRow(b.SizeKey, b.Full, b.Cut, b.Needed, b.ToBuy, b.Boxes, U.SqFtToM2(b.ToBuy * b.Length * b.Width));
        return t;
    }
}

public static class TileLayoutEngine
{
    public static TileLayoutResult Generate(Region2 region, TileSettings s, int hardLimit = 200000)
    {
        if (s.TileLength <= 0 || s.TileWidth <= 0)
            throw new ArgumentException(Loc.T("As dimensões da peça devem ser maiores que zero.", "Tile dimensions must be greater than zero."));
        if (s.Joint < 0)
            throw new ArgumentException(Loc.T("A junta não pode ser negativa.", "Joint cannot be negative."));

        var result = new TileLayoutResult { RegionArea = region.Area };
        var rotDeg = s.RotationDeg + (s.Layout == TileLayout.Diagonal ? 45 : 0);
        var rot = MathUtil.ToRadians(rotDeg);
        var refPt = region.Outer.Centroid;

        Vec2 ToLocal(Vec2 p) => (p - refPt).Rotate(-rot);
        Vec2 ToWorld(Vec2 q) => q.Rotate(rot) + refPt;

        var lb = Rect2.FromPoints(region.Outer.Select(ToLocal));
        var L = s.TileLength;
        var W = s.TileWidth;
        var J = s.Joint;

        Vec2 start = s.Start switch
        {
            TileStart.Corner => lb.Min,
            TileStart.CenterJoint => new Vec2(J / 2, J / 2),
            TileStart.Custom => ToLocal(new Vec2(s.StartX, s.StartY)),
            _ => new Vec2(-L / 2, -W / 2),
        };
        start += new Vec2(s.OffsetX, s.OffsetY);

        // Peças no sistema local (convexas). Os padrões poligonais são gerados sem junta, com passo
        // "peça + junta", e depois cada peça é encolhida meia junta: a junta fica certa em qualquer formato.
        IEnumerable<Polygon2> locals = s.Layout switch
        {
            TileLayout.Herringbone => Rects(Herringbone(lb, start, Math.Max(L, W), Math.Min(L, W), J)),
            TileLayout.Basketweave => Rects(Basketweave(lb, start, Math.Max(L, W), Math.Min(L, W), J)),
            TileLayout.ModularThreeSizes => Rects(ThreeSizes(lb, start, Math.Max(L, W), J)),
            TileLayout.ModularPythagorean => Rects(Pythagorean(lb, start, Math.Max(L, W), Math.Min(L, W), J)),
            TileLayout.RunningBond => Rects(Grid(lb, start, L, W, J, s.BondOffset)),
            TileLayout.PlankRandom => Rects(PlankRandom(lb, start, Math.Max(L, W), Math.Min(L, W), J, s.MinPlankRatio, s.Seed)),
            TileLayout.Chevron => Shrink(Chevron(lb, start, Math.Max(L, W) + J, Math.Min(L, W) + J, s.ChevronAngleDeg), J),
            TileLayout.Hexagon => Shrink(Hexagons(lb, start, L, J), J),
            TileLayout.OctagonDot => Shrink(OctagonDot(lb, start, L + J), J),
            TileLayout.OpusRomano => Shrink(OpusRomano(lb, start, Math.Min(L, W) + J), J),
            TileLayout.Rhombus3D => Shrink(Rhombus3D(lb, start, L + J), J),
            _ => Rects(Grid(lb, start, L, W, J, 0)),
        };

        var regionBounds = region.Bounds;
        var idx = 0;
        foreach (var local in locals)
        {
            if (idx >= hardLimit) break;
            if (local.IsEmpty) continue;
            var poly = new Polygon2(local.Select(ToWorld));
            if (!regionBounds.Intersects(poly.Bounds)) continue;
            var lbx = local.Bounds;
            var full = local.Area;
            if (full <= 1e-12) continue;
            var area = region.IntersectionArea(poly);
            if (area <= full * 1e-6) continue;
            var tile = new Tile
            {
                Index = idx++,
                Outline = poly,
                FullArea = full,
                Area = Math.Min(area, full),
                Length = Math.Max(lbx.Width, lbx.Height),
                Width = Math.Min(lbx.Width, lbx.Height),
                RotationRad = rot,
                SizeKey = SizeKey(lbx.Width, lbx.Height),
            };
            if (area >= full * (1 - 1e-6))
            {
                tile.Status = TileStatus.Full;
                tile.Clipped = poly;
                tile.MinPieceSize = Math.Min(lbx.Width, lbx.Height);
            }
            else
            {
                tile.Status = TileStatus.Cut;
                tile.Clipped = region.Outer.ClipByConvex(poly);
                var cb = Rect2.FromPoints(tile.Clipped.Select(ToLocal));
                tile.MinPieceSize = Math.Min(cb.Width, cb.Height);
                if (tile.MinPieceSize < s.MinCutSize) result.Slivers.Add(tile);
            }
            result.Tiles.Add(tile);
            result.CoveredArea += tile.Area;
        }

        // Compras por tamanho de peça
        foreach (var g in result.Tiles.GroupBy(t => t.SizeKey))
        {
            var first = g.First();
            var full = g.Count(t => t.Status == TileStatus.Full);
            var cuts = g.Where(t => t.Status == TileStatus.Cut).Select(t => t.CutRatio).ToList();
            var cutTiles = s.ReuseOffcuts ? PairOffcuts(cuts) : cuts.Count;
            var needed = full + cutTiles;
            var toBuy = (int)Math.Ceiling(needed * (1 + Math.Max(0, s.LossPercent) / 100.0) - 1e-9);
            result.BySize.Add(new TileSizeSummary
            {
                SizeKey = g.Key,
                Length = first.Length,
                Width = first.Width,
                Full = full,
                Cut = cuts.Count,
                Needed = needed,
                ToBuy = toBuy,
                Boxes = s.TilesPerBox > 0 ? (int)Math.Ceiling((double)toBuy / s.TilesPerBox) : 0,
            });
        }
        result.BySize.Sort((a, b) => (b.Length * b.Width).CompareTo(a.Length * a.Width));

        // Linhas de junta (eixo das juntas) para divisão em partes.
        result.JointLines.AddRange(JointLines(result.Tiles, J, regionBounds.Inflate(Math.Max(L, W))));
        return result;
    }

    /// <summary>Peças recortadas que podem sair da mesma peça inteira (estimativa gulosa).</summary>
    public static int PairOffcuts(List<double> ratios)
    {
        var r = ratios.OrderByDescending(x => x).ToList();
        int i = 0, j = r.Count - 1, count = 0;
        while (i <= j)
        {
            if (i < j && r[i] + r[j] <= 0.95) j--;
            i++;
            count++;
        }
        return count;
    }

    public static string SizeKey(double a, double b)
    {
        var l = U.ToMm(Math.Max(a, b));
        var w = U.ToMm(Math.Min(a, b));
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(l):0}x{Math.Round(w):0}");
    }

    private static IEnumerable<Rect2> Grid(Rect2 lb, Vec2 start, double L, double W, double J, double bond)
    {
        var px = L + J;
        var py = W + J;
        var j0 = (int)Math.Floor((lb.MinY - start.Y) / py) - 1;
        var j1 = (int)Math.Ceiling((lb.MaxY - start.Y) / py) + 1;
        for (var j = j0; j <= j1; j++)
        {
            var shift = bond == 0 ? 0 : (((j * bond) % 1) + 1) % 1 * px;
            var x0 = start.X + shift;
            var i0 = (int)Math.Floor((lb.MinX - x0) / px) - 1;
            var i1 = (int)Math.Ceiling((lb.MaxX - x0) / px) + 1;
            for (var i = i0; i <= i1; i++)
                yield return Rect2.FromSize(x0 + i * px, start.Y + j * py, L, W);
        }
    }

    private static IEnumerable<Rect2> Herringbone(Rect2 lb, Vec2 start, double a, double b, double J)
    {
        // Rede: t1 = (a', a'), t2 = (b', -b'); peça horizontal em [0,a]x[0,b] e vertical em (a-b, b+J).
        var ap = a + J;
        var bp = b + J;
        var corners = new[] { lb.Min, lb.Max, new Vec2(lb.MinX, lb.MaxY), new Vec2(lb.MaxX, lb.MinY) }.Select(c => c - start).ToArray();
        var ms = corners.Select(c => (c.X + c.Y) / (2 * ap)).ToArray();
        var ns = corners.Select(c => (c.X - c.Y) / (2 * bp)).ToArray();
        int m0 = (int)Math.Floor(ms.Min()) - 2, m1 = (int)Math.Ceiling(ms.Max()) + 2;
        int n0 = (int)Math.Floor(ns.Min()) - 2, n1 = (int)Math.Ceiling(ns.Max()) + 2;
        for (var m = m0; m <= m1; m++)
        {
            for (var n = n0; n <= n1; n++)
            {
                var o = start + new Vec2(m * ap + n * bp, m * ap - n * bp);
                yield return Rect2.FromSize(o.X, o.Y, a, b);
                yield return Rect2.FromSize(o.X + a - b, o.Y + b + J, b, a);
            }
        }
    }

    private static IEnumerable<Rect2> Basketweave(Rect2 lb, Vec2 start, double L, double W, double J)
    {
        var n = Math.Max(1, (int)Math.Round((L + J) / (W + J)));
        var stack = n * W + (n - 1) * J;
        var cell = Math.Max(L, stack) + J;
        var i0 = (int)Math.Floor((lb.MinX - start.X) / cell) - 1;
        var i1 = (int)Math.Ceiling((lb.MaxX - start.X) / cell) + 1;
        var j0 = (int)Math.Floor((lb.MinY - start.Y) / cell) - 1;
        var j1 = (int)Math.Ceiling((lb.MaxY - start.Y) / cell) + 1;
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var o = start + new Vec2(i * cell, j * cell);
                var horizontal = ((i + j) & 1) == 0;
                for (var k = 0; k < n; k++)
                {
                    yield return horizontal
                        ? Rect2.FromSize(o.X, o.Y + k * (W + J), L, W)
                        : Rect2.FromSize(o.X + k * (W + J), o.Y, W, L);
                }
            }
        }
    }

    private static IEnumerable<Rect2> ThreeSizes(Rect2 lb, Vec2 start, double big, double J)
    {
        // Peça grande L×L; peça pequena u = (L − junta) / 2 → módulo com L×L, u×L, L×u e u×u.
        var u = Math.Max((big - J) / 2, 1e-6);
        var cell = big + J + u + J;
        var i0 = (int)Math.Floor((lb.MinX - start.X) / cell) - 1;
        var i1 = (int)Math.Ceiling((lb.MaxX - start.X) / cell) + 1;
        var j0 = (int)Math.Floor((lb.MinY - start.Y) / cell) - 1;
        var j1 = (int)Math.Ceiling((lb.MaxY - start.Y) / cell) + 1;
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var o = start + new Vec2(i * cell, j * cell);
                yield return Rect2.FromSize(o.X, o.Y, big, big);
                yield return Rect2.FromSize(o.X + big + J, o.Y, u, big);
                yield return Rect2.FromSize(o.X, o.Y + big + J, big, u);
                yield return Rect2.FromSize(o.X + big + J, o.Y + big + J, u, u);
            }
        }
    }

    private static IEnumerable<Rect2> Pythagorean(Rect2 lb, Vec2 start, double a, double b, double J)
    {
        // Rede v1 = (a', b'), v2 = (-b', a'); peça grande na origem e pequena em (a-b, a+J).
        var ap = a + J;
        var bp = b + J;
        var det = ap * ap + bp * bp;
        var corners = new[] { lb.Min, lb.Max, new Vec2(lb.MinX, lb.MaxY), new Vec2(lb.MaxX, lb.MinY) }.Select(c => c - start).ToArray();
        // Resolve c = m v1 + n v2
        var ms = corners.Select(c => (c.X * ap + c.Y * bp) / det).ToArray();
        var ns = corners.Select(c => (-c.X * bp + c.Y * ap) / det).ToArray();
        int m0 = (int)Math.Floor(ms.Min()) - 2, m1 = (int)Math.Ceiling(ms.Max()) + 2;
        int n0 = (int)Math.Floor(ns.Min()) - 2, n1 = (int)Math.Ceiling(ns.Max()) + 2;
        for (var m = m0; m <= m1; m++)
        {
            for (var n = n0; n <= n1; n++)
            {
                var o = start + new Vec2(m * ap - n * bp, m * bp + n * ap);
                yield return Rect2.FromSize(o.X, o.Y, a, a);
                yield return Rect2.FromSize(o.X + a - b, o.Y + a + J, b, b);
            }
        }
    }

    private static IEnumerable<Polygon2> Rects(IEnumerable<Rect2> rs) => rs.Select(r => r.ToPolygon());

    /// <summary>Encolhe cada peça meia junta (peças geradas com passo "peça + junta").</summary>
    private static IEnumerable<Polygon2> Shrink(IEnumerable<Polygon2> polys, double joint)
    {
        foreach (var p in polys)
        {
            var q = joint > 1e-9 ? p.EnsureCounterClockwise().Offset(-joint / 2) : p.EnsureCounterClockwise();
            if (!q.IsEmpty && q.Area > 1e-10) yield return q;
        }
    }

    /// <summary>Faixa de índices que cobre o intervalo [min, max] com passo p a partir de o (com folga).</summary>
    private static (int A, int B) Span(double min, double max, double o, double p, int pad = 2) =>
        ((int)Math.Floor((min - o) / p) - pad, (int)Math.Ceiling((max - o) / p) + pad);

    /// <summary>Chevron: pares de réguas em "V"; colunas verticais, pontas cortadas no eixo da coluna.</summary>
    private static IEnumerable<Polygon2> Chevron(Rect2 lb, Vec2 start, double L, double W, double angleDeg)
    {
        var beta = MathUtil.ToRadians(Math.Clamp(angleDeg, 15, 75));
        var cb = Math.Cos(beta);
        var tb = Math.Tan(beta);
        var lh = L * cb;           // projeção horizontal da régua
        var h = W / cb;            // altura vertical da régua
        var rise = lh * tb;
        var (i0, i1) = Span(lb.MinX, lb.MaxX, start.X, 2 * lh);
        var (j0, j1) = Span(lb.MinY - rise, lb.MaxY, start.Y, h);
        for (var i = i0; i <= i1; i++)
        {
            for (var j = j0; j <= j1; j++)
            {
                var o = start + new Vec2(i * 2 * lh, j * h);
                yield return new Polygon2(new[] { o, o + new Vec2(lh, rise), o + new Vec2(lh, rise + h), o + new Vec2(0, h) });
                yield return new Polygon2(new[] { o + new Vec2(lh, rise), o + new Vec2(2 * lh, 0), o + new Vec2(2 * lh, h), o + new Vec2(lh, rise + h) });
            }
        }
    }

    /// <summary>Hexágonos de lado reto em cima (tamanho nominal = ponta a ponta).</summary>
    private static IEnumerable<Polygon2> Hexagons(Rect2 lb, Vec2 start, double size, double joint)
    {
        var r = size / 2 + joint / Math.Sqrt(3); // circunraio com a junta (passo entre faces paralelas = √3·r)
        var dx = 1.5 * r;
        var dy = Math.Sqrt(3) * r;
        var (i0, i1) = Span(lb.MinX, lb.MaxX, start.X, dx);
        var (j0, j1) = Span(lb.MinY, lb.MaxY, start.Y, dy);
        for (var i = i0; i <= i1; i++)
            for (var j = j0; j <= j1; j++)
            {
                var c = start + new Vec2(i * dx, j * dy + ((i & 1) != 0 ? dy / 2 : 0));
                yield return Polygon2.Regular(6, r, c);
            }
    }

    /// <summary>Octógonos (faces paralelas = passo) com taco em losango nos cantos.</summary>
    private static IEnumerable<Polygon2> OctagonDot(Rect2 lb, Vec2 start, double pitch)
    {
        var circ = pitch / 2 / Math.Cos(Math.PI / 8);
        var dot = pitch * (1 - 1 / Math.Sqrt(2)); // meia diagonal do taco
        var (i0, i1) = Span(lb.MinX, lb.MaxX, start.X, pitch);
        var (j0, j1) = Span(lb.MinY, lb.MaxY, start.Y, pitch);
        for (var i = i0; i <= i1; i++)
            for (var j = j0; j <= j1; j++)
            {
                var c = start + new Vec2(i * pitch, j * pitch);
                yield return Polygon2.Regular(8, circ, c, Math.PI / 8);
                var k = c + new Vec2(pitch / 2, pitch / 2);
                yield return new Polygon2(new[] { k + new Vec2(dot, 0), k + new Vec2(0, dot), k - new Vec2(dot, 0), k - new Vec2(0, dot) });
            }
    }

    /// <summary>
    /// Opus romano com 4 tamanhos num módulo de 6 × 4 unidades: 3×2 (2 peças), 2×2, 2×1 (3 peças) e 1×1 (2 peças).
    /// As fileiras de módulos são deslocadas meio módulo para não formar juntas contínuas.
    /// </summary>
    private static IEnumerable<Polygon2> OpusRomano(Rect2 lb, Vec2 start, double unit)
    {
        (double X, double Y, double W, double H)[] pieces =
        {
            (0, 0, 3, 2), (3, 0, 2, 2), (5, 0, 1, 1), (5, 1, 1, 1),
            (0, 2, 2, 1), (0, 3, 2, 1), (2, 2, 3, 2), (5, 2, 1, 2),
        };
        var mw = 6 * unit;
        var mh = 4 * unit;
        var (j0, j1) = Span(lb.MinY, lb.MaxY, start.Y, mh);
        for (var j = j0; j <= j1; j++)
        {
            var shift = (j & 1) != 0 ? 3 * unit : 0;
            var (i0, i1) = Span(lb.MinX, lb.MaxX, start.X + shift, mw);
            for (var i = i0; i <= i1; i++)
            {
                var o = start + new Vec2(shift + i * mw, j * mh);
                foreach (var p in pieces)
                    yield return Rect2.FromSize(o.X + p.X * unit, o.Y + p.Y * unit, p.W * unit, p.H * unit).ToPolygon();
            }
        }
    }

    /// <summary>Réguas de madeira com comprimentos e desencontros aleatórios (estáveis pela semente).</summary>
    private static IEnumerable<Rect2> PlankRandom(Rect2 lb, Vec2 start, double L, double W, double J, double minRatio, int seed)
    {
        var minLen = L * Math.Clamp(minRatio, 0.2, 1);
        var py = W + J;
        var (j0, j1) = Span(lb.MinY, lb.MaxY, start.Y, py);
        for (var j = j0; j <= j1; j++)
        {
            var rng = new Rng(seed * 7919 + j * 104729);
            var x = start.X - rng.Range(0, L) - Math.Ceiling(Math.Max(0, start.X - lb.MinX) / L + 1) * (L + J);
            while (x < lb.MaxX + L)
            {
                var len = rng.Range(minLen, L);
                yield return Rect2.FromSize(x, start.Y + j * py, len, W);
                x += len + J;
            }
        }
    }

    /// <summary>Losangos 3D: cada hexágono (ponta para cima) dividido em três losangos.</summary>
    private static IEnumerable<Polygon2> Rhombus3D(Rect2 lb, Vec2 start, double side)
    {
        var dx = Math.Sqrt(3) * side;
        var dy = 1.5 * side;
        var (i0, i1) = Span(lb.MinX, lb.MaxX, start.X, dx);
        var (j0, j1) = Span(lb.MinY, lb.MaxY, start.Y, dy);
        Vec2 V(Vec2 c, double deg) => c + new Vec2(Math.Cos(MathUtil.ToRadians(deg)), Math.Sin(MathUtil.ToRadians(deg))) * side;
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var c = start + new Vec2(i * dx + ((j & 1) != 0 ? dx / 2 : 0), j * dy);
                yield return new Polygon2(new[] { c, V(c, 330), V(c, 30), V(c, 90) });
                yield return new Polygon2(new[] { c, V(c, 90), V(c, 150), V(c, 210) });
                yield return new Polygon2(new[] { c, V(c, 210), V(c, 270), V(c, 330) });
            }
    }

    /// <summary>Eixos das juntas: arestas das peças deslocadas meia junta, mescladas quando colineares.</summary>
    private static List<Segment2> JointLines(List<Tile> tiles, double joint, Rect2 clip)
    {
        var groups = new Dictionary<(long, long), List<(double T0, double T1, Vec2 Dir, Vec2 Origin)>>();
        foreach (var t in tiles)
        {
            var o = t.Outline.EnsureCounterClockwise();
            for (var k = 0; k < o.Count; k++)
            {
                var a = o[k];
                var b = o[(k + 1) % o.Count];
                var seg = new Segment2(a, b).Offset(joint / 2);
                var d = seg.Direction;
                // Normaliza a direção para agrupar segmentos colineares.
                if (d.X < -1e-9 || (Math.Abs(d.X) <= 1e-9 && d.Y < 0)) { d = -d; seg = seg.Reversed(); }
                var nrm = d.Perp;
                var c = seg.A.Dot(nrm);
                var ang = Math.Atan2(d.Y, d.X);
                var key = ((long)Math.Round(ang * 1e6), (long)Math.Round(c * 1e5));
                var t0 = seg.A.Dot(d);
                var t1 = seg.B.Dot(d);
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new();
                l.Add((Math.Min(t0, t1), Math.Max(t0, t1), d, nrm * c));
            }
        }
        var lines = new List<Segment2>();
        foreach (var g in groups.Values)
        {
            var sorted = g.OrderBy(x => x.T0).ToList();
            var cur0 = sorted[0].T0;
            var cur1 = sorted[0].T1;
            var dir = sorted[0].Dir;
            var orig = sorted[0].Origin;
            void Flush()
            {
                var a = orig + dir * cur0;
                var b = orig + dir * cur1;
                if ((b - a).Length > Tolerance.ShortCurve && (clip.Contains(a) || clip.Contains(b)))
                    lines.Add(new Segment2(a, b));
            }
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].T0 <= cur1 + joint + 1e-6)
                {
                    cur1 = Math.Max(cur1, sorted[i].T1);
                }
                else
                {
                    Flush();
                    cur0 = sorted[i].T0;
                    cur1 = sorted[i].T1;
                }
            }
            Flush();
        }
        return lines;
    }

    /// <summary>
    /// Procura o deslocamento do ponto inicial que minimiza filetes (recortes muito finos)
    /// e a quantidade de peças compradas — "sugestão de paginação".
    /// </summary>
    public static (TileSettings Best, TileLayoutResult Result) OptimizeStart(Region2 region, TileSettings s, int steps = 6)
    {
        steps = Math.Clamp(steps, 2, 16);
        TileSettings? best = null;
        TileLayoutResult? bestResult = null;
        var bestScore = double.MaxValue;
        var px = s.TileLength + s.Joint;
        var py = s.TileWidth + s.Joint;
        for (var a = 0; a < steps; a++)
        {
            for (var b = 0; b < steps; b++)
            {
                var c = Clone(s);
                c.Start = TileStart.Corner;
                c.OffsetX = s.OffsetX + px * a / steps;
                c.OffsetY = s.OffsetY + py * b / steps;
                var r = Generate(region, c);
                var score = r.Slivers.Count * 1000.0 + r.TilesToBuy * 10.0 + r.CutCount;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                    bestResult = r;
                }
            }
        }
        return (best!, bestResult!);
    }

    public static TileSettings Clone(TileSettings s) => new()
    {
        TileLength = s.TileLength,
        TileWidth = s.TileWidth,
        Joint = s.Joint,
        Layout = s.Layout,
        BondOffset = s.BondOffset,
        RotationDeg = s.RotationDeg,
        Start = s.Start,
        StartX = s.StartX,
        StartY = s.StartY,
        OffsetX = s.OffsetX,
        OffsetY = s.OffsetY,
        MinCutSize = s.MinCutSize,
        LossPercent = s.LossPercent,
        TilesPerBox = s.TilesPerBox,
        ReuseOffcuts = s.ReuseOffcuts,
    };
}
