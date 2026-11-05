using TeraTools.Core.Facade;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Organic;

/// <summary>Estilo pronto de fachada de lâminas (a altura vem da parede ou do que o usuário indicar).</summary>
public sealed record FinStyle(string Id, string Name, string Description, Action<FinFacadeSettings> Apply);

public static class FinStyles
{
    public static IReadOnlyList<FinStyle> All { get; } = new List<FinStyle>
    {
        new("cobre", Loc.T("Cobre em onda com arco de entrada", "Copper wave with entrance arch"),
            Loc.T("Lâminas que giram em onda ao longo da fachada, abertas em arco sobre a entrada e cortadas por duas faixas.", "Fins rotating in a wave, opened as an arch over the entrance and crossed by two bands."),
            s =>
            {
                s.Profile = FinProfile.Plate; s.Depth = U.Mm(400); s.Thickness = U.Mm(20); s.Spacing = U.Mm(350);
                s.Variation = VariationMode.Sine; s.AmplitudeDeg = 60; s.Periods = 1.5; s.TwistDeg = 0;
                s.BottomTrim = FinTrim.Arch; s.ArchCenter = 0.5; s.ArchWidth = U.M(5); s.ArchSpring = U.M(2.4); s.ArchCrown = U.M(4.2);
                s.BandCount = 2; s.BandDepth = U.Mm(700); s.BandThickness = U.Mm(220); s.BandOffset = 0;
            }),
        new("torcidas", Loc.T("Aletas torcidas", "Twisted fins"), Loc.T("Todas as lâminas torcem 90° da base ao topo.", "All fins twist 90° from bottom to top."),
            s =>
            {
                s.Profile = FinProfile.Plate; s.Depth = U.Mm(350); s.Thickness = U.Mm(30); s.Spacing = U.Mm(450);
                s.Variation = VariationMode.None; s.TwistDeg = 90; s.TwistFollowsVariation = false;
            }),
        new("onda-torcao", Loc.T("Torção em onda", "Wave of twist"), Loc.T("A torção cresce e diminui ao longo da fachada, como um tecido.", "Twist grows and shrinks along the facade, like fabric."),
            s =>
            {
                s.Profile = FinProfile.Plate; s.Depth = U.Mm(400); s.Thickness = U.Mm(25); s.Spacing = U.Mm(400);
                s.Variation = VariationMode.Sine; s.AmplitudeDeg = 0; s.Periods = 1; s.TwistDeg = 120; s.TwistFollowsVariation = true;
            }),
        new("atrator", Loc.T("Abertura por atrator", "Attractor opening"), Loc.T("As lâminas se abrem perto de um ponto da fachada (vista, entrada).", "Fins open near a point of the facade (view, entrance)."),
            s =>
            {
                s.Profile = FinProfile.Plate; s.Depth = U.Mm(350); s.Thickness = U.Mm(30); s.Spacing = U.Mm(350);
                s.Variation = VariationMode.Attractor; s.AttractorPosition = 0.5; s.AttractorRadius = 0.35; s.AmplitudeDeg = 75; s.BaseAngleDeg = 0;
            }),
        new("gota", Loc.T("Aerofólios aleatórios", "Random airfoils"), Loc.T("Seção em gota com rotação aleatória controlada.", "Teardrop section with controlled random rotation."),
            s =>
            {
                s.Profile = FinProfile.Airfoil; s.Depth = U.Mm(320); s.Thickness = U.Mm(80); s.Spacing = U.Mm(380);
                s.Variation = VariationMode.Random; s.AmplitudeDeg = 30; s.Seed = 11;
            }),
        new("tubos", Loc.T("Tubos com topo em onda", "Tubes with wavy top"), Loc.T("Tubos redondos cujo topo desenha ondas.", "Round tubes whose tops draw waves."),
            s =>
            {
                s.Profile = FinProfile.Round; s.Depth = U.Mm(100); s.Spacing = U.Mm(220);
                s.Variation = VariationMode.None; s.TopTrim = FinTrim.Wave; s.TopAmount = -U.M(1.5); s.TrimWaves = 2;
            }),
        new("profundidade", Loc.T("Profundidade em onda", "Depth wave"), Loc.T("Lâminas retas cuja profundidade ondula ao longo da fachada.", "Straight fins whose depth undulates along the facade."),
            s =>
            {
                s.Profile = FinProfile.Plate; s.Depth = U.Mm(450); s.Thickness = U.Mm(30); s.Spacing = U.Mm(300);
                s.Variation = VariationMode.Sine; s.AmplitudeDeg = 0; s.Periods = 2; s.DepthVariation = 0.6;
            }),
        new("losangos", Loc.T("Losangos em degradê", "Diamond gradient"), Loc.T("Seção em losango girando de forma linear de uma ponta à outra.", "Diamond section rotating linearly end to end."),
            s =>
            {
                s.Profile = FinProfile.Rhombus; s.Depth = U.Mm(300); s.Thickness = U.Mm(120); s.Spacing = U.Mm(400);
                s.Variation = VariationMode.Linear; s.AmplitudeDeg = 45;
            }),
    };

    public static FinStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);
}
