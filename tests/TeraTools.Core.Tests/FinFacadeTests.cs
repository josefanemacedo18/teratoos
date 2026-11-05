using TeraTools.Core.Facade;
using TeraTools.Core.Geometry;
using TeraTools.Core.Organic;
using TeraTools.Core.Units;
using Xunit;
using Xunit.Abstractions;

namespace TeraTools.Core.Tests;

/// <summary>Fachada de lâminas personalizadas.</summary>
public class FinFacadeTests
{
    private readonly ITestOutputHelper _out;
    public FinFacadeTests(ITestOutputHelper output) => _out = output;

    private static readonly PolylinePath Straight = new(new[] { new Vec2(0, 0), new Vec2(U.M(15), 0) });
    private static readonly PolylinePath Corner = new(new[] { new Vec2(0, 0), new Vec2(U.M(10), 0), new Vec2(U.M(10), U.M(6)) });

    public static IEnumerable<object[]> Styles() => FinStyles.All.Select(s => new object[] { s.Id });

    private static FinFacadeSettings Style(string id)
    {
        var s = new FinFacadeSettings { TopZ = U.M(9) };
        FinStyles.Find(id)!.Apply(s);
        return s;
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void EveryStyleBuildsClosedFins(string id)
    {
        foreach (var path in new[] { Straight, Corner })
        {
            var s = Style(id);
            var r = FinFacadeGenerator.Generate(s, path);
            Assert.NotEmpty(r.Fins);
            foreach (var f in r.Fins)
            {
                Assert.True(f.Mesh.IsClosed(), $"{id}: lâmina {f.Index} aberta");
                Assert.True(f.Mesh.Volume > 0);
                Assert.True(f.Top > f.Bottom);
            }
            foreach (var b in r.Bands) Assert.True(b.IsClosed(), $"{id}: faixa aberta");
            _out.WriteLine($"{id}: {r.Fins.Count} lâminas, {r.Bands.Count} faixas ({U.ToM(r.BandLength):0.0} m), {r.TriangleCount} tri, avisos: {string.Join(" | ", r.Warnings)}");
        }
    }

    [Fact]
    public void SpacingIsCenteredAndCountWorks()
    {
        var s = new FinFacadeSettings { Spacing = U.Mm(400), StartMargin = 0, EndMargin = 0 };
        var pos = FinFacadeGenerator.Positions(s, U.M(2));
        Assert.Equal(6, pos.Count);
        Assert.Equal(U.M(1), (pos[0] + pos[^1]) / 2, 9);
        s.Count = 4;
        pos = FinFacadeGenerator.Positions(s, U.M(3));
        Assert.Equal(4, pos.Count);
        for (var i = 0; i < 4; i++) Assert.Equal(U.M(i), pos[i], 6);
    }

    [Fact]
    public void ArchOpensTheEntrance()
    {
        var s = Style("cobre");
        var r = FinFacadeGenerator.Generate(s, Straight);
        var mid = r.Fins.OrderBy(f => Math.Abs(f.Position - U.M(7.5))).First();
        Assert.InRange(mid.Bottom, U.M(4.1), U.M(4.21));
        var edge = r.Fins.First();
        Assert.Equal(0, edge.Bottom, 6);
        // as faixas são interrompidas no arco: a de baixo (3 m) passa pela abertura e vira dois trechos
        Assert.True(r.Bands.Count >= 3);
    }

    [Fact]
    public void OpeningZeroClosesAllFins()
    {
        var s = Style("cobre");
        s.Opening = 0;
        s.BaseAngleDeg = 5;
        var r = FinFacadeGenerator.Generate(s, Straight);
        Assert.All(r.Fins, f => Assert.Equal(5, f.AngleBottom, 6));
        s.Opening = 1;
        var r2 = FinFacadeGenerator.Generate(s, Straight);
        Assert.Contains(r2.Fins, f => Math.Abs(f.AngleBottom - 5) > 30);
    }

    [Fact]
    public void TwistChangesAngleWithHeight()
    {
        var s = Style("torcidas");
        var r = FinFacadeGenerator.Generate(s, Straight);
        Assert.All(r.Fins, f => Assert.Equal(90, f.AngleTop - f.AngleBottom, 6));
        // seção do topo girada 90°: a lâmina de chapa fica paralela à fachada lá em cima
        var f0 = r.Fins[0];
        var topRing = f0.Mesh.Vertices.Where(v => Math.Abs(v.Z - f0.Top) < 1e-6).ToList();
        var spanY = topRing.Max(v => v.Y) - topRing.Min(v => v.Y);
        Assert.InRange(spanY, U.Mm(25), U.Mm(35));
    }

    [Fact]
    public void FormulaDrivesRotationAndDrawnTrimFollowsCurve()
    {
        var s = new FinFacadeSettings { Variation = VariationMode.Formula, Formula = "90*u", TopZ = U.M(4),
            TopTrim = FinTrim.Drawn, TopCurve = { new Vec3(0, U.M(1), U.M(3)), new Vec3(U.M(15), U.M(1), U.M(5)) } };
        var r = FinFacadeGenerator.Generate(s, Straight);
        Assert.Equal(0, r.Fins.First().AngleBottom, 6);
        Assert.Equal(90, r.Fins.Last().AngleBottom, 6);
        Assert.True(r.Fins.Last().Top > r.Fins.First().Top + U.M(1.5));
    }

    [Fact]
    public void TouchingFinsAreFlagged()
    {
        var s = new FinFacadeSettings { Spacing = U.Mm(100), Depth = U.Mm(400), Variation = VariationMode.None, BaseAngleDeg = 80 };
        var r = FinFacadeGenerator.Generate(s, Straight);
        Assert.Contains(r.Warnings, w => w.Contains("se tocam"));
    }

    [Fact]
    public void CutListGroupsEqualFins()
    {
        var s = Style("torcidas");
        var r = FinFacadeGenerator.Generate(s, Straight);
        var t = r.CutList();
        Assert.Equal(2, t.RowCount); // uma linha de grupo + total
        Assert.Equal(r.Fins.Count, Convert.ToInt32(t.Rows[0][3]));
    }
}
