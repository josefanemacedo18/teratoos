using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

/// <summary>Em que borda da frente fica o puxador.</summary>
public enum HandleEdge
{
    /// <summary>Vertical, perto da borda direita (porta com dobradiça à esquerda).</summary>
    Right,
    /// <summary>Vertical, perto da borda esquerda (porta com dobradiça à direita).</summary>
    Left,
    /// <summary>Horizontal, perto do topo (gavetas, escamoteável).</summary>
    Top,
    /// <summary>Horizontal, perto da base (basculante).</summary>
    Bottom,
    /// <summary>Vertical, no centro (folha do meio de porta de correr).</summary>
    Center,
}

/// <summary>
/// Geometria dos puxadores (acessórios modelados, fora do plano de corte). A frente fica no plano XZ do
/// móvel; a face externa é a de menor Y. Barra 12 × 12 mm a 32 mm da frente, com dois apoios; botão
/// 28 mm; perfil no topo da frente. Cava e fecho toque não têm peça aparente.
/// </summary>
public static class HandleGeometry
{
    private static readonly double Bar = U.Mm(12);
    private static readonly double Standoff = U.Mm(32);
    private static readonly double Post = U.Mm(10);
    private static readonly double Inset = U.Mm(40);
    private static readonly double Knob = U.Mm(28);

    public static void Add(CabinetModel m, Board front, HandleKind kind, double length, string material, HandleEdge edge, double elevation)
    {
        if (kind is HandleKind.None or HandleKind.Cava or HandleKind.PushOpen) return;
        var p = front.Placement;
        var o = p.Frame.Origin;
        double x0 = o.X, x1 = o.X + p.SizeX, z0 = o.Z, z1 = o.Z + p.SizeZ, face = o.Y;
        var w = x1 - x0;
        var h = z1 - z0;
        var n = 0;
        void Box(double x, double y, double z, double sx, double sy, double sz)
        {
            if (sx <= 1e-6 || sy <= 1e-6 || sz <= 1e-6) return;
            m.Accessories.Add(new Accessory
            {
                Id = $"{front.Id}-PX{++n}",
                Name = Loc.F("Puxador — {0}", "Handle — {0}", front.Name),
                Kind = HardwareKind.Handle,
                Material = material,
                Placement = new OrientedBox(new Frame3(new Vec3(x, y, z), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), sx, sy, sz),
            });
        }

        if (kind == HandleKind.Profile)
        {
            // perfil de alumínio na borda superior, por toda a largura
            Box(x0, face - U.Mm(20), z1 - U.Mm(25), w, U.Mm(20), U.Mm(25));
            return;
        }
        var vertical = edge is HandleEdge.Left or HandleEdge.Right or HandleEdge.Center;
        var room = (vertical ? h : w) - 2 * Inset;
        var len = kind == HandleKind.Knob ? 0 : Math.Min(length, room);
        var isKnob = kind == HandleKind.Knob || len < U.Mm(60);

        double xc, zc;
        if (vertical)
        {
            xc = edge switch { HandleEdge.Right => x1 - Inset, HandleEdge.Left => x0 + Inset, _ => (x0 + x1) / 2 };
            var half = isKnob ? 0 : len / 2;
            var lo = z0 + Inset + half;
            var hi = z1 - Inset - half;
            if (hi < lo) hi = lo = (z0 + z1) / 2;
            var absBottom = elevation + z0;
            var absTop = elevation + z1;
            // aéreo: perto da base; baixo: perto do topo; alto: por volta de 1,05 m do piso
            zc = absBottom >= U.M(1.2) ? lo : absTop <= U.M(1.1) ? hi : Math.Clamp(U.M(1.05) - elevation, lo, hi);
        }
        else
        {
            xc = (x0 + x1) / 2;
            zc = edge == HandleEdge.Top ? z1 - Inset : z0 + Inset;
        }

        if (isKnob)
        {
            Box(xc - Knob / 2, face - U.Mm(25), zc - Knob / 2, Knob, U.Mm(25), Knob);
            return;
        }
        if (vertical)
        {
            Box(xc - Bar / 2, face - Standoff - Bar, zc - len / 2, Bar, Bar, len);
            Box(xc - Post / 2, face - Standoff, zc - len / 2 + U.Mm(8), Post, Standoff, Post);
            Box(xc - Post / 2, face - Standoff, zc + len / 2 - U.Mm(8) - Post, Post, Standoff, Post);
        }
        else
        {
            Box(xc - len / 2, face - Standoff - Bar, zc - Bar / 2, len, Bar, Bar);
            Box(xc - len / 2 + U.Mm(8), face - Standoff, zc - Post / 2, Post, Standoff, Post);
            Box(xc + len / 2 - U.Mm(8) - Post, face - Standoff, zc - Post / 2, Post, Standoff, Post);
        }
    }
}
