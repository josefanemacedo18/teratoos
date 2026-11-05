using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Estilo pronto de cobertura. <see cref="SupportsU"/> × <see cref="SupportsV"/> = grade automática de apoios.</summary>
public sealed record CanopyStyle(string Id, string Name, string Description, Func<CanopySettings> Create, int SupportsU, int SupportsV);

public static class CanopyStyles
{
    public static IReadOnlyList<CanopyStyle> All { get; } = new List<CanopyStyle>
    {
        new("arvores", Loc.T("Almofada sobre árvores", "Pillow on trees"), Loc.T("Superfície que sobe do contorno, grelha e apoios em árvore com dois níveis de galhos.", "Surface rising from the outline, grid and two-level tree supports."),
            () => new CanopySettings { Surface = CanopySurface.Pillow, Height = U.M(3.6), Rise = U.M(0.9), Structure = CanopyStructure.Grid, SupportKind = CanopySupports.Tree, Branches = 4, BranchLevels = 2 }, 2, 1),

        new("funil", Loc.T("Guarda-chuvas invertidos (funil)", "Inverted umbrellas (funnel)"), Loc.T("Cada pilar recolhe a água no centro do seu funil; nervuras radiais.", "Each column collects water at the center of its funnel; radial ribs."),
            () => new CanopySettings { Surface = CanopySurface.Umbrellas, UmbrellaInverted = true, Sharpness = 1, Height = U.M(3.4), Rise = U.M(1.2), Structure = CanopyStructure.Radial, RadialCount = 8, RadialRings = 2, SupportKind = CanopySupports.Straight, TrunkDiameter = U.Mm(350) }, 2, 2),

        new("guarda", Loc.T("Guarda-chuvas", "Umbrellas"), Loc.T("Pontos altos nos pilares, painéis e nervuras radiais.", "High points at the columns, panels and radial ribs."),
            () => new CanopySettings { Surface = CanopySurface.Umbrellas, UmbrellaInverted = false, Sharpness = 1.4, Height = U.M(3.2), Rise = U.M(1.0), Skin = CanopySkin.Panels, PanelSizeU = U.Mm(1200), PanelSizeV = U.Mm(1200), Structure = CanopyStructure.Radial, RadialCount = 10, RadialRings = 2, SupportKind = CanopySupports.Tree, BranchLevels = 1, Branches = 6, BranchReach = U.M(1.6) }, 2, 1),

        new("membrana", Loc.T("Membrana tensionada", "Tensile membrane"), Loc.T("Picos nos mastros e bordas ancoradas, com cabo de borda.", "Peaks at the masts and anchored edges, with an edge cable."),
            () => new CanopySettings { Surface = CanopySurface.Membrane, Height = U.M(3.0), Rise = U.M(2.0), Sharpness = 1.8, InfluenceRadius = U.M(4.5), SkinThickness = U.Mm(10), Structure = CanopyStructure.None, EdgeBeam = true, Profile = MemberProfile.Tube, MemberHeight = U.Mm(40), SupportKind = CanopySupports.Straight, TrunkDiameter = U.Mm(160) }, 2, 1),

        new("abobada", Loc.T("Abóbada envidraçada em grelha", "Glazed grid vault"), Loc.T("Arco atravessado ao eixo, painéis de vidro e grelha.", "Arch across the axis, glass panels and grid."),
            () => new CanopySettings { Surface = CanopySurface.Vault, Height = U.M(3.2), Rise = U.M(1.6), Skin = CanopySkin.Panels, SkinThickness = U.Mm(24), PanelSizeU = U.Mm(1500), PanelSizeV = U.Mm(1000), PanelJoint = U.Mm(15), Structure = CanopyStructure.Grid, StructureSpacingU = U.Mm(1500), StructureSpacingV = U.Mm(1000), MemberWidth = U.Mm(60), MemberHeight = U.Mm(160), SupportKind = CanopySupports.None }, 0, 0),

        new("sela", Loc.T("Sela (paraboloide hiperbólico)", "Saddle (hyperbolic paraboloid)"), Loc.T("Dois cantos altos e dois baixos, diagrid e viga de borda.", "Two high and two low corners, diagrid and edge beam."),
            () => new CanopySettings { Surface = CanopySurface.Saddle, Height = U.M(3.0), Rise = U.M(2.0), Structure = CanopyStructure.Diagrid, StructureSpacingU = U.M(1.2), StructureSpacingV = U.M(1.2), SupportKind = CanopySupports.None }, 0, 0),

        new("ondas", Loc.T("Ondas com lâminas", "Waves with slats"), Loc.T("Lâminas que ondulam em duas direções, sobre árvores.", "Slats waving in two directions, on trees."),
            () => new CanopySettings { Surface = CanopySurface.Waves, WavesU = 1.5, WavesV = 1, Height = U.M(3.4), Rise = U.M(0.8), Skin = CanopySkin.Slats, SlatSpacing = U.Mm(250), SlatDepth = U.Mm(180), SlatThickness = U.Mm(40), Structure = CanopyStructure.Grid, StructureSpacingU = U.M(2), StructureSpacingV = U.M(2), SupportKind = CanopySupports.Tree, BranchLevels = 2 }, 3, 1),

        new("inclinada", Loc.T("Plana inclinada com painéis", "Sloped flat with panels"), Loc.T("Plano com 5% de caimento, painéis e pilares retos.", "Plane with a 5% slope, panels and straight columns."),
            () => new CanopySettings { Surface = CanopySurface.Flat, SlopePercent = 5, Height = U.M(3.0), Skin = CanopySkin.Panels, PanelSizeU = U.Mm(2000), PanelSizeV = U.Mm(1000), Structure = CanopyStructure.Grid, StructureSpacingU = U.M(2), StructureSpacingV = U.M(1), SupportKind = CanopySupports.Straight, TrunkDiameter = U.Mm(200) }, 3, 2),

        new("bolhas", Loc.T("Bolhas (atratores)", "Bubbles (attractors)"), Loc.T("Bolhas sobre cada apoio, diagrid e árvores.", "Bubbles over each support, diagrid and trees."),
            () => new CanopySettings { Surface = CanopySurface.Attractors, Height = U.M(3.3), Rise = U.M(1.1), InfluenceRadius = U.M(2.2), Structure = CanopyStructure.Diagrid, StructureSpacingU = U.M(1.2), StructureSpacingV = U.M(1.2), SupportKind = CanopySupports.Tree, BranchLevels = 1, Branches = 5 }, 2, 2),
    };

    public static CanopyStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Grade de apoios (nu × nv) distribuída dentro da planta, na direção dada.</summary>
    public static List<Vec2> SupportGrid(Polygon2 footprint, int nu, int nv, double rotationDeg = 0)
    {
        var list = new List<Vec2>();
        if (nu <= 0 || nv <= 0) return list;
        var poly = footprint.EnsureCounterClockwise();
        var c = poly.Centroid;
        var ax = Vec2.FromAngle(rotationDeg * Math.PI / 180);
        var ay = ax.Perp;
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var p in poly)
        {
            var d = p - c;
            u0 = Math.Min(u0, d.Dot(ax)); u1 = Math.Max(u1, d.Dot(ax));
            v0 = Math.Min(v0, d.Dot(ay)); v1 = Math.Max(v1, d.Dot(ay));
        }
        for (var i = 0; i < nu; i++)
            for (var j = 0; j < nv; j++)
            {
                var u = u0 + (u1 - u0) * (i + 0.5) / nu;
                var v = v0 + (v1 - v0) * (j + 0.5) / nv;
                var p = c + ax * u + ay * v;
                if (poly.Contains(p)) list.Add(p);
            }
        return list;
    }
}
