using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Estilo pronto de coluna: um ponto de partida que o usuário ajusta depois.</summary>
public sealed record ColumnStyle(string Id, string Name, string Description, Func<ColumnSettings> Create);

/// <summary>Catálogo de estilos prontos de colunas orgânicas.</summary>
public static class ColumnStyles
{
    public static IReadOnlyList<ColumnStyle> All { get; } = new List<ColumnStyle>
    {
        new("cilindrica", Loc.T("Cilíndrica", "Cylindrical"), Loc.T("Coluna reta, seção circular, base e capitel em disco.", "Straight round column, disc base and capital."),
            () => new ColumnSettings { BaseRadius = U.Mm(175), TopRadius = U.Mm(175), Base = ColumnEnd.Disc, BaseSize = U.Mm(450), BaseThickness = U.Mm(40), Capital = ColumnEnd.Disc, CapitalSize = U.Mm(450), CapitalThickness = U.Mm(40) }),

        new("leque", Loc.T("Leque de nervuras", "Rib fan"), Loc.T("Nervuras que abrem em leque até o teto, sobre um núcleo fino.", "Ribs fanning out to the ceiling over a slender core."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Trumpet, BaseRadius = U.Mm(120), TopRadius = U.Mm(900), FlarePower = 2.5,
                Build = ColumnBuild.Ribs, MemberCount = 10, MemberSize = U.Mm(50), RingCount = 3, CoreRadius = U.Mm(70),
                Capital = ColumnEnd.Disc, CapitalSize = U.Mm(1900), CapitalThickness = U.Mm(40),
            }),

        new("trelica", Loc.T("Treliça cônica", "Conical lattice"), Loc.T("Diagrid em hélices cruzadas, mais larga no topo.", "Crossed-helix diagrid, wider at the top."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Cone, BaseRadius = U.Mm(180), TopRadius = U.Mm(450),
                Build = ColumnBuild.Lattice, MemberCount = 8, MemberSize = U.Mm(45), LatticeTurns = 0.5, RingCount = 4,
                Base = ColumnEnd.Disc, BaseSize = U.Mm(500), BaseThickness = U.Mm(30), Capital = ColumnEnd.Disc, CapitalSize = U.Mm(1000), CapitalThickness = U.Mm(30),
            }),

        new("cogumelo", Loc.T("Cogumelo vazado", "Open mushroom"), Loc.T("Grelha de nervuras e anéis que se abre como um cogumelo.", "Grid of ribs and rings opening like a mushroom."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Mushroom, BaseRadius = U.Mm(220), TopRadius = U.Mm(1200), MushroomStart = 0.65,
                Build = ColumnBuild.Grid, MemberCount = 16, MemberSize = U.Mm(40), RingCount = 7, CoreRadius = U.Mm(90),
            }),

        new("trompete", Loc.T("Trompete com capitel prato", "Trumpet with plate capital"), Loc.T("Fuste maciço que abre no alto e termina em um prato.", "Solid shaft flaring at the top into a plate."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Trumpet, BaseRadius = U.Mm(160), TopRadius = U.Mm(500), FlarePower = 4,
                Capital = ColumnEnd.Plate, CapitalSize = U.Mm(1400), CapitalThickness = U.Mm(120),
            }),

        new("tulipa", Loc.T("Pétalas (tulipa)", "Petals (tulip)"), Loc.T("Tronco maciço que se divide em pétalas afinadas.", "Solid trunk splitting into tapered petals."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Trumpet, BaseRadius = U.Mm(170), TopRadius = U.Mm(800), FlarePower = 2.2,
                Build = ColumnBuild.Petals, MemberCount = 6, ShellThickness = U.Mm(50), PetalStart = 0.5, PetalGap = 0.1, PetalTaper = 0.55,
            }),

        new("canelada", Loc.T("Fuste canelado ondulado", "Fluted wavy shaft"), Loc.T("Caneluras com ondulação em espiral ao longo da altura.", "Flutes with a spiral ripple along the height."),
            () => new ColumnSettings
            {
                BaseRadius = U.Mm(220), TopRadius = U.Mm(200), Flutes = 16, FluteDepth = 0.1, WaveAmplitude = 0.06, WaveCount = 4, WaveLobes = 3,
                Base = ColumnEnd.Square, BaseSize = U.Mm(560), BaseThickness = U.Mm(60), Capital = ColumnEnd.Square, CapitalSize = U.Mm(560), CapitalThickness = U.Mm(60),
            }),

        new("lamelas", Loc.T("Lamelas radiais", "Radial fins"), Loc.T("Lâminas em volta de um núcleo, com leve barriga.", "Fins around a core, with a slight belly."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Belly, BaseRadius = U.Mm(220), TopRadius = U.Mm(220), MidRadius = U.Mm(300),
                Build = ColumnBuild.Lamellas, MemberCount = 16, MemberSize = U.Mm(20), CoreRadius = U.Mm(100), TwistDeg = 30,
                Base = ColumnEnd.Disc, BaseSize = U.Mm(640), BaseThickness = U.Mm(30), Capital = ColumnEnd.Disc, CapitalSize = U.Mm(640), CapitalThickness = U.Mm(30),
            }),

        new("torcida", Loc.T("Torcida lobada", "Twisted lobed"), Loc.T("Seção de cinco lóbulos com torção de 180°.", "Five-lobed section twisted 180°."),
            () => new ColumnSettings
            {
                Section = ColumnSection.Lobed, Lobes = 5, LobeDepth = 0.35, BaseRadius = U.Mm(240), TopRadius = U.Mm(240), TwistDeg = 180,
            }),

        new("ampulheta", Loc.T("Ampulheta", "Hourglass"), Loc.T("Afina no meio e abre nas pontas.", "Narrow at the middle, wide at both ends."),
            () => new ColumnSettings
            {
                Silhouette = ColumnSilhouette.Hourglass, BaseRadius = U.Mm(300), TopRadius = U.Mm(300), MidRadius = U.Mm(150),
            }),

        new("inclinada", Loc.T("Inclinada em S", "S-curved"), Loc.T("Eixo em S com seção elíptica, para pilotis expressivos.", "S-shaped spine with an elliptic section for expressive pilotis."),
            () => new ColumnSettings
            {
                Spine = ColumnSpine.SCurve, BendOffset = U.Mm(400), Section = ColumnSection.Ellipse, EllipseRatio = 0.6,
                BaseRadius = U.Mm(260), TopRadius = U.Mm(220),
            }),

        new("estrela", Loc.T("Estrela facetada", "Faceted star"), Loc.T("Seção estrela de seis pontas, torcida e afinando para cima.", "Six-point star section, twisted and tapering upward."),
            () => new ColumnSettings
            {
                Section = ColumnSection.Star, Sides = 6, StarInner = 0.6, Silhouette = ColumnSilhouette.Cone, BaseRadius = U.Mm(280), TopRadius = U.Mm(200), TwistDeg = 60, Resolution = 60,
            }),
    };

    public static ColumnStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);
}
