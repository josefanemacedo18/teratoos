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
    [Text("Centros com espaçamento igual (mantém extremos)", "Equal center spacing (keep extremes)")] EqualCenters,
    [Text("Vãos iguais entre elementos (mantém extremos)", "Equal gaps between elements (keep extremes)")] EqualGaps,
    [Text("Espaçamento fixo entre centros", "Fixed center spacing")] FixedSpacing,
    [Text("Vão fixo entre elementos", "Fixed gap between elements")] FixedGap,
}

public sealed class DistributeOptions
{
    [Opt("Direção", "Direction")]
    public AxisChoice Axis { get; set; } = AxisChoice.ViewHorizontal;

    [Opt("Modo", "Mode")]
    public DistributeMode Mode { get; set; } = DistributeMode.EqualCenters;

    [Opt("Valor (espaçamento/vão)", "Value (spacing/gap)", Kind = OptKind.Length, Min = 0, VisibleIf = "Mode=FixedSpacing,FixedGap")]
    public double Value { get; set; } = U.Mm(600);
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.distribute", Modules.Modeling, "Distribuir", "Distribute", Order = 20, Large = true,
    DescPt = "Distribui elementos uniformemente (centros ou vãos iguais) ou com espaçamento fixo.",
    DescEn = "Distributes elements evenly (equal centers or gaps) or with fixed spacing.",
    HelpPt = "Os elementos são ordenados pela posição na direção escolhida. Nos modos 'iguais', o primeiro e o último ficam no lugar; nos modos 'fixos', o primeiro fica no lugar e os demais são reposicionados.",
    HelpEn = "Elements are sorted by position along the chosen direction. In 'equal' modes the first and last stay; in 'fixed' modes the first stays and the others move.",
    Keywords = "distribuir espacamento igual distribute spacing")]
public sealed class DistributeCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        if (els.Count < 2) ToolContext.Fail("Selecione pelo menos 2 elementos.", "Select at least 2 elements.");
        var o = ctx.Ask<DistributeOptions>();
        var axis = Xf.Axis(ctx.ActiveView, o.Axis);
        var items = els.Select(e => (E: e, Ext: Xf.Extent(e, axis))).OrderBy(x => (x.Ext.Min + x.Ext.Max) / 2).ToList();
        var n = items.Count;
        var targets = new double[n]; // nova posição do início (Min) de cada elemento
        switch (o.Mode)
        {
            case DistributeMode.EqualCenters:
            {
                var c0 = (items[0].Ext.Min + items[0].Ext.Max) / 2;
                var c1 = (items[^1].Ext.Min + items[^1].Ext.Max) / 2;
                for (var i = 0; i < n; i++)
                {
                    var half = (items[i].Ext.Max - items[i].Ext.Min) / 2;
                    targets[i] = c0 + (c1 - c0) * i / (n - 1) - half;
                }
                break;
            }
            case DistributeMode.EqualGaps:
            {
                var total = items[^1].Ext.Max - items[0].Ext.Min;
                var widths = items.Sum(x => x.Ext.Max - x.Ext.Min);
                var gap = (total - widths) / (n - 1);
                var pos = items[0].Ext.Min;
                for (var i = 0; i < n; i++)
                {
                    targets[i] = pos;
                    pos += items[i].Ext.Max - items[i].Ext.Min + gap;
                }
                break;
            }
            case DistributeMode.FixedSpacing:
            {
                var c0 = (items[0].Ext.Min + items[0].Ext.Max) / 2;
                for (var i = 0; i < n; i++)
                    targets[i] = c0 + i * o.Value - (items[i].Ext.Max - items[i].Ext.Min) / 2;
                break;
            }
            default:
            {
                var pos = items[0].Ext.Min;
                for (var i = 0; i < n; i++)
                {
                    targets[i] = pos;
                    pos += items[i].Ext.Max - items[i].Ext.Min + o.Value;
                }
                break;
            }
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < n; i++)
                Xf.TryMove(ctx.Doc, items[i].E, axis * (targets[i] - items[i].Ext.Min), ctx);
        });
    }
}

// =====================================================================================

// =====================================================================================

public enum RotatePivot
{
    [Text("Centro de cada elemento", "Center of each element")] EachCenter,
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
    DescPt = "Gira elementos em torno do próprio centro, do centro do conjunto ou de um ponto, com variação aleatória opcional.",
    DescEn = "Rotates elements about their own center, the set center or a point, with optional random variation.",
    Keywords = "rotacionar girar rotate angulo")]
public sealed class RotateCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
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

