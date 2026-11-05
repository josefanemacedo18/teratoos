using TeraTools.Core.Geometry;
using TeraTools.Core.Organic;
using TeraTools.Core.Units;
using Xunit;
using Xunit.Abstractions;

namespace TeraTools.Core.Tests;

/// <summary>Colunas orgânicas: toda peça gerada precisa ser um sólido fechado, com volume e medidas coerentes.</summary>
public class OrganicColumnTests
{
    private readonly ITestOutputHelper _out;
    public OrganicColumnTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Styles() => ColumnStyles.All.Select(s => new object[] { s.Id });

    private static void AssertSolid(ColumnResult r, string label)
    {
        Assert.NotEmpty(r.Meshes);
        foreach (var m in r.Meshes)
        {
            Assert.True(m.IsClosed(), $"{label}: peça '{m.Role}' com malha aberta");
            Assert.True(m.Volume > 0, $"{label}: peça '{m.Role}' sem volume");
            Assert.All(m.Vertices, v => Assert.True(double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z), $"{label}: vértice inválido"));
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void EveryStyleBuildsClosedSolids(string id)
    {
        var s = ColumnStyles.Find(id)!.Create();
        var r = ColumnGenerator.Generate(s);
        AssertSolid(r, id);
        var (min, max) = r.Meshes.Select(m => m.Bounds()).Aggregate((a, b) =>
            (new Vec3(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z)),
             new Vec3(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z))));
        Assert.InRange(min.Z, -1e-6, U.Mm(1));
        Assert.InRange(max.Z, s.Height - U.Mm(1), s.Height + 1e-6);
        Assert.True(r.TriangleCount < 200_000, $"{id}: {r.TriangleCount} triângulos");
        _out.WriteLine($"{id}: {r.Meshes.Count} peças, {r.TriangleCount} triângulos, {U.CuFtToM3(r.Quantities.Volume):0.000} m³, λ={r.Quantities.Slenderness:0}, avisos: {string.Join(" | ", r.Warnings)}");
    }

    [Fact]
    public void CylinderVolumeMatchesFormula()
    {
        var s = new ColumnSettings { Height = U.M(3), BaseRadius = U.Mm(200), TopRadius = U.Mm(200), Resolution = 128 };
        var r = ColumnGenerator.Generate(s);
        AssertSolid(r, "cilindro");
        var expected = Math.PI * Math.Pow(U.Mm(200), 2) * U.M(3);
        Assert.InRange(r.Quantities.Volume, expected * 0.99, expected * 1.001);
        // λ = L / i, i = r/2 para seção circular cheia → 3000 / 100 = 30
        Assert.InRange(r.Quantities.Slenderness, 29, 31);
    }

    [Fact]
    public void ShellIsLighterThanSolid()
    {
        var solid = ColumnGenerator.Generate(new ColumnSettings());
        var shell = ColumnGenerator.Generate(new ColumnSettings { Build = ColumnBuild.Shell, ShellThickness = U.Mm(40) });
        AssertSolid(shell, "casca");
        Assert.True(shell.Quantities.Volume < solid.Quantities.Volume * 0.5);
        // tubo de 200/160 mm: área ≈ π(0,2² − 0,16²)
        var expected = Math.PI * (Math.Pow(U.Mm(200), 2) - Math.Pow(U.Mm(160), 2)) * U.M(3);
        Assert.InRange(shell.Quantities.Volume, expected * 0.98, expected * 1.02);
    }

    [Theory]
    [InlineData(ColumnSection.Circle)]
    [InlineData(ColumnSection.Polygon)]
    [InlineData(ColumnSection.Lobed)]
    [InlineData(ColumnSection.Star)]
    [InlineData(ColumnSection.Ellipse)]
    public void SectionsAreUnitAndCounterClockwise(ColumnSection kind)
    {
        var pts = OrganicSection.Unit(kind, 48);
        Assert.Equal(48, pts.Count);
        Assert.True(new Polygon2(pts).SignedArea > 0);
        Assert.InRange(pts.Max(p => p.Length), 0.97, 1.0001);
    }

    [Fact]
    public void ResampleKeepsCorners()
    {
        var square = Polygon2.Rectangle(new Vec2(-1, -1), 2, 2);
        var pts = OrganicSection.Resample(square, 40);
        Assert.Equal(40, pts.Count);
        foreach (var c in square) Assert.Contains(pts, p => p.DistanceTo(c) < 1e-9);
    }

    [Fact]
    public void DrawnSectionSilhouetteAndSpineAreUsed()
    {
        // seção em L desenhada (não convexa), perfil que afina no meio, eixo desenhado que sobe e deita
        var l = new List<Vec2> { new(0, 0), new(U.Mm(600), 0), new(U.Mm(600), U.Mm(200)), new(U.Mm(200), U.Mm(200)), new(U.Mm(200), U.Mm(600)), new(0, U.Mm(600)) };
        var s = new ColumnSettings
        {
            Section = ColumnSection.Drawn, DrawnSection = l,
            Silhouette = ColumnSilhouette.Drawn, DrawnSilhouette = { new(U.Mm(300), 0), new(U.Mm(150), U.M(1.5)), new(U.Mm(300), U.M(3)) },
            Spine = ColumnSpine.Drawn, DrawnSpine = { new(0, 0, 0), new(0, 0, U.M(2)), new(U.M(1.5), 0, U.M(3)) },
        };
        var r = ColumnGenerator.Generate(s);
        AssertSolid(r, "desenhada");
        Assert.DoesNotContain(r.Warnings, w => w.Contains("desenhad"));
        var (min, max) = r.Meshes[0].Bounds();
        Assert.True(max.X > U.M(1.4), "o topo deve seguir o eixo desenhado");
        Assert.InRange(r.Quantities.MinRadius, U.Mm(140), U.Mm(160));
    }

    [Fact]
    public void FormulaSilhouetteAndInvalidFormula()
    {
        var ok = ColumnGenerator.Generate(new ColumnSettings { Silhouette = ColumnSilhouette.Formula, RadiusFormula = "150 + 100*t" });
        AssertSolid(ok, "fórmula");
        Assert.InRange(ok.Quantities.MaxRadius, U.Mm(249), U.Mm(251));
        var bad = ColumnGenerator.Generate(new ColumnSettings { Silhouette = ColumnSilhouette.Formula, RadiusFormula = "150 + (" });
        AssertSolid(bad, "fórmula inválida");
        Assert.Contains(bad.Warnings, w => w.Contains("Fórmula"));
    }

    [Fact]
    public void SlenderColumnIsFlagged()
    {
        var r = ColumnGenerator.Generate(new ColumnSettings { Height = U.M(6), BaseRadius = U.Mm(50), TopRadius = U.Mm(50) });
        Assert.Contains(r.Warnings, w => w.Contains("esbelta"));
    }

    [Theory]
    [InlineData(ColumnBuild.Ribs)]
    [InlineData(ColumnBuild.Grid)]
    [InlineData(ColumnBuild.Lattice)]
    [InlineData(ColumnBuild.Lamellas)]
    [InlineData(ColumnBuild.Petals)]
    [InlineData(ColumnBuild.Shell)]
    public void EveryBuildWorksWithEveryEndAndSpine(ColumnBuild build)
    {
        foreach (var spine in new[] { ColumnSpine.Straight, ColumnSpine.Inclined, ColumnSpine.Bow, ColumnSpine.SCurve })
            foreach (var end in Enum.GetValues<ColumnEnd>())
            {
                var s = new ColumnSettings { Build = build, Spine = spine, Base = end, Capital = end, CoreRadius = U.Mm(60), TwistDeg = 45, Resolution = 24 };
                var r = ColumnGenerator.Generate(s);
                AssertSolid(r, $"{build}/{spine}/{end}");
                if (build is ColumnBuild.Ribs or ColumnBuild.Grid or ColumnBuild.Lattice) Assert.True(r.Quantities.MemberLength > 0);
            }
    }

    [Fact]
    public void SectionPropertiesOfSquare()
    {
        var sq = Polygon2.Rectangle(new Vec2(-0.5, -0.5), 1, 1);
        var (a, i) = OrganicSection.Properties(sq.Points);
        Assert.Equal(1, a, 9);
        Assert.Equal(1.0 / 12, i, 9);
    }
}
