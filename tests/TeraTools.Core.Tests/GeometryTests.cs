using TeraTools.Core.Geometry;

namespace TeraTools.Core.Tests;

public class GeometryTests
{
    private static Polygon2 Square(double s) => Polygon2.Rectangle(Vec2.Zero, s, s);

    [Fact]
    public void Area_perimeter_centroid_of_square()
    {
        var p = Square(2);
        Assert.Equal(4, p.Area, 9);
        Assert.Equal(8, p.Perimeter, 9);
        Assert.True(p.Centroid.AlmostEquals(new Vec2(1, 1)));
        Assert.True(p.IsCounterClockwise);
        Assert.True(p.IsConvex);
    }

    [Fact]
    public void Contains_handles_inside_outside_and_boundary()
    {
        var p = Square(2);
        Assert.True(p.Contains(new Vec2(1, 1)));
        Assert.True(p.Contains(new Vec2(2, 1)));
        Assert.False(p.Contains(new Vec2(3, 1)));
    }

    [Fact]
    public void ClipByConvex_concave_subject_has_exact_area()
    {
        // L com área 3 (quadrado 2x2 menos 1x1)
        var l = new Polygon2(new[] { new Vec2(0, 0), new Vec2(2, 0), new Vec2(2, 1), new Vec2(1, 1), new Vec2(1, 2), new Vec2(0, 2) });
        Assert.Equal(3, l.Area, 9);
        var clip = Polygon2.Rectangle(new Vec2(0.5, 0.5), 1, 1); // 0.5..1.5
        var r = l.ClipByConvex(clip);
        Assert.Equal(0.75, r.Area, 9);
    }

    [Fact]
    public void Offset_square_inward_and_outward()
    {
        var p = Square(10);
        Assert.Equal(64, p.Offset(-1).Area, 6);
        Assert.Equal(144, p.Offset(1).Area, 6);
    }

    [Fact]
    public void Offset_collapses_to_empty_when_too_large()
    {
        var p = Square(2);
        Assert.True(p.Offset(-1.5).IsEmpty);
    }

    [Fact]
    public void Offset_concave_L_inward()
    {
        var l = new Polygon2(new[] { new Vec2(0, 0), new Vec2(4, 0), new Vec2(4, 2), new Vec2(2, 2), new Vec2(2, 4), new Vec2(0, 4) });
        var o = l.Offset(-0.5);
        // L encolhido: (0.5,0.5)-(3.5,0.5)-(3.5,1.5)-(1.5,1.5)-(1.5,3.5)-(0.5,3.5) → 3x1 + 1x2 = 5
        Assert.Equal(5, o.Area, 6);
        Assert.Equal(6, o.Count);
    }

    [Fact]
    public void Region_with_hole_area_and_contains()
    {
        var r = new Region2(Square(10), new[] { Polygon2.Rectangle(new Vec2(4, 4), 2, 2) });
        Assert.Equal(96, r.Area, 9);
        Assert.False(r.Contains(new Vec2(5, 5)));
        Assert.True(r.Contains(new Vec2(1, 1)));
        Assert.Equal(2, r.IntersectionArea(Polygon2.Rectangle(new Vec2(3, 4), 2, 2)), 9);
    }

    [Fact]
    public void Region_clip_segment_skips_hole()
    {
        var r = new Region2(Square(10), new[] { Polygon2.Rectangle(new Vec2(4, 4), 2, 2) });
        var parts = r.ClipSegment(new Segment2(new Vec2(-5, 5), new Vec2(15, 5)));
        Assert.Equal(2, parts.Count);
        Assert.Equal(8, parts.Sum(s => s.Length), 6);
    }

    [Fact]
    public void FromLoops_detects_holes()
    {
        var regions = Region2.FromLoops(new[] { Polygon2.Rectangle(new Vec2(4, 4), 2, 2), Square(10) });
        Assert.Single(regions);
        Assert.Single(regions[0].Holes);
    }

    [Fact]
    public void Frame_roundtrip()
    {
        var f = Frame3.FromXAndNormal(new Vec3(1, 2, 3), new Vec3(1, 1, 0), new Vec3(0, 0, 1));
        var w = f.ToWorld(new Vec3(2, -1, 0.5));
        var l = f.ToLocal(w);
        Assert.True(l.AlmostEquals(new Vec3(2, -1, 0.5), 1e-9));
    }

    [Fact]
    public void Rotate_around_axis()
    {
        var p = new Vec3(1, 0, 0).RotateAround(Vec3.UnitZ, Math.PI / 2);
        Assert.True(p.AlmostEquals(new Vec3(0, 1, 0), 1e-9));
    }
}
