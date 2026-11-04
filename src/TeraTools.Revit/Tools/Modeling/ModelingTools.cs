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

public enum AlignEdge
{
    [Text("Início (esquerda / baixo)", "Start (left / bottom)")] Min,
    [Text("Centro", "Center")] Center,
    [Text("Fim (direita / topo)", "End (right / top)")] Max,
}

public enum AlignReference
{
    [Text("Primeiro elemento selecionado", "First selected element")] First,
    [Text("Último elemento selecionado", "Last selected element")] Last,
    [Text("Extremo do conjunto", "Extreme of the set")] Extreme,
    [Text("Média do conjunto", "Average of the set")] Average,
    [Text("Elemento escolhido no modelo", "Element picked in the model")] PickElement,
    [Text("Ponto escolhido no modelo", "Point picked in the model")] PickPoint,
}

public sealed class AlignOptions
{
    [Opt("Direção", "Direction")]
    public AxisChoice Axis { get; set; } = AxisChoice.ViewHorizontal;

    [Opt("Alinhar por", "Align by")]
    public AlignEdge Edge { get; set; } = AlignEdge.Min;

    [Opt("Referência", "Reference")]
    public AlignReference Reference { get; set; } = AlignReference.First;

    [Opt("Deslocamento adicional", "Extra offset", Kind = OptKind.Length)]
    public double Offset { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.align", Modules.Modeling, "Alinhar", "Align", Order = 10, Large = true,
    DescPt = "Alinha elementos por bordas, centros ou elevação, em relação a um elemento, ponto ou ao conjunto.",
    DescEn = "Aligns elements by edges, centers or elevation, relative to an element, point or the set.",
    HelpPt = "Selecione os elementos (ou execute e selecione). Escolha a direção (horizontal/vertical da vista ou altura), a borda (início, centro, fim) e a referência.\n\n• Em plantas, horizontal = X e vertical = Y da vista.\n• Em cortes e elevações, a vertical é a altura.\n• Elementos fixados (pinned) ou travados são ignorados e listados no relatório.",
    HelpEn = "Select elements (or run and select). Choose the direction, the edge (start, center, end) and the reference.\n\n• In plans, horizontal = view X and vertical = view Y.\n• Pinned or constrained elements are skipped and listed in the report.",
    Keywords = "alinhar align alinhamento faces eixos centros")]
public sealed class AlignCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<AlignOptions>();
        var view = ctx.ActiveView;
        var axis = Xf.Axis(view, o.Axis);
        double Value(Element e)
        {
            var (min, max) = Xf.Extent(e, axis);
            return o.Edge switch { AlignEdge.Min => min, AlignEdge.Max => max, _ => (min + max) / 2 };
        }

        double target;
        switch (o.Reference)
        {
            case AlignReference.Last:
                target = Value(els[^1]);
                break;
            case AlignReference.Extreme:
                target = o.Edge == AlignEdge.Max ? els.Max(Value) : o.Edge == AlignEdge.Min ? els.Min(Value) : els.Average(Value);
                break;
            case AlignReference.Average:
                target = els.Average(Value);
                break;
            case AlignReference.PickElement:
                target = Value(ctx.PickElement("Escolha o elemento de referência", "Pick the reference element"));
                break;
            case AlignReference.PickPoint:
                target = ctx.PickPoint("Clique no ponto de referência", "Click the reference point").DotProduct(axis);
                break;
            default:
                target = Value(els[0]);
                break;
        }
        target += o.Offset;

        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var e in els)
            {
                var d = target - Value(e);
                Xf.TryMove(ctx.Doc, e, axis * d, ctx);
            }
        });
    }
}

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

public enum LinearArrayMode
{
    [Text("Por espaçamento", "By spacing")] Spacing,
    [Text("Distribuir entre dois pontos", "Fit between two points")] FitPoints,
}

public sealed class LinearArrayOptions
{
    [Opt("Modo", "Mode")]
    public LinearArrayMode Mode { get; set; } = LinearArrayMode.Spacing;

    [Opt("Quantidade total (inclui original)", "Total count (includes original)", Min = 2, Max = 2000)]
    public int Count { get; set; } = 5;

    [Opt("Espaçamento horizontal (X da vista)", "Horizontal spacing (view X)", Kind = OptKind.Length, VisibleIf = "Mode=Spacing")]
    public double SpacingX { get; set; } = U.Mm(1000);

    [Opt("Espaçamento vertical (Y da vista)", "Vertical spacing (view Y)", Kind = OptKind.Length, VisibleIf = "Mode=Spacing")]
    public double SpacingY { get; set; }

    [Opt("Espaçamento em altura (Z)", "Height spacing (Z)", Kind = OptKind.Length, VisibleIf = "Mode=Spacing")]
    public double SpacingZ { get; set; }

    [Opt("Linhas (matriz retangular)", "Rows (rectangular array)", Min = 1, Max = 500, VisibleIf = "Mode=Spacing")]
    public int Rows { get; set; } = 1;

    [Opt("Espaçamento entre linhas (Y da vista)", "Row spacing (view Y)", Kind = OptKind.Length, VisibleIf = "Mode=Spacing")]
    public double RowSpacing { get; set; } = U.Mm(1000);

    [Opt("Agrupar o resultado", "Group the result")]
    public bool GroupResult { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.arraylinear", Modules.Modeling, "Matriz linear/retangular", "Linear/rectangular array", Order = 30, Group = "Matrizes|Arrays",
    DescPt = "Cria cópias em linha ou em grade, por espaçamento ou distribuídas entre dois pontos.",
    DescEn = "Creates copies in a line or grid, by spacing or fitted between two points.",
    HelpPt = "As cópias são elementos independentes (não é uma matriz associativa do Revit). Use 'Agrupar o resultado' para mover tudo junto.",
    HelpEn = "Copies are independent elements (not an associative Revit array). Use 'Group the result' to keep them together.",
    Keywords = "matriz array copiar grade grid")]
public sealed class ArrayLinearCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<LinearArrayOptions>();
        var view = ctx.ActiveView;
        var ax = Xf.Axis(view, AxisChoice.ViewHorizontal);
        var ay = RevitGeo.IsPlanLike(view) ? Xf.Axis(view, AxisChoice.ViewVertical) : XYZ.BasisZ;
        XYZ step;
        if (o.Mode == LinearArrayMode.FitPoints)
        {
            var p0 = ctx.PickPoint("Clique no ponto inicial", "Click the start point");
            var p1 = ctx.PickPoint("Clique no ponto final", "Click the end point");
            step = (p1 - p0) / (o.Count - 1);
        }
        else
        {
            step = ax * o.SpacingX + ay * o.SpacingY + XYZ.BasisZ * o.SpacingZ;
        }
        var rows = o.Mode == LinearArrayMode.Spacing ? Math.Max(1, o.Rows) : 1;
        ctx.CheckCount(els.Count * (o.Count * rows - 1), "cópias", "copies");
        var ids = els.Select(e => e.Id).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var all = new List<ElementId>(ids);
            for (var r = 0; r < rows; r++)
            {
                for (var i = 0; i < o.Count; i++)
                {
                    if (r == 0 && i == 0) continue;
                    var t = step * i + ay * (o.RowSpacing * r);
                    var copies = ElementTransformUtils.CopyElements(ctx.Doc, ids, t);
                    all.AddRange(copies);
                    ctx.Report.Created(copies.Count);
                    foreach (var c in copies) ctx.Report.AddResult(c.Value);
                }
            }
            if (o.GroupResult) GroupTools.MakeGroup(ctx, all, null);
        });
    }
}

public sealed class RadialArrayOptions
{
    [Opt("Quantidade total (inclui original)", "Total count (includes original)", Min = 2, Max = 2000)]
    public int Count { get; set; } = 8;

    [Opt("Ângulo total", "Total angle", Kind = OptKind.Angle, Min = -360, Max = 360)]
    public double TotalAngle { get; set; } = 360;

    [Opt("Girar as cópias", "Rotate the copies", Tip = "Desligado: as cópias mudam de posição sem girar.|Off: copies move without rotating.")]
    public bool RotateCopies { get; set; } = true;

    [Opt("Agrupar o resultado", "Group the result")]
    public bool GroupResult { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.arrayradial", Modules.Modeling, "Matriz radial", "Radial array", Order = 31, Group = "Matrizes|Arrays",
    DescPt = "Cria cópias em torno de um centro escolhido, com ângulo total configurável.",
    DescEn = "Creates copies around a picked center, with configurable total angle.",
    HelpPt = "Com 360°, as cópias se distribuem por toda a volta; com outros ângulos, a última cópia cai exatamente no ângulo final.",
    HelpEn = "With 360° copies spread around the full circle; with other angles the last copy lands exactly on the end angle.",
    Keywords = "matriz radial polar circular array")]
public sealed class ArrayRadialCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<RadialArrayOptions>();
        var center = ctx.PickPoint("Clique no centro da matriz", "Click the array center");
        var full = Math.Abs(Math.Abs(o.TotalAngle) - 360) < 1e-6;
        var step = MathUtil.ToRadians(o.TotalAngle) / (full ? o.Count : o.Count - 1);
        var ids = els.Select(e => e.Id).ToList();
        var groupCenter = Xf.GroupCenter(els);
        ctx.CheckCount(els.Count * (o.Count - 1), "cópias", "copies");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var all = new List<ElementId>(ids);
            var axis = Line.CreateBound(center, center + XYZ.BasisZ);
            for (var i = 1; i < o.Count; i++)
            {
                var a = step * i;
                ICollection<ElementId> copies;
                if (o.RotateCopies)
                {
                    copies = ElementTransformUtils.CopyElements(ctx.Doc, ids, XYZ.Zero);
                    ElementTransformUtils.RotateElements(ctx.Doc, copies, axis, a);
                }
                else
                {
                    var v = groupCenter - center;
                    var rotated = new XYZ(v.X * Math.Cos(a) - v.Y * Math.Sin(a), v.X * Math.Sin(a) + v.Y * Math.Cos(a), v.Z);
                    copies = ElementTransformUtils.CopyElements(ctx.Doc, ids, rotated - v);
                }
                all.AddRange(copies);
                ctx.Report.Created(copies.Count);
                foreach (var c in copies) ctx.Report.AddResult(c.Value);
            }
            if (o.GroupResult) GroupTools.MakeGroup(ctx, all, null);
        });
    }
}

// =====================================================================================

public enum RotatePivot
{
    [Text("Centro de cada elemento", "Center of each element")] EachCenter,
    [Text("Centro do conjunto", "Center of the set")] SetCenter,
    [Text("Ponto escolhido", "Picked point")] PickPoint,
}

public sealed class CopyOffsetOptions
{
    [Opt("Deslocamento X da vista", "View X offset", Kind = OptKind.Length, Group = "Deslocamento|Offset")]
    public double Dx { get; set; } = U.Mm(1000);

    [Opt("Deslocamento Y da vista", "View Y offset", Kind = OptKind.Length, Group = "Deslocamento|Offset")]
    public double Dy { get; set; }

    [Opt("Deslocamento Z", "Z offset", Kind = OptKind.Length, Group = "Deslocamento|Offset")]
    public double Dz { get; set; }

    [Opt("Rotação de cada cópia", "Rotation per copy", Kind = OptKind.Angle, Group = "Rotação|Rotation")]
    public double Angle { get; set; }

    [Opt("Pivô da rotação", "Rotation pivot", Group = "Rotação|Rotation")]
    public RotatePivot Pivot { get; set; } = RotatePivot.EachCenter;

    [Opt("Número de cópias", "Number of copies", Min = 1, Max = 2000, Group = "Cópias|Copies", Tip = "Cada cópia é deslocada (e girada) em relação à anterior.|Each copy is offset (and rotated) from the previous one.")]
    public int Copies { get; set; } = 1;

    [Opt("Mover em vez de copiar", "Move instead of copy", Group = "Cópias|Copies")]
    public bool MoveOnly { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.copy", Modules.Modeling, "Copiar com deslocamento", "Copy with offset", Order = 40, Group = "Transformar|Transform",
    DescPt = "Copia (ou move) elementos com deslocamento numérico e rotação, quantas vezes quiser.",
    DescEn = "Copies (or moves) elements with numeric offset and rotation, as many times as needed.",
    Keywords = "copiar mover deslocamento rotacao copy move offset")]
public sealed class CopyOffsetCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<CopyOffsetOptions>();
        var view = ctx.ActiveView;
        var ax = Xf.Axis(view, AxisChoice.ViewHorizontal);
        var ay = RevitGeo.IsPlanLike(view) ? Xf.Axis(view, AxisChoice.ViewVertical) : XYZ.BasisZ;
        var delta = ax * o.Dx + ay * o.Dy + XYZ.BasisZ * o.Dz;
        XYZ? picked = o.Pivot == RotatePivot.PickPoint && Math.Abs(o.Angle) > 1e-9 ? ctx.PickPoint("Clique no pivô da rotação", "Click the rotation pivot") : null;
        var copies = o.MoveOnly ? 1 : o.Copies;
        ctx.CheckCount(els.Count * copies, "elementos", "elements");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            ICollection<ElementId> current = els.Select(e => e.Id).ToList();
            for (var i = 0; i < copies; i++)
            {
                var next = o.MoveOnly ? current : ElementTransformUtils.CopyElements(ctx.Doc, current, delta);
                if (o.MoveOnly) ElementTransformUtils.MoveElements(ctx.Doc, next, delta);
                if (Math.Abs(o.Angle) > 1e-9)
                    RotateTools.Rotate(ctx.Doc, next, MathUtil.ToRadians(o.Angle), o.Pivot, picked);
                if (!o.MoveOnly) ctx.Report.Created(next.Count);
                else ctx.Report.Modified(next.Count);
                foreach (var c in next) ctx.Report.AddResult(c.Value);
                current = next;
            }
        });
    }
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

public enum MirrorAxis
{
    [Text("Eixo vertical pelo centro do conjunto", "Vertical axis through set center")] VerticalCenter,
    [Text("Eixo horizontal pelo centro do conjunto", "Horizontal axis through set center")] HorizontalCenter,
    [Text("Dois pontos escolhidos", "Two picked points")] TwoPoints,
    [Text("Linha, grade ou plano de referência", "Line, grid or reference plane")] PickLine,
}

public sealed class MirrorOptions
{
    [Opt("Eixo de espelhamento", "Mirror axis")]
    public MirrorAxis Axis { get; set; } = MirrorAxis.VerticalCenter;

    [Opt("Manter os originais (criar cópia)", "Keep originals (create copy)")]
    public bool KeepOriginal { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.mirror", Modules.Modeling, "Espelhar", "Mirror", Order = 42, Group = "Transformar|Transform",
    DescPt = "Espelha elementos por um eixo do conjunto, por dois pontos ou por uma linha/grade/plano.",
    DescEn = "Mirrors elements about a set axis, two points or a line/grid/plane.",
    Keywords = "espelhar mirror simetria")]
public sealed class MirrorCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<MirrorOptions>();
        var view = ctx.ActiveView;
        var ids = els.Select(e => e.Id).ToList();
        if (!ElementTransformUtils.CanMirrorElements(ctx.Doc, ids))
            ToolContext.Fail("Alguns elementos selecionados não podem ser espelhados.", "Some selected elements cannot be mirrored.");
        Plane plane;
        var c = Xf.GroupCenter(els);
        switch (o.Axis)
        {
            case MirrorAxis.HorizontalCenter:
                plane = Plane.CreateByNormalAndOrigin(RevitGeo.IsPlanLike(view) ? Xf.Axis(view, AxisChoice.ViewVertical) : XYZ.BasisZ, c);
                break;
            case MirrorAxis.TwoPoints:
            {
                var a = ctx.PickPoint("Primeiro ponto do eixo", "First axis point");
                var b = ctx.PickPoint("Segundo ponto do eixo", "Second axis point");
                var dir = new XYZ(b.X - a.X, b.Y - a.Y, 0);
                if (dir.GetLength() < 1e-6) ctx.Cancel();
                plane = Plane.CreateByNormalAndOrigin(dir.Normalize().CrossProduct(XYZ.BasisZ), a);
                break;
            }
            case MirrorAxis.PickLine:
            {
                var e = ctx.PickElement("Escolha a linha, grade ou plano de referência", "Pick the line, grid or reference plane",
                    x => x is CurveElement || x is Grid || x is ReferencePlane);
                Curve? curve = e switch
                {
                    CurveElement ce => ce.GeometryCurve,
                    Grid g => g.Curve,
                    ReferencePlane rp => Line.CreateBound(rp.BubbleEnd, rp.FreeEnd),
                    _ => null,
                };
                if (curve == null) ctx.Cancel();
                var p0 = curve!.GetEndPoint(0);
                var p1 = curve.GetEndPoint(1);
                var dir = new XYZ(p1.X - p0.X, p1.Y - p0.Y, 0);
                plane = Plane.CreateByNormalAndOrigin(dir.Normalize().CrossProduct(XYZ.BasisZ), p0);
                break;
            }
            default:
                plane = Plane.CreateByNormalAndOrigin(Xf.Axis(view, AxisChoice.ViewHorizontal), c);
                break;
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var result = ElementTransformUtils.MirrorElements(ctx.Doc, ids, plane, o.KeepOriginal);
            if (o.KeepOriginal) ctx.Report.Created(result.Count);
            else ctx.Report.Modified(ids.Count);
            foreach (var r in result) ctx.Report.AddResult(r.Value);
        });
    }
}

// =====================================================================================

public enum ElevationMode
{
    [Text("Definir elevação em relação ao nível", "Set elevation from level")] Set,
    [Text("Somar deslocamento vertical", "Add vertical offset")] Offset,
    [Text("Igualar à base de um elemento escolhido", "Match the base of a picked element")] MatchBase,
    [Text("Igualar ao topo de um elemento escolhido", "Match the top of a picked element")] MatchTop,
}

public sealed class ElevationOptions
{
    [Opt("Modo", "Mode")]
    public ElevationMode Mode { get; set; } = ElevationMode.Set;

    [Opt("Valor", "Value", Kind = OptKind.Length, VisibleIf = "Mode=Set,Offset")]
    public double Value { get; set; } = U.Mm(900);
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.elevation", Modules.Modeling, "Posição e elevação", "Position and elevation", Order = 50, Group = "Transformar|Transform",
    DescPt = "Ajusta a elevação de famílias, pisos, forros e paredes (por parâmetro) ou move outros elementos em Z.",
    DescEn = "Adjusts the elevation of families, floors, ceilings and walls (by parameter) or moves other elements in Z.",
    HelpPt = "Usa o parâmetro nativo de deslocamento do elemento quando existe (elevação a partir do nível, deslocamento do hospedeiro, altura acima do nível, deslocamento da base). Caso contrário, move o elemento verticalmente.",
    HelpEn = "Uses the element's native offset parameter when available; otherwise moves the element vertically.",
    Keywords = "elevacao altura cota offset nivel elevation height")]
public sealed class ElevationCommand : ToolCommand
{
    private static readonly BuiltInParameter[] OffsetParams =
    {
        BuiltInParameter.INSTANCE_ELEVATION_PARAM,
        BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM,
        BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM,
        BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM,
        BuiltInParameter.WALL_BASE_OFFSET,
        BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM,
    };

    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<ElevationOptions>();
        double? matchZ = null;
        if (o.Mode is ElevationMode.MatchBase or ElevationMode.MatchTop)
        {
            var refEl = ctx.PickElement("Escolha o elemento de referência", "Pick the reference element");
            var b = RevitGeo.Box(refEl) ?? throw new ToolException(Loc.T("Elemento sem geometria.", "Element without geometry."));
            matchZ = o.Mode == ElevationMode.MatchBase ? b.Min.Z : b.Max.Z;
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var e in els)
            {
                var param = OffsetParams.Select(bp => e.get_Parameter(bp)).FirstOrDefault(p => p is { IsReadOnly: false, StorageType: StorageType.Double });
                var box = RevitGeo.Box(e);
                double delta;
                if (o.Mode == ElevationMode.Offset)
                {
                    delta = o.Value;
                }
                else if (o.Mode == ElevationMode.Set)
                {
                    if (param != null)
                    {
                        param.Set(o.Value);
                        ctx.Report.Modified();
                        continue;
                    }
                    var lvl = RevitGeo.LevelOf(e);
                    delta = (lvl?.Elevation ?? 0) + o.Value - (box?.Min.Z ?? 0);
                }
                else
                {
                    delta = matchZ!.Value - (box?.Min.Z ?? 0);
                }
                if (param != null)
                {
                    param.Set(param.AsDouble() + delta);
                    ctx.Report.Modified();
                }
                else
                {
                    Xf.TryMove(ctx.Doc, e, XYZ.BasisZ * delta, ctx);
                }
            }
        });
    }
}

// =====================================================================================

public enum ResizeOperation
{
    [Text("Definir valor", "Set value")] Set,
    [Text("Somar", "Add")] Add,
    [Text("Multiplicar (escala)", "Multiply (scale)")] Multiply,
}

public sealed class ResizeOptions
{
    [Opt("Parâmetros (em ordem de preferência)", "Parameters (in order of preference)", Tip = "Separados por ponto e vírgula. O primeiro encontrado em cada elemento é usado.|Separated by semicolons. The first found on each element is used.")]
    public string Parameters { get; set; } = "TT_Largura;Largura;Width;Comprimento;Length";

    [Opt("Operação", "Operation")]
    public ResizeOperation Operation { get; set; } = ResizeOperation.Set;

    [Opt("Valor", "Value", Kind = OptKind.Length, VisibleIf = "Operation=Set,Add")]
    public double Value { get; set; } = U.Mm(600);

    [Opt("Fator", "Factor", Min = 0.001, Max = 1000, VisibleIf = "Operation=Multiply")]
    public double Factor { get; set; } = 1.1;

    [Opt("Alterar também parâmetros de tipo", "Also change type parameters", Tip = "Afeta TODAS as instâncias do tipo.|Affects ALL instances of the type.")]
    public bool AllowType { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.resize", Modules.Modeling, "Redimensionar", "Resize", Order = 60, Group = "Transformar|Transform",
    DescPt = "Altera dimensões de elementos compatíveis via parâmetros (definir, somar ou escalar).",
    DescEn = "Changes dimensions of compatible elements through parameters (set, add or scale).",
    HelpPt = "Funciona com qualquer família que tenha parâmetros de comprimento (ex.: Largura, Altura, Comprimento). Elementos sem o parâmetro são listados no relatório.",
    HelpEn = "Works with any family that has length parameters (e.g., Width, Height, Length). Elements without the parameter are listed in the report.",
    Keywords = "redimensionar tamanho largura altura escala resize scale")]
public sealed class ResizeCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<ResizeOptions>();
        var names = o.Parameters.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        var doneTypes = new HashSet<long>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var e in els)
            {
                var p = FindLength(e, names);
                if (p == null && o.AllowType && ctx.Doc.GetElement(e.GetTypeId()) is { } type)
                {
                    if (!doneTypes.Add(type.Id.Value)) continue;
                    p = FindLength(type, names);
                }
                if (p == null)
                {
                    ctx.Report.Warn(Loc.F("{0}: nenhum parâmetro editável ({1}).", "{0}: no editable parameter ({1}).", Lookup.ElementLabel(e), string.Join(", ", names)), e.Id.Value);
                    continue;
                }
                var v = o.Operation switch
                {
                    ResizeOperation.Add => p.AsDouble() + o.Value,
                    ResizeOperation.Multiply => p.AsDouble() * o.Factor,
                    _ => o.Value,
                };
                if (v <= 0)
                {
                    ctx.Report.Warn(Loc.F("{0}: valor resultante inválido.", "{0}: invalid resulting value.", Lookup.ElementLabel(e)), e.Id.Value);
                    continue;
                }
                p.Set(v);
                ctx.Report.Modified();
            }
        });
    }

    private static Parameter? FindLength(Element e, string[] names)
    {
        foreach (var n in names)
        {
            var p = e.LookupParameter(n);
            if (p is { IsReadOnly: false, StorageType: StorageType.Double }) return p;
        }
        return null;
    }
}

// =====================================================================================

internal static class GroupTools
{
    public static Group? MakeGroup(ToolContext ctx, ICollection<ElementId> ids, string? name)
    {
        try
        {
            var g = ctx.Doc.Create.NewGroup(ids);
            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    g.GroupType.Name = name;
                }
                catch
                {
                    ctx.Report.Warn(Loc.F("Nome de grupo \"{0}\" já existe; mantido o nome automático.", "Group name \"{0}\" already exists; automatic name kept.", name));
                }
            }
            ctx.Report.Count(Loc.T("Grupos criados", "Groups created"));
            ctx.Report.AddResult(g.Id.Value);
            return g;
        }
        catch (Exception ex)
        {
            ctx.Report.Warn(Loc.F("Não foi possível agrupar: {0}", "Could not group: {0}", ex.Message));
            return null;
        }
    }
}

public sealed class GroupOptions
{
    [Opt("Nome do grupo", "Group name")]
    public string Name { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.group", Modules.Modeling, "Agrupar", "Group", Order = 70, Group = "Grupos|Groups",
    DescPt = "Cria um grupo de modelo com os elementos selecionados, com nome opcional.",
    DescEn = "Creates a model group from the selected elements, with an optional name.",
    Keywords = "agrupar grupo group")]
public sealed class GroupCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Xf.Pick(ctx);
        var o = ctx.Ask<GroupOptions>();
        ctx.Modify(ctx.Tool.Name, () => GroupTools.MakeGroup(ctx, els.Select(e => e.Id).ToList(), o.Name));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.ungroup", Modules.Modeling, "Desagrupar", "Ungroup", Order = 71, Group = "Grupos|Groups",
    DescPt = "Desfaz todos os grupos selecionados (os membros ficam selecionados).",
    DescEn = "Ungroups all selected groups (members remain selected).",
    Keywords = "desagrupar ungroup grupo")]
public sealed class UngroupCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var groups = ctx.SelectedOrPick("Selecione os grupos", "Select the groups", e => e is Group).OfType<Group>().ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var g in groups)
            {
                var members = g.UngroupMembers();
                foreach (var m in members) ctx.Report.AddResult(m.Value);
                ctx.Report.Count(Loc.T("Grupos desfeitos", "Groups ungrouped"));
            }
        });
    }
}

// =====================================================================================

[Transaction(TransactionMode.Manual)]
[Tool("modeling.replacetype", Modules.Modeling, "Substituir família/tipo", "Replace family/type", Order = 80, Large = true,
    DescPt = "Troca o tipo (ou a família) dos elementos selecionados, mantendo posição e parâmetros de instância compatíveis.",
    DescEn = "Swaps the type (or family) of selected elements, keeping position and compatible instance parameters.",
    HelpPt = "Selecione elementos de uma mesma categoria. A lista mostra todos os tipos compatíveis carregados no projeto (inclusive de outras famílias da mesma categoria). Valores de parâmetros de instância com o mesmo nome são restaurados após a troca.",
    HelpEn = "Select elements of the same category. The list shows all compatible types loaded in the project. Instance parameter values with the same name are restored after the swap.",
    Keywords = "substituir trocar tipo familia replace swap type")]
public sealed class ReplaceTypeCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione os elementos a substituir", "Select the elements to replace",
            e => e is not ElementType && e.GetTypeId() != ElementId.InvalidElementId && e.Category != null);
        var catIds = els.Select(e => e.Category.Id.Value).Distinct().ToList();
        if (catIds.Count > 1)
            ToolContext.Fail("Selecione elementos de uma única categoria.", "Select elements of a single category.");
        var first = els[0];
        var validTypeIds = first.GetValidTypes();
        var types = validTypeIds.Select(id => ctx.Doc.GetElement(id)).OfType<ElementType>()
            .OrderBy(t => t.FamilyName).ThenBy(t => t.Name, Core.Text.TextUtil.NaturalComparer).ToList();
        if (types.Count == 0) ToolContext.Fail("Não há outros tipos compatíveis no projeto.", "There are no other compatible types in the project.");
        var target = Pick.One(types, t => $"{t.FamilyName} : {t.Name}", ctx.Tool.Name, Loc.T("Escolha o novo tipo:", "Choose the new type:"), ctx.Owner);
        if (target == null) ctx.Cancel();

        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (target is FamilySymbol fs && !fs.IsActive) fs.Activate();
            foreach (var e in els)
            {
                if (e.GetTypeId() == target!.Id)
                {
                    ctx.Report.Skipped();
                    continue;
                }
                var snapshot = Snapshot(e);
                try
                {
                    e.ChangeTypeId(target.Id);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("{0}: {1}", "{0}: {1}", Lookup.ElementLabel(e), ex.Message), e.Id.Value);
                    continue;
                }
                var restored = Restore(e, snapshot);
                ctx.Report.Modified();
                if (restored > 0) ctx.Report.Count(Loc.T("Parâmetros restaurados", "Parameters restored"), restored);
                ctx.Report.AddResult(e.Id.Value);
            }
        });
    }

    private static List<(string Name, StorageType Type, object? Value)> Snapshot(Element e)
    {
        var list = new List<(string, StorageType, object?)>();
        foreach (Parameter p in e.Parameters)
        {
            if (p.IsReadOnly || !p.HasValue || p.Definition == null) continue;
            if (p.Definition is InternalDefinition id && id.BuiltInParameter is BuiltInParameter.ELEM_TYPE_PARAM or BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM or BuiltInParameter.ELEM_FAMILY_PARAM or BuiltInParameter.SYMBOL_ID_PARAM)
                continue;
            object? v = p.StorageType switch
            {
                StorageType.Double => p.AsDouble(),
                StorageType.Integer => p.AsInteger(),
                StorageType.String => p.AsString(),
                StorageType.ElementId => p.AsElementId(),
                _ => null,
            };
            list.Add((p.Definition.Name, p.StorageType, v));
        }
        return list;
    }

    private static int Restore(Element e, List<(string Name, StorageType Type, object? Value)> snap)
    {
        var n = 0;
        foreach (var (name, type, value) in snap)
        {
            var p = e.LookupParameter(name);
            if (p == null || p.IsReadOnly || p.StorageType != type || value == null) continue;
            try
            {
                switch (type)
                {
                    case StorageType.Double when Math.Abs(p.AsDouble() - (double)value) > 1e-9:
                        p.Set((double)value);
                        n++;
                        break;
                    case StorageType.Integer when p.AsInteger() != (int)value:
                        p.Set((int)value);
                        n++;
                        break;
                    case StorageType.String when p.AsString() != (string)value:
                        p.Set((string)value);
                        n++;
                        break;
                    case StorageType.ElementId when p.AsElementId() != (ElementId)value:
                        p.Set((ElementId)value);
                        n++;
                        break;
                }
            }
            catch
            {
                // valor não aceito pelo novo tipo
            }
        }
        return n;
    }
}

// =====================================================================================

public enum RefKind
{
    [Text("Linha de modelo", "Model line")] ModelLine,
    [Text("Linha de detalhe (só na vista)", "Detail line (view only)")] DetailLine,
    [Text("Plano de referência", "Reference plane")] ReferencePlane,
}

public enum RefMode
{
    [Text("Clicar pares de pontos (Esc para terminar)", "Click point pairs (Esc to finish)")] Points,
    [Text("Ligar centros dos elementos selecionados", "Connect centers of selected elements")] Centers,
    [Text("Eixos X e Y por cada elemento selecionado", "X and Y axes through each selected element")] Axes,
    [Text("Marcar centro (cruz) de cada elemento", "Mark center (cross) of each element")] Cross,
}

public sealed class RefLineOptions
{
    [Opt("Criar", "Create")]
    public RefKind Kind { get; set; } = RefKind.ModelLine;

    [Opt("Modo", "Mode")]
    public RefMode Mode { get; set; } = RefMode.Points;

    [Opt("Comprimento dos eixos/cruzes", "Axis/cross length", Kind = OptKind.Length, VisibleIf = "Mode=Axes,Cross")]
    public double Length { get; set; } = U.Mm(1000);
}

[Transaction(TransactionMode.Manual)]
[Tool("modeling.refline", Modules.Modeling, "Pontos e linhas auxiliares", "Reference points and lines", Order = 90,
    DescPt = "Cria linhas de modelo/detalhe ou planos de referência por pontos, centros ou eixos dos elementos.",
    DescEn = "Creates model/detail lines or reference planes from points, element centers or axes.",
    Keywords = "linha auxiliar plano referencia ponto centro reference line plane")]
public sealed class RefLineCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<RefLineOptions>();
        var view = ctx.ActiveView;
        if (o.Kind == RefKind.DetailLine && view is View3D)
            ToolContext.Fail("Linhas de detalhe não podem ser criadas em vistas 3D.", "Detail lines cannot be created in 3D views.");
        var segments = new List<(XYZ A, XYZ B)>();
        if (o.Mode == RefMode.Points)
        {
            while (true)
            {
                try
                {
                    var a = ctx.PickPoint("Primeiro ponto (Esc termina)", "First point (Esc finishes)");
                    var b = ctx.PickPoint("Segundo ponto", "Second point");
                    if (a.DistanceTo(b) > Core.Geometry.Tolerance.ShortCurve) segments.Add((a, b));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
            }
        }
        else
        {
            var els = Xf.Pick(ctx);
            var centers = els.Select(RevitGeo.Anchor).Where(c => c != null).Select(c => c!).ToList();
            var h = o.Length / 2;
            switch (o.Mode)
            {
                case RefMode.Centers:
                    for (var i = 0; i + 1 < centers.Count; i++) segments.Add((centers[i], centers[i + 1]));
                    break;
                case RefMode.Axes:
                case RefMode.Cross:
                    foreach (var c in centers)
                    {
                        segments.Add((c - XYZ.BasisX * h, c + XYZ.BasisX * h));
                        segments.Add((c - XYZ.BasisY * h, c + XYZ.BasisY * h));
                    }
                    break;
            }
        }
        if (segments.Count == 0) ctx.Cancel();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var (a, b) in segments)
            {
                Element? created = null;
                switch (o.Kind)
                {
                    case RefKind.ReferencePlane:
                        var dir = (b - a).Normalize();
                        var cut = Math.Abs(dir.Z) < 0.99 ? XYZ.BasisZ : XYZ.BasisX;
                        created = ctx.Doc.Create.NewReferencePlane(a, b, cut, view);
                        break;
                    case RefKind.DetailLine:
                        var pa = ProjectToView(view, a);
                        var pb = ProjectToView(view, b);
                        if (pa.DistanceTo(pb) > Core.Geometry.Tolerance.ShortCurve)
                            created = ctx.Doc.Create.NewDetailCurve(view, Line.CreateBound(pa, pb));
                        break;
                    default:
                        var line = Line.CreateBound(a, b);
                        var normal = line.Direction.CrossProduct(XYZ.BasisZ);
                        if (normal.GetLength() < 1e-6) normal = line.Direction.CrossProduct(XYZ.BasisX);
                        var plane = Plane.CreateByNormalAndOrigin(normal.CrossProduct(line.Direction).Normalize(), a);
                        created = ctx.Doc.Create.NewModelCurve(line, SketchPlane.Create(ctx.Doc, plane));
                        break;
                }
                if (created != null)
                {
                    ctx.Report.Created();
                    ctx.Report.AddResult(created.Id.Value);
                }
            }
        });
    }

    private static XYZ ProjectToView(View v, XYZ p)
    {
        var n = v.ViewDirection;
        var o = v.Origin;
        return p - n * (p - o).DotProduct(n);
    }
}
