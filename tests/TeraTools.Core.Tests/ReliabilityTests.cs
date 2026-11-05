using TeraTools.Core.Geometry;
using TeraTools.Core.Patterns;
using TeraTools.Core.Reporting;
using TeraTools.Core.Units;
using Xunit;

namespace TeraTools.Core.Tests;

/// <summary>
/// Garantias que evitam o "não gera nada": contornos sempre fechados e válidos para o Revit,
/// e falhas que aparecem no relatório em vez de sumirem.
/// </summary>
public class ReliabilityTests
{
    /// <summary>Tolerância de curva curta do Revit (≈ 0,78 mm) × 2, como no gerador de padrões.</summary>
    private const double MinEdge = 0.00256 * 2;

    [Fact]
    public void CleanForSketch_FundeVerticesProximosEMantemOLacoFechado()
    {
        // quadrado com um vértice extra a 0,1 mm do canto: pular o segmento abriria o laço
        var p = new Polygon2(new[]
        {
            new Vec2(0, 0), new Vec2(U.Mm(0.1), 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1),
        });
        var c = p.CleanForSketch(MinEdge);
        Assert.NotNull(c);
        Assert.Equal(4, c!.Count);
        for (var i = 0; i < c.Count; i++)
            Assert.True(c[i].DistanceTo(c[(i + 1) % c.Count]) >= MinEdge);
        Assert.Equal(1.0, c.Area, 6);
    }

    [Fact]
    public void CleanForSketch_DescartaFormaDegenerada()
    {
        var tiny = Polygon2.Rectangle(Vec2.Zero, U.Mm(0.5), U.Mm(0.5));
        Assert.Null(tiny.CleanForSketch(MinEdge));
    }

    [Fact]
    public void IsSimple_DetectaLacoCruzado()
    {
        var bowTie = new Polygon2(new[] { new Vec2(0, 0), new Vec2(1, 1), new Vec2(1, 0), new Vec2(0, 1) });
        Assert.False(bowTie.IsSimple());
        Assert.True(Polygon2.Rectangle(Vec2.Zero, 1, 1).IsSimple());
    }

    [Theory]
    [InlineData(PixyPattern.Hexagonal)]
    [InlineData(PixyPattern.Voronoi)]
    [InlineData(PixyPattern.StarBurst)]
    [InlineData(PixyPattern.Circles)]
    [InlineData(PixyPattern.Diamonds)]
    [InlineData(PixyPattern.Triangles)]
    [InlineData(PixyPattern.Squares)]
    [InlineData(PixyPattern.Waves)]
    [InlineData(PixyPattern.Bricks)]
    public void PadroesGeramContornosAceitosPeloRevit(PixyPattern pattern)
    {
        var face = new Region2(Polygon2.Rectangle(Vec2.Zero, U.M(3), U.M(2.4)));
        var cells = PixyPatternGenerator.Generate(new PixySettings { Pattern = pattern, CellSize = U.Mm(150), OpeningRatio = 0.8 }, face);
        Assert.NotEmpty(cells);
        var valid = 0;
        foreach (var cell in cells)
        {
            var c = cell.Shape.CleanForSketch(MinEdge);
            if (c == null) continue;
            valid++;
            Assert.True(c.IsSimple());
            for (var i = 0; i < c.Count; i++)
                Assert.True(c[i].DistanceTo(c[(i + 1) % c.Count]) >= MinEdge);
        }
        // quase todas as formas devem sobreviver à limpeza
        Assert.True(valid >= cells.Count * 0.95, $"{pattern}: só {valid} de {cells.Count} formas válidas");
    }

    [Fact]
    public void Relatorio_AgrupaFalhasComQuantidadeEMotivo()
    {
        var r = new OperationReport();
        r.ItemFailed("Tubos", "Curve is too short");
        r.ItemFailed("Tubos", "outro motivo");
        r.ItemsFailed("Nós", 3, "Invalid profile");
        Assert.Equal(5, r.FailedItems);
        Assert.Equal("Curve is too short", r.FirstFailure);
        Assert.Equal(2, r.WarningCount);
        Assert.Contains(r.Items, i => i.Message.Contains("Tubos") && i.Message.Contains('2') && i.Message.Contains("Curve is too short"));
        Assert.Contains(r.Items, i => i.Message.Contains("Nós") && i.Message.Contains('3'));
    }
}
