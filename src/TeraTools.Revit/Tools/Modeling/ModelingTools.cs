using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Modeling;

/// <summary>Utilidades comuns das ferramentas de transformação.</summary>
internal static class Xf
{
    public static List<Element> Pick(ToolContext ctx) =>
        ctx.SelectedOrPick("Selecione os elementos", "Select the elements", e => e is not ElementType && e is not View && e.Category != null);

    /// <summary>Extensão do elemento ao longo de um eixo (projeção dos cantos da caixa).</summary>
    public static (double Min, double Max) Extent(Element e, XYZ axis)
    {
        var b = RevitGeo.Box(e);
        if (b == null)
        {
            var a = RevitGeo.Anchor(e);
            var v = a?.DotProduct(axis) ?? 0;
            return (v, v);
        }
        double min = double.MaxValue, max = double.MinValue;
        foreach (var c in Corners(b))
        {
            var v = c.DotProduct(axis);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        return (min, max);
    }

    public static IEnumerable<XYZ> Corners(BoundingBoxXYZ b)
    {
        var t = b.Transform;
        for (var i = 0; i < 8; i++)
        {
            var p = new XYZ((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
            yield return t.OfPoint(p);
        }
    }

    public static XYZ GroupCenter(IEnumerable<Element> els)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var e in els)
        {
            var b = RevitGeo.Box(e);
            if (b == null) continue;
            foreach (var c in Corners(b))
            {
                minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); minZ = Math.Min(minZ, c.Z);
                maxX = Math.Max(maxX, c.X); maxY = Math.Max(maxY, c.Y); maxZ = Math.Max(maxZ, c.Z);
            }
        }
        return minX > maxX ? XYZ.Zero : new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
    }

    public static XYZ Axis(View v, AxisChoice a) => a switch
    {
        AxisChoice.ViewHorizontal => Flat(v.RightDirection),
        AxisChoice.ViewVertical => RevitGeo.IsPlanLike(v) ? Flat(v.UpDirection) : XYZ.BasisZ,
        _ => XYZ.BasisZ,
    };

    private static XYZ Flat(XYZ d)
    {
        var f = new XYZ(d.X, d.Y, 0);
        return f.GetLength() < 1e-9 ? XYZ.BasisX : f.Normalize();
    }

    public static bool TryMove(Document doc, Element e, XYZ delta, ToolContext ctx)
    {
        if (delta.GetLength() < 1e-9) return true;
        try
        {
            ElementTransformUtils.MoveElement(doc, e.Id, delta);
            ctx.Report.Modified();
            return true;
        }
        catch (Exception ex)
        {
            ctx.Report.Warn(Loc.F("Não foi possível mover {0}: {1}", "Could not move {0}: {1}", Lookup.ElementLabel(e), ex.Message), e.Id.Value);
            return false;
        }
    }
}

public enum AxisChoice
{
    [Text("Horizontal da vista (X)", "View horizontal (X)")] ViewHorizontal,
    [Text("Vertical da vista (Y em planta)", "View vertical (Y in plan)")] ViewVertical,
    [Text("Altura (Z)", "Height (Z)")] Height,
}

// =====================================================================================

// =====================================================================================

public enum DistributeMode
{
    [Text("Espaçamento igual entre pontos (mantém o primeiro e o último)", "Equal spacing between points (keep first and last)")] EqualCenters,
    [Text("Vãos livres iguais entre elementos (mantém extremos)", "Equal clear gaps between elements (keep extremes)")] EqualGaps,
    [Text("Espaçamento fixo entre pontos", "Fixed spacing between points")] FixedSpacing,
    [Text("Vão livre fixo entre elementos", "Fixed clear gap between elements")] FixedGap,
}

public enum DistributeReference
{
    [Text("Ponto de inserção (exato)", "Insertion point (exact)")] Insertion,
    [Text("Centro da geometria real", "Real geometry center")] Geometry,
}

public sealed class DistributeOptions
{
    [Opt("Direção", "Direction")]
    public AxisChoice Axis { get; set; } = AxisChoice.ViewHorizontal;

    [Opt("Modo", "Mode")]
    public DistributeMode Mode { get; set; } = DistributeMode.EqualCenters;

    [Opt("Ponto de referência", "Reference point", VisibleIf = "Mode=EqualCenters,FixedSpacing",
        Tip = "Ponto de inserção = o ponto da família/linha de locação (o mais preciso). Os modos de vão usam sempre a geometria real.|Insertion point is the most precise. Gap modes always use real geometry.")]
    public DistributeReference Reference { get; set; } = DistributeReference.Insertion;

    [Opt("Valor (espaçamento/vão)", "Value (spacing/gap)", Kind = OptKind.Length, Min = 0, VisibleIf = "Mode=FixedSpacing,FixedGap")]
    public double Value { get; set; } = U.Mm(600);
}

internal static class GeoExtent
{
    /// <summary>Extensão exata ao longo do eixo pelos vértices dos sólidos (não usa a caixa envolvente).</summary>
    public static (double Min, double Max)? Of(Element e, XYZ axis)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var s in RevitGeo.Solids(e))
            foreach (Edge edge in s.Edges)
                foreach (var p in edge.Tessellate())
                {
                    var v = p.DotProduct(axis);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
        if (min == double.MaxValue && e.Location is LocationCurve lc)
            foreach (var p in lc.Curve.Tessellate())
            {
                var v = p.DotProduct(axis);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        return min == double.MaxValue ? null : (min, max);
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.distribute", Modules.Modeling, "Distribuir", "Distribute", Order = 20, Large = true,
    DescPt = "Distribui móveis, luminárias, painéis e outros elementos com espaçamento igual, vão livre igual ou valores fixos — medindo pelo ponto de inserção ou pela geometria real.",
    DescEn = "Distributes furniture, fixtures, panels and other elements with equal spacing, equal clear gap or fixed values — measured by insertion point or real geometry.",
    HelpPt = "1. Selecione 2 ou mais elementos.\n2. Escolha a direção (horizontal/vertical da vista ou altura) e o modo.\n3. Confira a pré-visualização: cinza = posição atual, laranja = nova posição.\n\nModos 'iguais': o primeiro e o último ficam onde estão. Modos 'fixos': o primeiro fica e os demais se ajustam. Elementos fixados (pin) são ignorados e listados.\n\nO Revit não tem comando de distribuir: esta ferramenta mede pelo ponto de inserção (exato) ou pelos vértices da geometria real — nunca pela caixa envolvente.",
    HelpEn = "Select 2+ elements, choose direction and mode, check the preview (gray = current, orange = new). Pinned elements are skipped.",
    Keywords = "distribuir espacamento igual vao distribute spacing equal gap")]
public sealed class DistributeCommand : ToolCommand
{
    private sealed record Item(Element E, double Ref, double Min, double Max, XYZ Anchor);

    public override void Run(ToolContext ctx)
    {
        var all = Xf.Pick(ctx);
        var pinned = all.Where(e => e.Pinned).ToList();
        foreach (var e in pinned) ctx.Report.Info(Loc.F("Fixado (pin), ignorado: {0}", "Pinned, skipped: {0}", Lookup.ElementLabel(e)), e.Id.Value);
        var els = all.Where(e => !e.Pinned).ToList();
        if (els.Count < 2) ToolContext.Fail("Selecione pelo menos 2 elementos não fixados.", "Select at least 2 unpinned elements.");
        var view = ctx.ActiveView;
        var o = ctx.Ask<DistributeOptions>(preview: x => Preview(view, els, x));
        var (axis, items, deltas) = Plan(view, els, o);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < items.Count; i++)
                Xf.TryMove(ctx.Doc, items[i].E, axis * deltas[i], ctx);
        });
    }

    private static (XYZ Axis, List<Item> Items, double[] Deltas) Plan(View view, List<Element> els, DistributeOptions o)
    {
        var axis = Xf.Axis(view, o.Axis);
        var useGeometry = o.Mode is DistributeMode.EqualGaps or DistributeMode.FixedGap || o.Reference == DistributeReference.Geometry;
        var items = new List<Item>();
        foreach (var e in els)
        {
            var anchor = RevitGeo.Anchor(e) ?? XYZ.Zero;
            var ext = GeoExtent.Of(e, axis);
            var (mn, mx) = ext ?? (anchor.DotProduct(axis), anchor.DotProduct(axis));
            var r = useGeometry ? (mn + mx) / 2 : anchor.DotProduct(axis);
            items.Add(new Item(e, r, mn, mx, anchor));
        }
        items = items.OrderBy(x => x.Ref).ToList();
        var n = items.Count;
        var d = new double[n];
        switch (o.Mode)
        {
            case DistributeMode.EqualCenters:
                for (var i = 0; i < n; i++) d[i] = items[0].Ref + (items[^1].Ref - items[0].Ref) * i / (n - 1) - items[i].Ref;
                break;
            case DistributeMode.FixedSpacing:
                for (var i = 0; i < n; i++) d[i] = items[0].Ref + i * o.Value - items[i].Ref;
                break;
            case DistributeMode.EqualGaps:
            {
                var total = items[^1].Max - items[0].Min;
                var gap = (total - items.Sum(x => x.Max - x.Min)) / (n - 1);
                var pos = items[0].Min;
                for (var i = 0; i < n; i++)
                {
                    d[i] = pos - items[i].Min;
                    pos += items[i].Max - items[i].Min + gap;
                }
                break;
            }
            default:
            {
                var pos = items[0].Min;
                for (var i = 0; i < n; i++)
                {
                    d[i] = pos - items[i].Min;
                    pos += items[i].Max - items[i].Min + o.Value;
                }
                break;
            }
        }
        return (axis, items, d);
    }

    private static PreviewModel Preview(View view, List<Element> els, DistributeOptions o)
    {
        var (axis, items, d) = Plan(view, els, o);
        var pm = new PreviewModel();
        var right = Xf.Axis(view, AxisChoice.ViewHorizontal);
        var up = RevitGeo.IsPlanLike(view) ? Xf.Axis(view, AxisChoice.ViewVertical) : XYZ.BasisZ;
        Core.Geometry.Vec2 P(XYZ p) => new(p.DotProduct(right), p.DotProduct(up));
        var size = Math.Max(U.Mm(80), (items[^1].Max - items[0].Min) / 60);
        for (var i = 0; i < items.Count; i++)
        {
            var a = P(items[i].Anchor);
            var b = P(items[i].Anchor + axis * d[i]);
            pm.Line(a, b, "#9AA3AE", 0.8);
            pm.Dot(a, size, "#9AA3AE");
            pm.Dot(b, size, "#B5652B");
        }
        var moved = d.Count(x => Math.Abs(x) > 1e-6);
        pm.Info = Loc.F("{0} elementos · {1} serão movidos · maior deslocamento {2}", "{0} elements · {1} will move · largest move {2}",
            items.Count, moved, U.FormatMm(d.Length == 0 ? 0 : d.Max(Math.Abs)));
        return pm;
    }
}

// =====================================================================================

public enum RotatePivot
{
    [Text("Ponto de inserção de cada elemento (gira no lugar)", "Insertion point of each element (rotate in place)")] EachCenter,
    [Text("Centro do conjunto", "Center of the set")] SetCenter,
    [Text("Ponto escolhido", "Picked point")] PickPoint,
}

public sealed class RotateOptions
{
    [Opt("Ângulo", "Angle", Kind = OptKind.Angle, Min = -360, Max = 360)]
    public double Angle { get; set; } = 90;

    [Opt("Pivô", "Pivot")]
    public RotatePivot Pivot { get; set; } = RotatePivot.EachCenter;

    [Opt("Variação aleatória (±)", "Random variation (±)", Kind = OptKind.Angle, Min = 0, Max = 180, Tip = "Soma um ângulo aleatório a cada elemento (útil para mobiliário e paisagismo).|Adds a random angle to each element (useful for furniture and landscaping).")]
    public double RandomRange { get; set; }

    [Opt("Semente", "Seed", VisibleIf = "RandomRange!=0")]
    public int Seed { get; set; } = 1;
}

internal static class RotateTools
{
    public static void Rotate(Document doc, ICollection<ElementId> ids, double radians, RotatePivot pivot, XYZ? picked)
    {
        if (pivot == RotatePivot.EachCenter)
        {
            foreach (var id in ids)
            {
                var e = doc.GetElement(id);
                var c = RevitGeo.Anchor(e);
                if (c == null) continue;
                ElementTransformUtils.RotateElement(doc, id, Line.CreateBound(c, c + XYZ.BasisZ), radians);
            }
            return;
        }
        var center = pivot == RotatePivot.PickPoint && picked != null ? picked : Xf.GroupCenter(ids.Select(doc.GetElement));
        ElementTransformUtils.RotateElements(doc, ids, Line.CreateBound(center, center + XYZ.BasisZ), radians);
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.rotate", Modules.Modeling, "Rotacionar", "Rotate", Order = 41, Group = "Transformar|Transform",
    DescPt = "Gira vários elementos de uma vez, cada um no próprio ponto de inserção (o que o Revit não faz), em torno do conjunto ou de um ponto, com variação aleatória opcional.",
    HelpPt = "Útil para girar cadeiras, luminárias e vegetação no lugar. Com 'variação aleatória' cada elemento recebe um ângulo diferente (estável pela semente). Elementos fixados (pin) são ignorados.",
    DescEn = "Rotates elements about their own center, the set center or a point, with optional random variation.",
    Keywords = "rotacionar girar rotate angulo")]
public sealed class RotateCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var all = Xf.Pick(ctx);
        foreach (var e in all.Where(e => e.Pinned)) ctx.Report.Info(Loc.F("Fixado (pin), ignorado: {0}", "Pinned, skipped: {0}", Lookup.ElementLabel(e)), e.Id.Value);
        var els = all.Where(e => !e.Pinned).ToList();
        if (els.Count == 0) ToolContext.Fail("Nenhum elemento não fixado selecionado.", "No unpinned element selected.");
        var o = ctx.Ask<RotateOptions>();
        XYZ? picked = o.Pivot == RotatePivot.PickPoint ? ctx.PickPoint("Clique no pivô da rotação", "Click the rotation pivot") : null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.RandomRange <= 0)
            {
                RotateTools.Rotate(ctx.Doc, els.Select(e => e.Id).ToList(), MathUtil.ToRadians(o.Angle), o.Pivot, picked);
                ctx.Report.Modified(els.Count);
                return;
            }
            var i = 0;
            foreach (var e in els)
            {
                var a = o.Angle + (Rng.Hash(i++, 0, o.Seed) * 2 - 1) * o.RandomRange;
                try
                {
                    RotateTools.Rotate(ctx.Doc, new List<ElementId> { e.Id }, MathUtil.ToRadians(a), o.Pivot == RotatePivot.SetCenter ? RotatePivot.EachCenter : o.Pivot, picked);
                    ctx.Report.Modified();
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("{0}: {1}", "{0}: {1}", Lookup.ElementLabel(e), ex.Message), e.Id.Value);
                }
            }
        });
    }
}

// =====================================================================================

// =====================================================================================

// =====================================================================================

// =====================================================================================

// =====================================================================================

