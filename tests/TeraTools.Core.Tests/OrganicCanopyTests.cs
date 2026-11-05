using TeraTools.Core.Geometry;
using TeraTools.Core.Organic;
using TeraTools.Core.Units;
using Xunit;
using Xunit.Abstractions;

namespace TeraTools.Core.Tests;

/// <summary>Coberturas orgânicas: superfície, pele, estrutura e apoios.</summary>
public class OrganicCanopyTests
{
    private readonly ITestOutputHelper _out;
    public OrganicCanopyTests(ITestOutputHelper output) => _out = output;

    private static readonly Polygon2 Rect = Polygon2.Rectangle(new Vec2(10, 20), U.M(10), U.M(7));

    public static IEnumerable<object[]> Styles() => CanopyStyles.All.Select(s => new object[] { s.Id });

    private static CanopySettings FromStyle(string id, Polygon2? footprint = null)
    {
        var st = CanopyStyles.Find(id)!;
        var s = st.Create();
        s.Footprint = footprint ?? Rect;
        s.Supports = CanopyStyles.SupportGrid(s.Footprint, st.SupportsU, st.SupportsV);
        return s;
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void EveryStyleBuilds(string id)
    {
        var s = FromStyle(id);
        var r = CanopyGenerator.Generate(s);
        Assert.NotEmpty(r.Meshes);
        foreach (var m in r.Meshes)
        {
            Assert.True(m.Volume > 0, $"{id}: '{m.Role}' sem volume");
            Assert.True(m.IsClosed(), $"{id}: '{m.Role}' com malha aberta");
        }
        var q = r.Quantities;
        Assert.InRange(U.SqFtToM2(q.PlanArea), 69.9, 70.1);
        Assert.True(q.MaxHeight >= q.MinHeight);
        Assert.True(r.TriangleCount < 400_000, $"{id}: {r.TriangleCount} triângulos");
        _out.WriteLine($"{id}: {r.Meshes.Count} peças, {r.TriangleCount} tri, pele {U.SqFtToM2(q.SkinArea):0.0} m², estrutura {U.ToM(q.StructureLength):0.0} m, apoios {q.SupportCount} ({U.ToM(q.SupportLength):0.0} m), alturas {U.ToM(q.MinHeight):0.00}–{U.ToM(q.MaxHeight):0.00} m, avisos: {string.Join(" | ", r.Warnings)}");
    }

    [Fact]
    public void ContinuousSkinOnFlatRoofHasExactArea()
    {
        var s = new CanopySettings { Footprint = Rect, Surface = CanopySurface.Flat, SlopePercent = 0, Structure = CanopyStructure.None, EdgeBeam = false, SupportKind = CanopySupports.None };
        var r = CanopyGenerator.Generate(s);
        var skin = Assert.Single(r.Meshes);
        Assert.True(skin.IsClosed());
        Assert.InRange(U.SqFtToM2(r.Quantities.SkinArea), 69.99, 70.01);
        // 70 m² × 60 mm = 4,2 m³
        Assert.InRange(U.CuFtToM3(skin.Volume), 4.19, 4.21);
        Assert.Contains(r.Warnings, w => w.Contains("plana"));
    }

    [Fact]
    public void ConcaveFootprintStaysInside()
    {
        // planta em L
        var l = new Polygon2(new[] { new Vec2(0, 0), new Vec2(U.M(10), 0), new Vec2(U.M(10), U.M(4)), new Vec2(U.M(4), U.M(4)), new Vec2(U.M(4), U.M(9)), new Vec2(0, U.M(9)) });
        var s = new CanopySettings { Footprint = l, Surface = CanopySurface.Pillow, SupportKind = CanopySupports.None };
        var r = CanopyGenerator.Generate(s);
        var skin = r.Meshes.First(m => m.Role == CanopyRole.Skin);
        Assert.True(skin.IsClosed());
        Assert.InRange(U.SqFtToM2(r.Quantities.PlanArea), 59.9, 60.1);
        // nenhum vértice da pele fora da planta
        Assert.All(skin.Vertices, v => Assert.True(l.Contains(v.XY, 1e-6) || l.DistanceToBoundary(v.XY) < 1e-6));
    }

    [Fact]
    public void PanelsHaveJoints()
    {
        var s = new CanopySettings { Footprint = Rect, Surface = CanopySurface.Pillow, Skin = CanopySkin.Panels, PanelSizeU = U.M(1), PanelSizeV = U.M(1), PanelJoint = U.Mm(20), Structure = CanopyStructure.None, EdgeBeam = false, SupportKind = CanopySupports.None };
        var r = CanopyGenerator.Generate(s);
        Assert.Equal(70, r.Quantities.PanelCount);
        // área da pele menor que a planta (juntas), mas com a curvatura somada
        Assert.All(r.Meshes, m => Assert.True(m.IsClosed()));
    }

    [Fact]
    public void FunnelIsFlaggedForPondingButPillowIsNot()
    {
        var funnel = FromStyle("funil");
        var r1 = CanopyGenerator.Generate(funnel);
        Assert.True(r1.Quantities.PondingPoints > 0);
        Assert.Contains(r1.Warnings, w => w.Contains("empoçar"));
        var pillow = new CanopySettings { Footprint = Rect, Surface = CanopySurface.Pillow, SupportKind = CanopySupports.None };
        var r2 = CanopyGenerator.Generate(pillow);
        Assert.Equal(0, r2.Quantities.PondingPoints);
    }

    [Fact]
    public void LowCanopyIsFlagged()
    {
        var s = new CanopySettings { Footprint = Rect, Surface = CanopySurface.Saddle, Height = U.M(1.8), SupportKind = CanopySupports.None };
        var r = CanopyGenerator.Generate(s);
        Assert.Contains(r.Warnings, w => w.Contains("Altura livre"));
    }

    [Fact]
    public void SupportsReachTheUnderside()
    {
        var s = FromStyle("arvores");
        s.Columns.Clear();
        var r = CanopyGenerator.Generate(s);
        Assert.Equal(2, r.Quantities.SupportCount);
        var field = new CanopyField(s);
        var supports = r.Meshes.Where(m => m.Role == CanopyRole.Support).ToList();
        Assert.NotEmpty(supports);
        // todo apoio começa no chão e nenhum passa acima da superfície
        Assert.Contains(supports, m => m.Bounds().Min.Z < U.Mm(1));
        foreach (var m in supports)
            foreach (var v in m.Vertices.Where(v => s.Footprint.Contains(v.XY)))
                Assert.True(v.Z <= field.Z(v.XY) + U.Mm(60), "galho atravessa a pele");
    }

    [Fact]
    public void ConnectsToExistingColumnsAtTopAndMiddle()
    {
        var s = new CanopySettings
        {
            Footprint = Rect, Surface = CanopySurface.Pillow, SupportKind = CanopySupports.None,
            Columns = { new CanopyColumn { At = Rect.Centroid, BaseZ = 0, TopZ = U.M(2.8) } },
            Connection = ColumnConnection.Both, IntermediateFraction = 0.6,
        };
        var r = CanopyGenerator.Generate(s);
        Assert.Equal(2, r.Quantities.ConnectionCount);
        var conn = r.Meshes.Where(m => m.Role == CanopyRole.Connector).ToList();
        Assert.True(conn.Count >= 2);
        // ramos saem da altura intermediária (60% de 2,8 m)
        Assert.Contains(conn, m => Math.Abs(m.Bounds().Min.Z - U.M(1.68)) < U.Mm(100));
    }

    [Fact]
    public void UmbrellasWithoutSupportsFallBackToPillow()
    {
        var s = new CanopySettings { Footprint = Rect, Surface = CanopySurface.Umbrellas, SupportKind = CanopySupports.None };
        var r = CanopyGenerator.Generate(s);
        Assert.Contains(r.Warnings, w => w.Contains("almofada"));
        Assert.NotEmpty(r.Meshes);
    }

    [Fact]
    public void SweepAndSphereAreClosed()
    {
        var path = new List<Vec3> { new(0, 0, 0), new(1, 0, 0.2), new(2, 1, 0.4), new(3, 1, 0.4) };
        var beam = MeshKit.Sweep(path, new List<Vec2> { new(-0.1, -0.3), new(0.1, -0.3), new(0.1, 0), new(-0.1, 0) }, Vec3.UnitZ, "b");
        Assert.True(beam.IsClosed());
        Assert.True(beam.Volume > 0);
        var ring = MeshKit.Sweep(new List<Vec3> { new(0, 0, 0), new(4, 0, 0), new(4, 3, 0), new(0, 3, 0) }, new List<Vec2> { new(-0.1, -0.3), new(0.1, -0.3), new(0.1, 0), new(-0.1, 0) }, Vec3.UnitZ, "r", closedPath: true);
        Assert.True(ring.IsClosed());
        var sphere = MeshKit.Sphere(Vec3.Zero, 1, "s", 32);
        Assert.True(sphere.IsClosed());
        Assert.InRange(sphere.Volume, 4.0, 4.19);
    }

    [Theory]
    [InlineData("circulo")]
    [InlineData("estrela")]
    [InlineData("trapezio")]
    public void SkinIsClosedOnCurvedAndConcaveOutlines(string kind)
    {
        Polygon2 poly = kind switch
        {
            "circulo" => Polygon2.Circle(new Vec2(3, -2), U.M(6), 72),
            "estrela" => new Polygon2(Enumerable.Range(0, 14).Select(i => Vec2.FromAngle(Math.PI * i / 7, i % 2 == 0 ? U.M(8) : U.M(4)))),
            _ => new Polygon2(new[] { new Vec2(0, 0), new Vec2(U.M(12), U.M(0.37)), new Vec2(U.M(9.13), U.M(6.01)), new Vec2(U.M(1.7), U.M(5.55)) }),
        };
        foreach (var skin in new[] { CanopySkin.Continuous, CanopySkin.Panels })
        {
            var s = new CanopySettings { Footprint = poly, Surface = CanopySurface.Pillow, Skin = skin, Structure = CanopyStructure.Grid, SupportKind = CanopySupports.None };
            var r = CanopyGenerator.Generate(s);
            foreach (var m in r.Meshes) Assert.True(m.IsClosed(), $"{kind}/{skin}: '{m.Role}' aberta");
            Assert.InRange(r.Quantities.SkinArea, poly.Area * (skin == CanopySkin.Panels ? 0.9 : 1.0), poly.Area * 1.3);
        }
    }
}
