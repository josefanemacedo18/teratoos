using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TeraTools.Core.Geometry;

namespace TeraTools.Revit.Ui;

/// <summary>Desenho 2D de pré-visualização (planta/elevação) mostrado no diálogo de opções.</summary>
public sealed class PreviewModel
{
    public List<PreviewShape> Shapes { get; } = new();
    public string? Info { get; set; }

    /// <summary>Cena 3D opcional (mostrada na aba 3D do diálogo).</summary>
    public Scene3? Scene { get; set; }

    /// <summary>Cena 3D, criada sob demanda.</summary>
    public Scene3 S3 => Scene ??= new Scene3();

    public bool Has3D => Scene != null && !Scene.IsEmpty;

    public PreviewModel Polygon(Polygon2 poly, string? fill = "#D9C2AE", string stroke = "#6B4A33", double width = 1)
    {
        if (!poly.IsEmpty) Shapes.Add(new PreviewShape { Points = poly.Points.ToList(), Closed = true, Fill = fill, Stroke = stroke, Width = width });
        return this;
    }

    public PreviewModel Outline(Polygon2 poly, string stroke = "#5F6B7A", double width = 1.5, bool dashed = false)
    {
        if (!poly.IsEmpty) Shapes.Add(new PreviewShape { Points = poly.Points.ToList(), Closed = true, Stroke = stroke, Width = width, Dashed = dashed });
        return this;
    }

    public PreviewModel Region(Region2 region, string stroke = "#5F6B7A")
    {
        Outline(region.Outer, stroke, 2);
        foreach (var h in region.Holes) Outline(h, stroke, 1.5, true);
        return this;
    }

    public PreviewModel Line(Vec2 a, Vec2 b, string stroke = "#3B7DD8", double width = 1)
    {
        Shapes.Add(new PreviewShape { Points = new List<Vec2> { a, b }, Closed = false, Stroke = stroke, Width = width });
        return this;
    }

    public PreviewModel Dot(Vec2 c, double radius, string fill = "#B5652B") =>
        Polygon(Polygon2.Circle(c, radius, 12), fill, fill, 0.5);
}

public sealed class PreviewShape
{
    public List<Vec2> Points { get; set; } = new();
    public bool Closed { get; set; } = true;
    public string? Fill { get; set; }
    public string Stroke { get; set; } = "#5F6B7A";
    public double Width { get; set; } = 1;
    public bool Dashed { get; set; }
}

/// <summary>Elemento WPF que desenha um <see cref="PreviewModel"/> ajustado à área disponível.</summary>
public sealed class PreviewCanvas : FrameworkElement
{
    private PreviewModel? _model;
    private readonly Dictionary<string, Brush> _brushes = new();

    public PreviewModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            InvalidateVisual();
        }
    }

    private Brush B(string hex)
    {
        if (!_brushes.TryGetValue(hex, out var b))
            _brushes[hex] = b = Theme.Brush(hex);
        return b;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRectangle(Theme.Get("TT.Surface"), null, new Rect(0, 0, w, h));
        if (_model == null || _model.Shapes.Count == 0)
        {
            var ft = new FormattedText(Core.Localization.Loc.T("Sem pré-visualização", "No preview"),
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Theme.Get("TT.Muted"), 1.0);
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in _model.Shapes)
            foreach (var p in s.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
        var bw = Math.Max(maxX - minX, 1e-9);
        var bh = Math.Max(maxY - minY, 1e-9);
        const double pad = 12;
        var scale = Math.Min((w - 2 * pad) / bw, (h - 2 * pad) / bh);
        if (scale <= 0 || double.IsInfinity(scale)) return;
        var ox = pad + (w - 2 * pad - bw * scale) / 2;
        var oy = pad + (h - 2 * pad - bh * scale) / 2;
        Point Map(Vec2 p) => new(ox + (p.X - minX) * scale, oy + (maxY - p.Y) * scale);

        foreach (var s in _model.Shapes)
        {
            if (s.Points.Count < 2) continue;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(Map(s.Points[0]), s.Fill != null && s.Closed, s.Closed);
                for (var i = 1; i < s.Points.Count; i++)
                    ctx.LineTo(Map(s.Points[i]), true, false);
            }
            geo.Freeze();
            var pen = new Pen(B(s.Stroke), s.Width);
            if (s.Dashed) pen.DashStyle = DashStyles.Dash;
            pen.Freeze();
            dc.DrawGeometry(s.Fill != null ? B(s.Fill) : null, pen, geo);
        }
    }
}
