using TeraTools.Core.Geometry;
using TeraTools.Core.Patterns;
using TeraTools.Core.Units;
using Xunit;

namespace TeraTools.Core.Tests;

public class PixyPatternTests
{
    private static readonly Region2 Face = new(Polygon2.Rectangle(Vec2.Zero, U.M(3), U.M(2.4)));

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
    public void Every_pattern_generates_cells_fully_inside_the_face(PixyPattern pattern)
    {
        var cells = PixyPatternGenerator.Generate(new PixySettings { Pattern = pattern, CellSize = U.Mm(250) }, Face);
        Assert.True(cells.Count > 10, $"{pattern}: {cells.Count}");
        foreach (var c in cells)
            foreach (var p in c.Shape)
                Assert.True(Face.Contains(p, 1e-6));
    }

    [Fact]
    public void Voronoi_cells_do_not_overlap()
    {
        var cells = PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Voronoi, CellSize = U.Mm(300), OpeningRatio = 0.9 }, Face);
        for (var i = 0; i < cells.Count; i++)
            for (var j = i + 1; j < cells.Count; j++)
                Assert.False(PolygonOps.Intersects(cells[i].Shape, cells[j].Shape, 1e-6), $"{i} x {j}");
    }

    [Fact]
    public void Opening_ratio_controls_open_area()
    {
        double Area(double r) => PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Hexagonal, OpeningRatio = r }, Face).Sum(c => c.Shape.Area);
        Assert.True(Area(0.8) > Area(0.4) * 2);
    }

    [Fact]
    public void Linear_fade_shrinks_shapes_along_direction()
    {
        var cells = PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Squares, Fade = FadeMode.Linear, FadeAngleDeg = 0, FadeFrom = 1, FadeTo = 0.3 }, Face);
        var left = cells.Where(c => c.Center.X < U.M(1)).Average(c => c.Shape.Area);
        var right = cells.Where(c => c.Center.X > U.M(2)).Average(c => c.Shape.Area);
        Assert.True(left > right * 2);
    }

    [Fact]
    public void Random_skip_and_limit_reduce_count()
    {
        var all = PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Circles }, Face).Count;
        var some = PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Circles, RandomSkip = 0.5 }, Face).Count;
        Assert.InRange(some, all * 0.3, all * 0.7);
        Assert.Equal(7, PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Circles }, Face, 7).Count);
    }

    [Fact]
    public void Star_has_alternating_radii()
    {
        var star = PixyPatternGenerator.Star(Vec2.Zero, 1, 0.4, 6, 0);
        Assert.Equal(12, star.Count);
        Assert.Equal(1, star[0].Length, 9);
        Assert.Equal(0.4, star[1].Length, 9);
    }

    [Fact]
    public void Cells_avoid_holes_in_the_face()
    {
        var withWindow = new Region2(Face.Outer, new[] { Polygon2.Rectangle(new Vec2(U.M(1), U.M(0.8)), U.M(1), U.M(0.8)) });
        var cells = PixyPatternGenerator.Generate(new PixySettings { Pattern = PixyPattern.Squares, CellSize = U.Mm(200) }, withWindow);
        foreach (var c in cells) Assert.False(withWindow.Holes[0].Contains(c.Center));
    }
}
