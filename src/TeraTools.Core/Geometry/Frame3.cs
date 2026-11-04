namespace TeraTools.Core.Geometry;

/// <summary>
/// Sistema de coordenadas local (origem + eixos ortonormais). Usado para posicionar
/// módulos, chapas, aletas etc. em qualquer orientação.
/// </summary>
public readonly record struct Frame3(Vec3 Origin, Vec3 XAxis, Vec3 YAxis, Vec3 ZAxis)
{
    public static readonly Frame3 World = new(Vec3.Zero, Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);

    /// <summary>Cria um sistema a partir da origem, da direção X e de uma normal aproximada (Z).</summary>
    public static Frame3 FromXAndNormal(Vec3 origin, Vec3 xDir, Vec3 normal)
    {
        var z = normal.Normalized();
        var x = (xDir - z * xDir.Dot(z)).Normalized();
        if (x.LengthSquared < 1e-12)
            x = z.AnyPerpendicular();
        var y = z.Cross(x).Normalized();
        return new Frame3(origin, x, y, z);
    }

    /// <summary>Sistema horizontal com rotação em torno de Z (ângulo em radianos).</summary>
    public static Frame3 Horizontal(Vec3 origin, double rotationRadians)
    {
        var x = new Vec3(Math.Cos(rotationRadians), Math.Sin(rotationRadians), 0);
        var y = new Vec3(-x.Y, x.X, 0);
        return new Frame3(origin, x, y, Vec3.UnitZ);
    }

    public Vec3 ToWorld(Vec3 local) => Origin + XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

    public Vec3 ToWorld(double x, double y, double z) => Origin + XAxis * x + YAxis * y + ZAxis * z;

    public Vec3 DirectionToWorld(Vec3 local) => XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

    public Vec3 ToLocal(Vec3 world)
    {
        var d = world - Origin;
        return new Vec3(d.Dot(XAxis), d.Dot(YAxis), d.Dot(ZAxis));
    }

    public Frame3 Translated(Vec3 offset) => this with { Origin = Origin + offset };

    /// <summary>Gira o sistema inteiro em torno de uma reta no espaço.</summary>
    public Frame3 RotatedAround(Vec3 axisPoint, Vec3 axisDir, double radians) => new(
        Origin.RotateAround(axisPoint, axisDir, radians),
        XAxis.RotateAround(axisDir, radians),
        YAxis.RotateAround(axisDir, radians),
        ZAxis.RotateAround(axisDir, radians));

    /// <summary>Compõe: interpreta <paramref name="child"/> como local a este sistema.</summary>
    public Frame3 Compose(Frame3 child) => new(
        ToWorld(child.Origin),
        DirectionToWorld(child.XAxis),
        DirectionToWorld(child.YAxis),
        DirectionToWorld(child.ZAxis));

    /// <summary>Ângulo de rotação em torno de Z do eixo X (para elementos horizontais).</summary>
    public double PlanRotation => Math.Atan2(XAxis.Y, XAxis.X);
}

/// <summary>Caixa orientada: sistema local + dimensões ao longo de X, Y e Z locais (origem no canto mínimo).</summary>
public readonly record struct OrientedBox(Frame3 Frame, double SizeX, double SizeY, double SizeZ)
{
    public Vec3 Center => Frame.ToWorld(SizeX / 2, SizeY / 2, SizeZ / 2);

    /// <summary>
    /// Cria a caixa a partir do centro e de dois eixos; o eixo Z é X × Y (sistema destro).
    /// </summary>
    public static OrientedBox FromCenter(Vec3 center, Vec3 xAxis, Vec3 yAxis, double sizeX, double sizeY, double sizeZ)
    {
        var x = xAxis.Normalized();
        var y = (yAxis - x * yAxis.Dot(x)).Normalized();
        var z = x.Cross(y).Normalized();
        var origin = center - x * (sizeX / 2) - y * (sizeY / 2) - z * (sizeZ / 2);
        return new OrientedBox(new Frame3(origin, x, y, z), sizeX, sizeY, sizeZ);
    }

    /// <summary>
    /// Caixa "em pé" (Z = vertical) a partir do centro e de duas direções horizontais perpendiculares.
    /// Troca a ordem dos eixos se necessário para manter Z para cima.
    /// </summary>
    public static OrientedBox UprightFromCenter(Vec3 center, Vec3 dirA, double sizeA, Vec3 dirB, double sizeB, double height)
    {
        var a = new Vec3(dirA.X, dirA.Y, 0).Normalized();
        var b = new Vec3(dirB.X, dirB.Y, 0).Normalized();
        return a.Cross(b).Z > 0
            ? FromCenter(center, a, b, sizeA, sizeB, height)
            : FromCenter(center, b, a, sizeB, sizeA, height);
    }

    /// <summary>Verdadeiro se o eixo Z local é vertical (pode ser colocado por nível + rotação em planta).</summary>
    public bool IsUpright => Frame.ZAxis.Z > 1 - 1e-9;

    public double Volume => SizeX * SizeY * SizeZ;

    public IEnumerable<Vec3> Corners()
    {
        for (var i = 0; i < 8; i++)
            yield return Frame.ToWorld((i & 1) == 0 ? 0 : SizeX, (i & 2) == 0 ? 0 : SizeY, (i & 4) == 0 ? 0 : SizeZ);
    }
}
