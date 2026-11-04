using TeraTools.Core.Geometry;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Layout;

/// <summary>Distribuição em grade dentro de um ambiente (luminárias, módulos de forro, sprinklers...).</summary>
public sealed class GridLayoutSettings
{
    public double SpacingX { get; set; } = U.M(1.2);
    public double SpacingY { get; set; } = U.M(1.2);

    /// <summary>Distância mínima das paredes (quando não usa distribuição uniforme).</summary>
    public double MarginX { get; set; } = U.M(0.6);
    public double MarginY { get; set; } = U.M(0.6);

    /// <summary>Ajusta o espaçamento para distribuir uniformemente (margem = meio espaçamento).</summary>
    public bool FitEvenly { get; set; } = true;

    /// <summary>Alinha a grade com a direção principal do ambiente.</summary>
    public bool AutoOrient { get; set; } = true;

    public double RotationDeg { get; set; }

    /// <summary>Distância mínima de qualquer ponto à borda (evita pontos colados em reentrâncias).</summary>
    public double MinWallDistance { get; set; } = U.M(0.2);
}

public static class GridLayout
{
    public static List<Vec2> Generate(Region2 room, GridLayoutSettings s, out double angleRad)
    {
        angleRad = s.AutoOrient ? DominantDirection(room.Outer) : MathUtil.ToRadians(s.RotationDeg);
        if (s.AutoOrient) angleRad += MathUtil.ToRadians(s.RotationDeg);
        var c = room.Outer.Centroid;
        var a = angleRad;
        var local = room.Outer.Select(p => (p - c).Rotate(-a)).ToList();
        var b = Rect2.FromPoints(local);
        var xs = Axis(b.MinX, b.MaxX, s.SpacingX, s.MarginX, s.FitEvenly);
        var ys = Axis(b.MinY, b.MaxY, s.SpacingY, s.MarginY, s.FitEvenly);
        var pts = new List<Vec2>();
        foreach (var y in ys)
            foreach (var x in xs)
            {
                var p = new Vec2(x, y).Rotate(a) + c;
                if (room.Contains(p) && room.DistanceToBoundary(p) >= s.MinWallDistance - 1e-9)
                    pts.Add(p);
            }
        if (pts.Count == 0 && room.Contains(c)) pts.Add(c);
        return pts;
    }

    private static List<double> Axis(double min, double max, double spacing, double margin, bool even)
    {
        var len = max - min;
        var list = new List<double>();
        if (len <= 0) return list;
        if (even)
        {
            var n = Math.Max(1, (int)Math.Ceiling(len / Math.Max(1e-9, spacing) - 1e-9));
            var a = len / n;
            for (var k = 0; k < n; k++) list.Add(min + (k + 0.5) * a);
        }
        else
        {
            var avail = len - 2 * margin;
            if (avail < 0) { list.Add((min + max) / 2); return list; }
            var n = (int)Math.Floor(avail / Math.Max(1e-9, spacing) + 1e-9) + 1;
            var start = min + margin + (avail - (n - 1) * spacing) / 2;
            for (var k = 0; k < n; k++) list.Add(start + k * spacing);
        }
        return list;
    }

    /// <summary>Direção predominante das paredes (ângulo módulo 90°, ponderado pelo comprimento).</summary>
    public static double DominantDirection(Polygon2 p)
    {
        var bins = new double[90];
        foreach (var e in p.Edges())
        {
            var ang = MathUtil.ToDegrees(Math.Atan2(e.Vector.Y, e.Vector.X));
            var m = ((ang % 90) + 90) % 90;
            bins[(int)Math.Round(m) % 90] += e.Length;
        }
        var best = 0;
        for (var i = 1; i < 90; i++)
            if (bins[i] > bins[best]) best = i;
        return MathUtil.ToRadians(best);
    }
}

/// <summary>Método dos lúmens (estimativa de quantidade de luminárias).</summary>
public static class LumenMethod
{
    /// <summary>N = (E × A) / (Φ × FU × FM).</summary>
    public static int Count(double targetLux, double areaM2, double lumensPerFixture, double utilization = 0.6, double maintenance = 0.8)
    {
        if (lumensPerFixture <= 0 || utilization <= 0 || maintenance <= 0) return 0;
        return Math.Max(1, (int)Math.Ceiling(targetLux * areaM2 / (lumensPerFixture * utilization * maintenance) - 1e-9));
    }

    public static double Illuminance(int count, double areaM2, double lumensPerFixture, double utilization = 0.6, double maintenance = 0.8) =>
        areaM2 <= 0 ? 0 : count * lumensPerFixture * utilization * maintenance / areaM2;

    /// <summary>Índice do recinto K = (C × L) / (Hm × (C + L)).</summary>
    public static double RoomIndex(double lengthM, double widthM, double mountingHeightM) =>
        mountingHeightM <= 0 ? 0 : lengthM * widthM / (mountingHeightM * (lengthM + widthM));
}

/// <summary>Preset de layout de mobiliário: itens posicionados em relação a um ponto base.</summary>
public sealed class LayoutPreset
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<LayoutPresetItem> Items { get; set; } = new();
}

public sealed class LayoutPresetItem
{
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Category { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double RotationDeg { get; set; }
    public bool Mirrored { get; set; }
}
