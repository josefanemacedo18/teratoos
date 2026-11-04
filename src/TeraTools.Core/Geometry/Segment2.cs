namespace TeraTools.Core.Geometry;

/// <summary>Segmento de reta 2D.</summary>
public readonly record struct Segment2(Vec2 A, Vec2 B)
{
    public double Length => A.DistanceTo(B);
    public Vec2 Vector => B - A;
    public Vec2 Direction => (B - A).Normalized();
    public Vec2 Midpoint => (A + B) * 0.5;

    /// <summary>Normal à direita (para polígonos anti-horários, aponta para fora).</summary>
    public Vec2 RightNormal
    {
        get
        {
            var d = Direction;
            return new Vec2(d.Y, -d.X);
        }
    }

    public Vec2 PointAt(double t) => Vec2.Lerp(A, B, t);

    /// <summary>Parâmetro (0..1, não limitado) da projeção do ponto na reta do segmento.</summary>
    public double ProjectParameter(Vec2 p)
    {
        var v = B - A;
        var l2 = v.LengthSquared;
        return l2 < 1e-20 ? 0 : (p - A).Dot(v) / l2;
    }

    public Vec2 ClosestPoint(Vec2 p)
    {
        var t = Math.Clamp(ProjectParameter(p), 0, 1);
        return PointAt(t);
    }

    public double DistanceTo(Vec2 p) => ClosestPoint(p).DistanceTo(p);

    public Segment2 Reversed() => new(B, A);

    public Segment2 Offset(double d)
    {
        var n = RightNormal * d;
        return new Segment2(A + n, B + n);
    }

    /// <summary>Interseção de segmentos. Retorna parâmetros t (neste) e u (no outro).</summary>
    public bool Intersect(Segment2 other, out Vec2 point, out double t, out double u, double tol = 1e-12)
    {
        point = default;
        t = u = 0;
        var r = B - A;
        var s = other.B - other.A;
        var denom = r.Cross(s);
        if (Math.Abs(denom) < tol)
            return false;
        var qp = other.A - A;
        t = qp.Cross(s) / denom;
        u = qp.Cross(r) / denom;
        const double eps = 1e-9;
        if (t < -eps || t > 1 + eps || u < -eps || u > 1 + eps)
            return false;
        point = PointAt(t);
        return true;
    }

    /// <summary>Interseção das retas infinitas que contêm os segmentos.</summary>
    public static bool LineIntersection(Vec2 p1, Vec2 d1, Vec2 p2, Vec2 d2, out Vec2 point)
    {
        var denom = d1.Cross(d2);
        if (Math.Abs(denom) < 1e-12)
        {
            point = p2;
            return false;
        }
        var t = (p2 - p1).Cross(d2) / denom;
        point = p1 + d1 * t;
        return true;
    }
}
