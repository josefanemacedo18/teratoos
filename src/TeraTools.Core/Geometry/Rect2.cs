namespace TeraTools.Core.Geometry;

/// <summary>Retângulo alinhado aos eixos.</summary>
public readonly record struct Rect2(double MinX, double MinY, double MaxX, double MaxY)
{
    public static readonly Rect2 Empty = new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    public static Rect2 FromSize(double x, double y, double width, double height) => new(x, y, x + width, y + height);

    public static Rect2 FromPoints(IEnumerable<Vec2> pts)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var p in pts)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new Rect2(minX, minY, maxX, maxY);
    }

    public bool IsEmpty => MaxX < MinX || MaxY < MinY;
    public double Width => IsEmpty ? 0 : MaxX - MinX;
    public double Height => IsEmpty ? 0 : MaxY - MinY;
    public double Area => Width * Height;
    public Vec2 Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);
    public Vec2 Min => new(MinX, MinY);
    public Vec2 Max => new(MaxX, MaxY);

    public bool Contains(Vec2 p, double tol = 0) =>
        p.X >= MinX - tol && p.X <= MaxX + tol && p.Y >= MinY - tol && p.Y <= MaxY + tol;

    public bool Contains(Rect2 r, double tol = 1e-9) =>
        r.MinX >= MinX - tol && r.MaxX <= MaxX + tol && r.MinY >= MinY - tol && r.MaxY <= MaxY + tol;

    public bool Intersects(Rect2 r, double tol = 0) =>
        r.MinX < MaxX - tol && r.MaxX > MinX + tol && r.MinY < MaxY - tol && r.MaxY > MinY + tol;

    public Rect2 Union(Rect2 r) => IsEmpty ? r : r.IsEmpty ? this :
        new(Math.Min(MinX, r.MinX), Math.Min(MinY, r.MinY), Math.Max(MaxX, r.MaxX), Math.Max(MaxY, r.MaxY));

    public Rect2 Intersection(Rect2 r)
    {
        var x0 = Math.Max(MinX, r.MinX);
        var y0 = Math.Max(MinY, r.MinY);
        var x1 = Math.Min(MaxX, r.MaxX);
        var y1 = Math.Min(MaxY, r.MaxY);
        return x1 <= x0 || y1 <= y0 ? Empty : new Rect2(x0, y0, x1, y1);
    }

    public Rect2 Inflate(double d) => new(MinX - d, MinY - d, MaxX + d, MaxY + d);

    public Rect2 Translate(Vec2 v) => new(MinX + v.X, MinY + v.Y, MaxX + v.X, MaxY + v.Y);

    public Polygon2 ToPolygon() => new(new[]
    {
        new Vec2(MinX, MinY), new Vec2(MaxX, MinY), new Vec2(MaxX, MaxY), new Vec2(MinX, MaxY)
    });
}
