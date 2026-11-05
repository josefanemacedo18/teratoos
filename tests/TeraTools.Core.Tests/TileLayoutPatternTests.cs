using TeraTools.Core.Geometry;
using TeraTools.Core.Tiling;
using TeraTools.Core.Units;
using Xunit;

namespace TeraTools.Core.Tests;

/// <summary>Padrões de assentamento: cobrem o ambiente, sem sobreposição, com a junta pedida.</summary>
public class TileLayoutPatternTests
{
    private static readonly Region2 Room = new(Polygon2.Rectangle(Vec2.Zero, U.M(3), U.M(2.4)));

    public static IEnumerable<object[]> AllLayouts() => Enum.GetValues<TileLayout>().Select(l => new object[] { l });

    private static TileSettings Settings(TileLayout layout, double joint = 2) => new()
    {
        Layout = layout,
        TileLength = U.Mm(layout is TileLayout.Hexagon or TileLayout.OctagonDot or TileLayout.Rhombus3D ? 200 : 600),
        TileWidth = U.Mm(layout is TileLayout.OpusRomano ? 100 : 150),
        Joint = U.Mm(joint),
        Start = TileStart.Corner,
    };

    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void CobreOAmbienteSemSobreposicao(TileLayout layout)
    {
        var r = TileLayoutEngine.Generate(Room, Settings(layout));
        Assert.NotEmpty(r.Tiles);
        // cobertura: só as juntas ficam de fora
        var coverage = r.CoveredArea / Room.Area;
        Assert.InRange(coverage, 0.90, 1.0 + 1e-9);
        // sem sobreposição entre peças (amostra das peças inteiras)
        var full = r.Tiles.Where(t => t.Status == TileStatus.Full).Take(150).ToList();
        for (var i = 0; i < full.Count; i++)
            for (var j = i + 1; j < full.Count; j++)
            {
                if (!full[i].Outline.Bounds.Intersects(full[j].Outline.Bounds)) continue;
                var overlap = full[i].Outline.ClipByConvex(full[j].Outline).Area;
                Assert.True(overlap < U.M2ToSqFt(1e-6), $"{layout}: peças {full[i].Index} e {full[j].Index} se sobrepõem");
            }
    }

    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void SemJuntaCobreTudo(TileLayout layout)
    {
        var r = TileLayoutEngine.Generate(Room, Settings(layout, 0));
        Assert.InRange(r.CoveredArea / Room.Area, 0.995, 1.0 + 1e-6);
    }

    [Fact]
    public void HexagonoTemOTamanhoNominal()
    {
        var r = TileLayoutEngine.Generate(Room, Settings(TileLayout.Hexagon));
        var t = r.Tiles.First(x => x.Status == TileStatus.Full);
        // ponta a ponta = 200 mm
        Assert.Equal(U.Mm(200), t.Length, 3);
    }

    [Fact]
    public void MadeiraAleatoriaEhEstavelEVariada()
    {
        var a = TileLayoutEngine.Generate(Room, Settings(TileLayout.PlankRandom));
        var b = TileLayoutEngine.Generate(Room, Settings(TileLayout.PlankRandom));
        Assert.Equal(a.Tiles.Count, b.Tiles.Count);
        var lengths = a.Tiles.Where(t => t.Status == TileStatus.Full).Select(t => Math.Round(U.ToMm(t.Length))).Distinct().Count();
        Assert.True(lengths > 5, "as réguas deveriam ter comprimentos variados");
        var other = TileLayoutEngine.Generate(Room, new TileSettings { Layout = TileLayout.PlankRandom, TileLength = U.Mm(600), TileWidth = U.Mm(150), Joint = U.Mm(2), Seed = 7 });
        Assert.NotEqual(a.Tiles.Select(t => t.Outline.Centroid.X).Take(20), other.Tiles.Select(t => t.Outline.Centroid.X).Take(20));
    }

    [Fact]
    public void OpusRomanoUsaQuatroTamanhos()
    {
        var r = TileLayoutEngine.Generate(Room, Settings(TileLayout.OpusRomano));
        Assert.Equal(4, r.Tiles.Where(t => t.Status == TileStatus.Full).Select(t => t.SizeKey).Distinct().Count());
    }

    [Fact]
    public void JuntasDividemOsPadroesPoligonais()
    {
        foreach (var layout in new[] { TileLayout.Chevron, TileLayout.Hexagon, TileLayout.OctagonDot, TileLayout.Rhombus3D })
            Assert.NotEmpty(TileLayoutEngine.Generate(Room, Settings(layout)).JointLines);
    }
}
