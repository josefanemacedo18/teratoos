using TeraTools.Core.Geometry;

namespace TeraTools.Core.Documentation;

public sealed class ViewBox
{
    public string Key { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class PackedView
{
    public ViewBox View { get; set; } = new();
    public int Sheet { get; set; }
    public Vec2 Center { get; set; }
}

/// <summary>
/// Distribui vistas (retângulos em escala de papel) na área útil de pranchas, em prateleiras
/// da esquerda para a direita e de cima para baixo. Usa quantas pranchas forem necessárias.
/// </summary>
public static class SheetPacker
{
    public static List<PackedView> Pack(IEnumerable<ViewBox> views, Rect2 area, double gap, bool sortBySize = true)
    {
        var list = sortBySize ? views.OrderByDescending(v => v.Height).ThenByDescending(v => v.Width).ToList() : views.ToList();
        var result = new List<PackedView>();
        var sheet = 0;
        var x = area.MinX;
        var yTop = area.MaxY;
        var rowH = 0.0;
        foreach (var v in list)
        {
            var w = Math.Min(v.Width, area.Width);
            var h = Math.Min(v.Height, area.Height);
            if (x + w > area.MaxX + 1e-9)
            {
                x = area.MinX;
                yTop -= rowH + gap;
                rowH = 0;
            }
            if (yTop - h < area.MinY - 1e-9)
            {
                sheet++;
                x = area.MinX;
                yTop = area.MaxY;
                rowH = 0;
            }
            result.Add(new PackedView { View = v, Sheet = sheet, Center = new Vec2(x + w / 2, yTop - h / 2) });
            x += w + gap;
            rowH = Math.Max(rowH, h);
        }
        return result;
    }
}
