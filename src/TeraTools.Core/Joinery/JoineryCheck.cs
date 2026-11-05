using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

/// <summary>Duas peças que se invadem (fora dos encaixes previstos na fabricação).</summary>
public sealed record JoineryOverlap(string A, string B, string NameA, string NameB, double DX, double DY, double DZ)
{
    public string Describe() => Loc.F("\"{0}\" invade \"{1}\" ({2} × {3} × {4} mm)", "\"{0}\" overlaps \"{1}\" ({2} × {3} × {4} mm)",
        NameA, NameB, Math.Round(U.ToMm(DX), 1), Math.Round(U.ToMm(DY), 1), Math.Round(U.ToMm(DZ), 1));
}

/// <summary>
/// Verificação geométrica da marcenaria: peças (e puxadores) não podem ocupar o mesmo espaço.
/// Tolerância padrão de 0,5 mm nas três direções. Encaixes intencionais (fundo no canal das
/// laterais, fundo de gaveta no canal da caixa) não contam. O usuário decide o que fazer com o aviso.
/// </summary>
public static class JoineryCheck
{
    public static readonly double DefaultTolerance = U.Mm(0.5);

    private static readonly HashSet<PanelRole> Carcass = new()
    {
        PanelRole.Side, PanelRole.Bottom, PanelRole.Top, PanelRole.Rail, PanelRole.Divider, PanelRole.FixedShelf,
    };

    private static readonly HashSet<PanelRole> DrawerBox = new()
    {
        PanelRole.DrawerSide, PanelRole.DrawerBack, PanelRole.DrawerInnerFront,
    };

    /// <summary>Encaixes previstos: fundo em canal (8 mm) e fundo de gaveta em canal (6 mm).</summary>
    private static bool IsJoint(PanelRole a, PanelRole b, double dx, double dy, double dz)
    {
        static bool Pair(PanelRole a, PanelRole b, PanelRole x, HashSet<PanelRole> set) => (a == x && set.Contains(b)) || (b == x && set.Contains(a));
        var depth = new[] { dx, dy, dz }.Min();
        if (Pair(a, b, PanelRole.Back, Carcass) && depth <= U.Mm(12)) return true;
        if (Pair(a, b, PanelRole.DrawerBottom, DrawerBox) && depth <= U.Mm(10)) return true;
        return false;
    }

    public static List<JoineryOverlap> Overlaps(CabinetModel m, double? tolerance = null)
    {
        var tol = tolerance ?? DefaultTolerance;
        var items = m.Boards.Select(b => (b.Id, b.Name, b.Role, Box: b.Placement))
            .Concat(m.Accessories.Select(a => (a.Id, a.Name, Role: PanelRole.Filler, Box: a.Placement)))
            .ToList();
        var list = new List<JoineryOverlap>();
        for (var i = 0; i < items.Count; i++)
        {
            var (ia, na, ra, a) = items[i];
            var a0 = a.Frame.Origin;
            for (var j = i + 1; j < items.Count; j++)
            {
                var (ib, nb, rb, b) = items[j];
                var b0 = b.Frame.Origin;
                var dx = Math.Min(a0.X + a.SizeX, b0.X + b.SizeX) - Math.Max(a0.X, b0.X);
                if (dx <= tol) continue;
                var dy = Math.Min(a0.Y + a.SizeY, b0.Y + b.SizeY) - Math.Max(a0.Y, b0.Y);
                if (dy <= tol) continue;
                var dz = Math.Min(a0.Z + a.SizeZ, b0.Z + b.SizeZ) - Math.Max(a0.Z, b0.Z);
                if (dz <= tol) continue;
                if (IsJoint(ra, rb, dx, dy, dz)) continue;
                list.Add(new JoineryOverlap(ia, ib, na, nb, dx, dy, dz));
            }
        }
        return list;
    }
}
