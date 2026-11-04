using System.Globalization;

namespace TeraTools.Core.Geometry;

/// <summary>Vetor/ponto 2D imutável (unidades livres; no add-in usamos pés, unidade interna do Revit).</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);
    public static readonly Vec2 UnitX = new(1, 0);
    public static readonly Vec2 UnitY = new(0, 1);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    /// <summary>Ângulo em radianos em relação ao eixo X (-π..π).</summary>
    public double Angle => Math.Atan2(Y, X);

    /// <summary>Perpendicular (rotação de +90°).</summary>
    public Vec2 Perp => new(-Y, X);

    public double Dot(Vec2 o) => X * o.X + Y * o.Y;

    /// <summary>Componente Z do produto vetorial (positivo = o está à esquerda).</summary>
    public double Cross(Vec2 o) => X * o.Y - Y * o.X;

    public double DistanceTo(Vec2 o) => (this - o).Length;

    public Vec2 Normalized()
    {
        var l = Length;
        return l < 1e-15 ? Zero : new Vec2(X / l, Y / l);
    }

    public Vec2 Rotate(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new Vec2(X * c - Y * s, X * s + Y * c);
    }

    public Vec2 RotateAround(Vec2 center, double radians) => (this - center).Rotate(radians) + center;

    public bool AlmostEquals(Vec2 o, double tol = Tolerance.Point) =>
        Math.Abs(X - o.X) <= tol && Math.Abs(Y - o.Y) <= tol;

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public static Vec2 FromAngle(double radians, double length = 1.0) =>
        new(Math.Cos(radians) * length, Math.Sin(radians) * length);

    public Vec3 ToVec3(double z = 0) => new(X, Y, z);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X:0.####}, {Y:0.####})");
}

/// <summary>Tolerâncias geométricas padrão (em pés, compatíveis com o Revit).</summary>
public static class Tolerance
{
    /// <summary>Tolerância para coincidência de pontos (~0,03 mm).</summary>
    public const double Point = 1e-4;

    /// <summary>Menor comprimento de curva aceito pelo Revit (~0,8 mm).</summary>
    public const double ShortCurve = 0.00256;

    /// <summary>Tolerância angular (radianos).</summary>
    public const double Angle = 1e-6;

    /// <summary>Tolerância de área (pés²).</summary>
    public const double Area = 1e-7;
}
