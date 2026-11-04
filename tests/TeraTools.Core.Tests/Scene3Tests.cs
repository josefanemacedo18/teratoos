using TeraTools.Core.Geometry;
using Xunit;

namespace TeraTools.Core.Tests;

public class Scene3Tests
{
    private static double TriArea(Polygon2 p, (int A, int B, int C) t) => Math.Abs((p[t.B] - p[t.A]).Cross(p[t.C] - p[t.A])) / 2;

    [Fact]
    public void Ear_clipping_covers_concave_star_exactly()
    {
        var pts = new List<Vec2>();
        for (var i = 0; i < 10; i++)
        {
            var r = i % 2 == 0 ? 1.0 : 0.4;
            var a = Math.PI / 2 + i * Math.PI / 5;
            pts.Add(new Vec2(r * Math.Cos(a), r * Math.Sin(a)));
        }
        var star = new Polygon2(pts);
        var tris = Triangulation.EarClip(star);
        Assert.Equal(8, tris.Count);
        Assert.Equal(star.Area, tris.Sum(t => TriArea(star, t)), 9);
    }

    [Fact]
    public void Ear_clipping_works_for_clockwise_input()
    {
        var sq = new Polygon2(new[] { new Vec2(0, 0), new Vec2(0, 1), new Vec2(1, 1), new Vec2(1, 0) });
        var tris = Triangulation.EarClip(sq);
        Assert.Equal(2, tris.Count);
        Assert.Equal(1, tris.Sum(t => TriArea(sq, t)), 9);
    }

    [Fact]
    public void Scene_counts_triangles_and_bounds()
    {
        var s = new Scene3();
        s.Box(new OrientedBox(Frame3.World, 2, 3, 4), "#FF0000");
        s.Prism(Frame3.World, Polygon2.Regular(6, 1, Vec2.Zero), 5, 1, "#00FF00");
        Assert.Equal(12 + (4 * 2 + 12), s.TriangleCount);
        var b = s.Bounds()!.Value;
        Assert.Equal(0, b.Min.Z, 9);
        Assert.Equal(6, b.Max.Z, 9);
        Assert.Equal(2, s.Meshes.Count());
    }

    [Fact]
    public void Scene_truncates_above_limit()
    {
        var s = new Scene3 { MaxTriangles = 30 };
        for (var i = 0; i < 10; i++) s.Box(new OrientedBox(Frame3.World, 1, 1, 1), "#000000");
        Assert.True(s.Truncated);
        Assert.Equal(24, s.TriangleCount);
    }
}
