using System.Globalization;

namespace TeraTools.Core.Geometry;

/// <summary>Vetor/ponto 3D imutável.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitX = new(1, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public double Dot(Vec3 o) => X * o.X + Y * o.Y + Z * o.Z;

    public Vec3 Cross(Vec3 o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);

    public double DistanceTo(Vec3 o) => (this - o).Length;

    public Vec3 Normalized()
    {
        var l = Length;
        return l < 1e-15 ? Zero : new Vec3(X / l, Y / l, Z / l);
    }

    public Vec2 XY => new(X, Y);

    public bool AlmostEquals(Vec3 o, double tol = Tolerance.Point) =>
        Math.Abs(X - o.X) <= tol && Math.Abs(Y - o.Y) <= tol && Math.Abs(Z - o.Z) <= tol;

    public bool IsParallelTo(Vec3 o, double tol = 1e-6) => Normalized().Cross(o.Normalized()).Length <= tol;

    public static Vec3 Lerp(Vec3 a, Vec3 b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    /// <summary>Rotação deste vetor em torno de um eixo unitário (fórmula de Rodrigues).</summary>
    public Vec3 RotateAround(Vec3 axis, double radians)
    {
        var k = axis.Normalized();
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return this * c + k.Cross(this) * s + k * (k.Dot(this) * (1 - c));
    }

    /// <summary>Rotação deste ponto em torno de uma reta (ponto + direção).</summary>
    public Vec3 RotateAround(Vec3 axisPoint, Vec3 axisDir, double radians) =>
        (this - axisPoint).RotateAround(axisDir, radians) + axisPoint;

    /// <summary>Um vetor unitário qualquer perpendicular a este.</summary>
    public Vec3 AnyPerpendicular()
    {
        var n = Normalized();
        var helper = Math.Abs(n.Z) < 0.9 ? UnitZ : UnitX;
        return helper.Cross(n).Normalized();
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X:0.####}, {Y:0.####}, {Z:0.####})");
}
