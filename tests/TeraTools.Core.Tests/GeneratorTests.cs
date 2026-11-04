using TeraTools.Core.Facade;
using TeraTools.Core.Geometry;
using TeraTools.Core.Imaging;
using TeraTools.Core.Kinetic;
using TeraTools.Core.Patterns;
using TeraTools.Core.Structure;
using TeraTools.Core.Units;

namespace TeraTools.Core.Tests;

public class PatternTests
{
    private static Region2 Rect(double w, double h) => new(Polygon2.Rectangle(Vec2.Zero, w, h));

    [Fact]
    public void Rectangular_grid_fully_inside_has_expected_count()
    {
        var s = new PatternSettings
        {
            Layout = PatternLayout.Rectangular,
            SpacingX = 1, SpacingY = 1, ModuleWidth = 0.8, ModuleHeight = 0.8,
            Anchor = PatternAnchor.RegionCorner, OffsetX = 0.5, OffsetY = 0.5,
        };
        var cells = PatternGenerator.Generate(s, Rect(5, 4));
        Assert.Equal(20, cells.Count);
        Assert.All(cells, c => Assert.Equal(0.8, c.Width, 9));
    }

    [Fact]
    public void Gradient_scale_stays_within_range()
    {
        var s = new PatternSettings
        {
            Layout = PatternLayout.Hexagonal, Shape = ModuleShape.Hexagon,
            SpacingX = 0.5, SpacingY = 0.5, ModuleWidth = 0.4, ModuleHeight = 0.46,
            Gradient = GradientMode.Linear, ScaleMin = 0.2, ScaleMax = 1.0, Clip = ClipMode.CenterInside,
        };
        var cells = PatternGenerator.Generate(s, Rect(6, 3));
        Assert.NotEmpty(cells);
        Assert.All(cells, c => Assert.InRange(c.Width / 0.4, 0.2 - 1e-9, 1.0 + 1e-9));
        Assert.True(cells.Max(c => c.Width) > cells.Min(c => c.Width) * 2);
    }

    [Fact]
    public void Radial_ring_counts()
    {
        var s = new PatternSettings
        {
            Layout = PatternLayout.Radial, RadialRings = 3, InnerRadius = 0, RingSpacing = 1, ModulesPerRing = 8,
            ModuleWidth = 0.2, ModuleHeight = 0.2, Clip = ClipMode.CenterInside,
        };
        var cells = PatternGenerator.Generate(s, Rect(10, 10));
        Assert.Equal(1 + 8 + 8, cells.Count);
    }

    [Fact]
    public void Random_layout_respects_minimum_distance()
    {
        var s = new PatternSettings { Layout = PatternLayout.Random, SpacingX = 0.5, SpacingY = 0.5, ModuleWidth = 0.1, ModuleHeight = 0.1, Clip = ClipMode.CenterInside, Seed = 3 };
        var cells = PatternGenerator.Generate(s, Rect(4, 4));
        Assert.True(cells.Count > 20);
        for (var i = 0; i < cells.Count; i++)
            for (var j = i + 1; j < cells.Count; j++)
                Assert.True(cells[i].Center.DistanceTo(cells[j].Center) >= 0.5 - 1e-9);
        var again = PatternGenerator.Generate(s, Rect(4, 4));
        Assert.Equal(cells.Count, again.Count);
    }

    [Fact]
    public void Triangular_layout_alternates_orientation()
    {
        var s = new PatternSettings { Layout = PatternLayout.Triangular, Shape = ModuleShape.Triangle, SpacingX = 1, SpacingY = 0.866, ModuleWidth = 0.9, ModuleHeight = 0.78, Clip = ClipMode.FullyInside };
        var cells = PatternGenerator.Generate(s, Rect(5, 3));
        Assert.Contains(cells, c => c.Flipped);
        Assert.Contains(cells, c => !c.Flipped);
    }

    [Fact]
    public void Formula_gradient_and_density()
    {
        var s = new PatternSettings
        {
            SpacingX = 0.25, SpacingY = 0.25, ModuleWidth = 0.2, ModuleHeight = 0.2,
            Gradient = GradientMode.Formula, Formula = "u", ApplyToScale = false,
            ApplyToDensity = true, DensityMin = 0, DensityMax = 1, Clip = ClipMode.CenterInside,
        };
        var cells = PatternGenerator.Generate(s, Rect(5, 2));
        var left = cells.Count(c => c.Center.X < 2.5);
        var right = cells.Count(c => c.Center.X >= 2.5);
        Assert.True(right > left);
    }
}

public class ImageBlocksTests
{
    private static PixelImage Gradient(int w, int h)
    {
        var px = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var v = (byte)(255 * x / (w - 1));
                px[i] = v; px[i + 1] = v; px[i + 2] = v; px[i + 3] = 255;
            }
        return new PixelImage(w, h, px);
    }

    [Fact]
    public void Generates_grid_with_aspect_ratio_and_levels()
    {
        var img = Gradient(100, 50);
        var s = new ImageBlockSettings { Columns = 20, TotalWidth = U.M(2), HeightLevels = 5, MaxElements = 10000, ColorMode = BlockColorMode.AutoPalette, PaletteSize = 3 };
        var r = ImageToBlocks.Generate(img, s);
        Assert.Equal(20, r.Columns);
        Assert.Equal(10, r.Rows);
        Assert.Equal(200, r.Blocks.Count);
        Assert.InRange(r.Palette.Count, 1, 3);
        Assert.True(r.Blocks.Select(b => b.HeightLevel).Distinct().Count() <= 5);
        Assert.True(r.Blocks.First(b => b.Column == 19).Height > r.Blocks.First(b => b.Column == 0).Height);
    }

    [Fact]
    public void Reduces_resolution_to_respect_element_limit()
    {
        var img = Gradient(200, 200);
        var s = new ImageBlockSettings { Columns = 100, MaxElements = 400 };
        var r = ImageToBlocks.Generate(img, s);
        Assert.True(r.Blocks.Count <= 400);
        Assert.True(r.ResolutionReduced);
    }

    [Fact]
    public void Kmeans_is_deterministic()
    {
        var cols = Enumerable.Range(0, 300).Select(i => new Colors.RgbColor((byte)(i % 255), (byte)(i * 7 % 255), (byte)(i * 13 % 255))).ToList();
        var a = ImageToBlocks.KMeans(cols, 4, 9);
        var b = ImageToBlocks.KMeans(cols, 4, 9);
        Assert.Equal(a, b);
    }
}

public class BriseTests
{
    [Fact]
    public void Vertical_fins_by_spacing_centered()
    {
        var path = new PolylinePath(new[] { new Vec2(0, 0), new Vec2(10, 0) });
        var s = new BriseSettings { Spacing = 1, FinDepth = 0.3, FinThickness = 0.05, BottomElevation = 0, TopElevation = 3 };
        var r = BriseGenerator.Generate(s, path);
        Assert.Equal(11, r.Fins.Count);
        Assert.All(r.Fins, f => Assert.Equal(3, f.Length, 9));
        Assert.All(r.Fins, f => Assert.True(f.Box.IsUpright));
    }

    [Fact]
    public void Horizontal_fins_follow_polyline_spans()
    {
        var path = new PolylinePath(new[] { new Vec2(0, 0), new Vec2(4, 0), new Vec2(4, 3) });
        var s = new BriseSettings { Orientation = FinOrientation.Horizontal, Distribution = FinDistribution.ByCount, Count = 5, BottomElevation = 0.5, TopElevation = 2.5, FinDepth = 0.2, FinThickness = 0.03, RotationDeg = 30 };
        var r = BriseGenerator.Generate(s, path);
        Assert.Equal(10, r.Fins.Count);
        Assert.Equal(5 * 7, r.TotalLength, 6);
    }

    [Fact]
    public void Fins_are_clipped_by_facade_opening()
    {
        var path = new PolylinePath(new[] { new Vec2(0, 0), new Vec2(6, 0) });
        var facade = new Region2(Polygon2.Rectangle(Vec2.Zero, 6, 3), new[] { Polygon2.Rectangle(new Vec2(1.5, 1), 3, 1) });
        var s = new BriseSettings { Spacing = 1, BottomElevation = 0, TopElevation = 3 };
        var r = BriseGenerator.Generate(s, path, facade);
        // Aletas em x=2,3,4 atravessam a abertura (2 trechos cada); x=0,1,5,6 inteiras.
        Assert.Equal(4 + 3 * 2, r.Fins.Count);
    }

    [Fact]
    public void Sine_variation_changes_rotation()
    {
        var path = new PolylinePath(new[] { new Vec2(0, 0), new Vec2(10, 0) });
        var s = new BriseSettings { Spacing = 0.5, Variation = VariationMode.Sine, VariationMinDeg = -30, VariationMaxDeg = 30, SinePeriods = 1 };
        var r = BriseGenerator.Generate(s, path);
        Assert.InRange(r.Fins.Min(f => f.RotationDeg), -30.0001, -20);
        Assert.InRange(r.Fins.Max(f => f.RotationDeg), 20, 30.0001);
    }
}

public class SpaceFrameTests
{
    [Fact]
    public void Square_on_square_offset_counts()
    {
        var footprint = new Region2(Polygon2.Rectangle(new Vec2(-1.5, -1.5), 3, 3));
        var s = new SpaceFrameSettings { ModuleX = 1, ModuleY = 1, Depth = 0.7, OffsetX = 0.5, OffsetY = 0.5 };
        var r = SpaceFrameGenerator.Generate(s, footprint);
        var top = r.Nodes.Count(n => n.Layer == 0);
        var bottom = r.Nodes.Count(n => n.Layer == 1);
        Assert.Equal(16, top);
        Assert.Equal(9, bottom);
        Assert.Equal(24, r.Bars.Count(b => b.Role == BarRole.TopChord));
        Assert.Equal(12, r.Bars.Count(b => b.Role == BarRole.BottomChord));
        Assert.Equal(36, r.Bars.Count(b => b.Role == BarRole.Diagonal));
        var diag = Math.Sqrt(0.5 * 0.5 * 2 + 0.7 * 0.7);
        Assert.All(r.Bars.Where(b => b.Role == BarRole.Diagonal), b => Assert.Equal(diag, b.Length, 6));
        Assert.NotEmpty(r.BarSchedule().Rows);
    }

    [Fact]
    public void Aligned_type_has_verticals()
    {
        var footprint = new Region2(Polygon2.Rectangle(new Vec2(-1.01, -1.01), 2.02, 2.02));
        var s = new SpaceFrameSettings { Type = SpaceFrameType.SquareOnSquareAligned, ModuleX = 1, ModuleY = 1, Depth = 1, Diagonals = DiagonalPattern.Alternate };
        var r = SpaceFrameGenerator.Generate(s, footprint);
        Assert.Equal(9, r.Bars.Count(b => b.Role == BarRole.Vertical));
        Assert.Equal(12, r.Bars.Count(b => b.Role == BarRole.Diagonal));
    }

    [Fact]
    public void Triangular_type_bottom_nodes_have_three_diagonals()
    {
        var footprint = new Region2(Polygon2.Rectangle(new Vec2(-3, -3), 6, 6));
        var s = new SpaceFrameSettings { Type = SpaceFrameType.TriangularOffset, ModuleX = 1, Depth = 0.8 };
        var r = SpaceFrameGenerator.Generate(s, footprint);
        var bottomIds = r.Nodes.Where(n => n.Layer == 1).Select(n => n.Id).ToHashSet();
        Assert.NotEmpty(bottomIds);
        foreach (var id in bottomIds)
            Assert.Equal(3, r.Bars.Count(b => b.Role == BarRole.Diagonal && (b.A == id || b.B == id)));
    }
}

public class KineticTests
{
    [Fact]
    public void Closed_and_open_angles()
    {
        var s = new KineticSettings { Columns = 3, Rows = 2, OpenAngleDeg = 90 };
        var closed = KineticGenerator.Evaluate(s, Frame3.World, 0);
        var open = KineticGenerator.Evaluate(s, Frame3.World, 1);
        Assert.All(closed, p => Assert.Equal(0, p.AngleDeg, 9));
        Assert.All(open, p => Assert.Equal(90, p.AngleDeg, 9));
        Assert.Equal(6, open.Count);
    }

    [Fact]
    public void Hinge_edge_stays_fixed()
    {
        var s = new KineticSettings { Columns = 1, Rows = 1, Hinge = HingePosition.Start, Axis = HingeAxis.Vertical, OpenAngleDeg = 60, PanelWidth = 1, PanelHeight = 2, Thickness = 0.04, OffsetFromFacade = 0.2 };
        var p = KineticGenerator.Evaluate(s, Frame3.World, 1)[0];
        // Canto do plano médio na aresta da dobradiça (x=0) não se move.
        Assert.True(p.Corners[0].AlmostEquals(new Vec3(0, 0, 0.2), 1e-9));
        // A borda livre se afasta da fachada (z maior).
        Assert.True(p.Corners[1].Z > 0.2 + 0.5);
    }

    [Fact]
    public void Stagger_opens_progressively()
    {
        var s = new KineticSettings { Columns = 5, Rows = 1, Mode = KineticMode.Stagger, StaggerAmount = 0.5 };
        var mid = KineticGenerator.Evaluate(s, Frame3.World, 0.5);
        Assert.True(mid[0].OpeningFraction > mid[4].OpeningFraction);
        var full = KineticGenerator.Evaluate(s, Frame3.World, 1);
        Assert.All(full, p => Assert.Equal(1, p.OpeningFraction, 9));
    }
}
