using TeraTools.Core.Checks;
using TeraTools.Core.Costing;
using TeraTools.Core.Documentation;
using TeraTools.Core.Geometry;
using TeraTools.Core.Joinery;
using TeraTools.Core.Layout;
using TeraTools.Core.Nesting;
using TeraTools.Core.Reporting;
using TeraTools.Core.Tiling;
using TeraTools.Core.Units;

namespace TeraTools.Core.Tests;

public class TilingTests
{
    private static Region2 Room(double w, double h) => new(Polygon2.Rectangle(Vec2.Zero, w, h));

    [Fact]
    public void Grid_from_corner_without_joint_has_only_full_tiles()
    {
        var s = new TileSettings { TileLength = 0.6, TileWidth = 0.6, Joint = 0, Start = TileStart.Corner, LossPercent = 0 };
        var r = TileLayoutEngine.Generate(Room(3, 3), s);
        Assert.Equal(25, r.FullCount);
        Assert.Equal(0, r.CutCount);
        Assert.Equal(9, r.CoveredArea, 6);
        Assert.Equal(25, r.TilesToBuy);
    }

    [Fact]
    public void Center_start_produces_cuts_and_losses()
    {
        var s = new TileSettings { TileLength = 0.6, TileWidth = 0.6, Joint = 0.002, Start = TileStart.Center, LossPercent = 10, TilesPerBox = 4 };
        var r = TileLayoutEngine.Generate(Room(3.1, 2.7), s);
        Assert.True(r.CutCount > 0);
        Assert.InRange(r.CoveredArea, 3.1 * 2.7 * 0.98, 3.1 * 2.7);
        var b = r.BySize.Single();
        Assert.True(b.ToBuy >= b.Needed);
        Assert.Equal((int)Math.Ceiling(b.ToBuy / 4.0), b.Boxes);
    }

    [Theory]
    [InlineData(TileLayout.Herringbone)]
    [InlineData(TileLayout.Basketweave)]
    [InlineData(TileLayout.ModularThreeSizes)]
    [InlineData(TileLayout.ModularPythagorean)]
    [InlineData(TileLayout.RunningBond)]
    [InlineData(TileLayout.Diagonal)]
    public void Layouts_cover_room_without_gaps_when_joint_is_zero(TileLayout layout)
    {
        var s = new TileSettings { TileLength = 0.6, TileWidth = 0.3, Joint = 0, Layout = layout, Start = TileStart.Center };
        var room = Room(4, 3);
        var r = TileLayoutEngine.Generate(room, s);
        Assert.Equal(room.Area, r.CoveredArea, 4);
        // Sem sobreposição: soma das áreas recortadas = área do ambiente.
        Assert.True(r.Tiles.Count > 10);
    }

    [Fact]
    public void Optimize_start_does_not_increase_slivers()
    {
        var room = Room(3.05, 2.95);
        var s = new TileSettings { TileLength = 0.6, TileWidth = 0.6, Joint = 0.003, Start = TileStart.Corner, MinCutSize = 0.08 };
        var baseRes = TileLayoutEngine.Generate(room, s);
        var (_, best) = TileLayoutEngine.OptimizeStart(room, s, 6);
        Assert.True(best.Slivers.Count <= baseRes.Slivers.Count);
    }

    [Fact]
    public void Joint_lines_exist_for_parts_division()
    {
        var s = new TileSettings { TileLength = 0.6, TileWidth = 0.6, Joint = 0.002, Start = TileStart.Corner };
        var r = TileLayoutEngine.Generate(Room(1.8, 1.2), s);
        Assert.NotEmpty(r.JointLines);
    }

    [Fact]
    public void Offcut_pairing()
    {
        Assert.Equal(2, TileLayoutEngine.PairOffcuts(new List<double> { 0.4, 0.5, 0.3, 0.6 }));
        Assert.Equal(3, TileLayoutEngine.PairOffcuts(new List<double> { 0.9, 0.8, 0.7 }));
    }
}

public class JoineryTests
{
    [Fact]
    public void Base_cabinet_boards_and_hardware()
    {
        var s = CabinetSpec.Defaults(CabinetKind.Base);
        s.Width = U.Mm(800);
        var m = CabinetBuilder.Build(s);
        Assert.Equal(2, m.Boards.Count(b => b.Role == PanelRole.Side));
        Assert.Single(m.Boards, b => b.Role == PanelRole.Bottom);
        Assert.Equal(2, m.Boards.Count(b => b.Role == PanelRole.Rail));
        Assert.Single(m.Boards, b => b.Role == PanelRole.Back);
        Assert.Single(m.Boards, b => b.Role == PanelRole.Shelf);
        Assert.Equal(2, m.Boards.Count(b => b.Role == PanelRole.Door));
        Assert.Single(m.Boards, b => b.Role == PanelRole.Plinth);
        Assert.Equal(4, m.Hardware.Where(h => h.Kind == HardwareKind.Hinge).Sum(h => h.Quantity));
        Assert.Equal(2, m.Hardware.Where(h => h.Kind == HardwareKind.Handle).Sum(h => h.Quantity));

        var side = m.Boards.First(b => b.Role == PanelRole.Side);
        Assert.Equal(870 - 100, U.ToMm(side.Length), 6);
        Assert.Equal(550, U.ToMm(side.Width), 6);
        Assert.True(side.EdgeL1);

        var door = m.Boards.First(b => b.Role == PanelRole.Door);
        Assert.True(door.EdgeL1 && door.EdgeL2 && door.EdgeC1 && door.EdgeC2);
        Assert.Equal((800 - 2 * 3) / 2.0, U.ToMm(door.Width), 6);
    }

    [Fact]
    public void Drawer_unit_has_boxes_and_slides()
    {
        var s = CabinetSpec.Defaults(CabinetKind.Base);
        s.Doors = DoorKind.None;
        s.Drawers = 3;
        var m = CabinetBuilder.Build(s);
        Assert.Equal(3, m.Boards.Count(b => b.Role == PanelRole.DrawerFront));
        Assert.Equal(6, m.Boards.Count(b => b.Role == PanelRole.DrawerSide));
        Assert.Equal(3, m.Hardware.Where(h => h.Kind == HardwareKind.Slide).Sum(h => h.Quantity));
        var slide = m.Hardware.First(h => h.Kind == HardwareKind.Slide);
        Assert.Equal(500, U.ToMm(slide.Length), 6);
    }

    [Fact]
    public void Combo_doors_and_drawer_with_fixed_shelf()
    {
        var s = CabinetSpec.Defaults(CabinetKind.Base);
        s.Drawers = 1;
        var m = CabinetBuilder.Build(s);
        Assert.Single(m.Boards, b => b.Role == PanelRole.DrawerFront);
        Assert.Single(m.Boards, b => b.Role == PanelRole.FixedShelf);
        Assert.Equal(2, m.Boards.Count(b => b.Role == PanelRole.Door));
        var front = m.Boards.First(b => b.Role == PanelRole.DrawerFront).Placement;
        var door = m.Boards.First(b => b.Role == PanelRole.Door).Placement;
        Assert.True(front.Frame.Origin.Z > door.Frame.Origin.Z + door.SizeZ);
    }

    [Fact]
    public void Wardrobe_has_sliding_doors_and_hanging_rail()
    {
        var m = CabinetBuilder.Build(CabinetSpec.Defaults(CabinetKind.Wardrobe));
        Assert.Equal(2, m.Boards.Count(b => b.Role == PanelRole.Door));
        Assert.Contains(m.Hardware, h => h.Kind == HardwareKind.HangingRail);
        Assert.Contains(m.Hardware, h => h.Kind == HardwareKind.SlidingTrack);
    }

    [Theory]
    [InlineData(CabinetKind.Wall)]
    [InlineData(CabinetKind.Tall)]
    [InlineData(CabinetKind.OpenNiche)]
    [InlineData(CabinetKind.ClosedNiche)]
    [InlineData(CabinetKind.Vanity)]
    [InlineData(CabinetKind.Counter)]
    [InlineData(CabinetKind.Island)]
    [InlineData(CabinetKind.TvPanel)]
    [InlineData(CabinetKind.Bookcase)]
    public void All_kinds_build_valid_boards(CabinetKind kind)
    {
        var m = CabinetBuilder.Build(CabinetSpec.Defaults(kind));
        Assert.NotEmpty(m.Boards);
        Assert.All(m.Boards, b => Assert.True(b.Length > 0 && b.Width > 0 && b.Thickness > 0));
        Assert.All(m.Boards, b => Assert.True(b.Length >= b.Width || b.HasGrain));
    }

    [Fact]
    public void Too_small_cabinet_throws()
    {
        var s = CabinetSpec.Defaults(CabinetKind.Base);
        s.Width = U.Mm(50);
        Assert.Throws<ArgumentException>(() => CabinetBuilder.Build(s));
    }

    [Fact]
    public void Cut_list_aggregates_identical_boards()
    {
        var spec = CabinetSpec.Defaults(CabinetKind.Base);
        spec.Width = U.Mm(900); // base (870) diferente das laterais (770)
        var m = CabinetBuilder.Build(spec);
        var rows = CutList.Aggregate(m.Boards.Select(b => ("M1", b)));
        var sideRow = rows.First(r => r.Names.Any(n => n.StartsWith("Lateral")));
        Assert.Equal(2, sideRow.Quantity);
        Assert.NotEmpty(CutList.FabricationExport(rows).Rows);
        Assert.NotEmpty(CutList.Hardware(m.Hardware.Select(h => ("M1", h))).Rows);
    }

    [Fact]
    public void Composition_split_equal_and_fit()
    {
        var r = Composition.SplitEqual(U.Mm(2400), U.Mm(300), U.Mm(900), U.Mm(600));
        Assert.True(r.Success);
        Assert.Equal(4, r.Widths.Count);
        var fit = Composition.Fit(U.Mm(3000), new[]
        {
            new ModuleSlot { Name = "Pia", FixedWidth = U.Mm(1200) },
            new ModuleSlot { Name = "A", PreferredWidth = U.Mm(600), MinWidth = U.Mm(400), MaxWidth = U.Mm(800) },
            new ModuleSlot { Name = "B", PreferredWidth = U.Mm(600), MinWidth = U.Mm(400), MaxWidth = U.Mm(800) },
        }, U.Mm(100));
        // Flexíveis travam no máximo (800): sobram 200 mm, acima do complemento máximo de 100 mm.
        Assert.False(fit.Success);
        var fit2 = Composition.Fit(U.Mm(3000), new[]
        {
            new ModuleSlot { Name = "Pia", FixedWidth = U.Mm(1200) },
            new ModuleSlot { Name = "A", PreferredWidth = U.Mm(600), MinWidth = U.Mm(400), MaxWidth = U.Mm(800) },
            new ModuleSlot { Name = "B", PreferredWidth = U.Mm(600), MinWidth = U.Mm(400), MaxWidth = U.Mm(800) },
        }, U.Mm(300));
        Assert.True(fit2.Success);
        Assert.Equal(200, U.ToMm(fit2.Filler), 6);
        Assert.Equal(3000, U.ToMm(fit2.Widths.Sum() + fit2.Filler), 6);
    }
}

public class NestingTests
{
    private static void AssertValid(NestingResult r, NestingSettings s)
    {
        foreach (var sheet in r.Sheets)
        {
            foreach (var p in sheet.Parts)
            {
                Assert.True(p.X >= s.Trim - 1e-9);
                Assert.True(p.Y >= s.Trim - 1e-9);
                Assert.True(p.X + p.SizeX <= sheet.Sheet.Length - s.Trim + 1e-9);
                Assert.True(p.Y + p.SizeY <= sheet.Sheet.Width - s.Trim + 1e-9);
            }
            for (var i = 0; i < sheet.Parts.Count; i++)
                for (var j = i + 1; j < sheet.Parts.Count; j++)
                {
                    var a = sheet.Parts[i].Rect.Inflate(s.Kerf / 2 - 1e-9);
                    var b = sheet.Parts[j].Rect.Inflate(s.Kerf / 2 - 1e-9);
                    Assert.False(a.Intersects(b), $"Sobreposição na chapa {sheet.Index}");
                }
        }
    }

    [Theory]
    [InlineData(NestingMethod.Guillotine)]
    [InlineData(NestingMethod.MaxRects)]
    public void Places_all_parts_without_overlap(NestingMethod method)
    {
        var parts = new List<PartRequest>
        {
            new() { Id = "A", Material = "MDF", Thickness = U.Mm(15), Length = U.Mm(800), Width = U.Mm(550), Quantity = 6 },
            new() { Id = "B", Material = "MDF", Thickness = U.Mm(15), Length = U.Mm(770), Width = U.Mm(400), Quantity = 8 },
            new() { Id = "C", Material = "MDF", Thickness = U.Mm(15), Length = U.Mm(1200), Width = U.Mm(300), Quantity = 4 },
            new() { Id = "D", Material = "Freijó", Thickness = U.Mm(18), Length = U.Mm(700), Width = U.Mm(397), Quantity = 4, HasGrain = true },
        };
        var sheets = new List<SheetSpec> { new() { Material = "Freijó", Thickness = U.Mm(18), HasGrain = true } };
        var s = new NestingSettings { Method = method };
        var r = Nester.Nest(parts, sheets, s);
        Assert.Empty(r.Unplaced);
        Assert.Equal(22, r.Sheets.Sum(x => x.Parts.Count));
        AssertValid(r, s);
        Assert.All(r.Sheets.Where(x => x.Sheet.Material == "Freijó").SelectMany(x => x.Parts), p => Assert.False(p.Rotated));
        Assert.InRange(r.Utilization, 0.3, 1.0);
        Assert.NotEmpty(r.Summary().Rows);
    }

    [Fact]
    public void Oversized_part_is_reported()
    {
        var parts = new[] { new PartRequest { Id = "X", Material = "MDF", Thickness = U.Mm(15), Length = U.Mm(3000), Width = U.Mm(2000) } };
        var r = Nester.Nest(parts, Array.Empty<SheetSpec>(), new NestingSettings());
        Assert.Single(r.Unplaced);
    }

    [Fact]
    public void Guillotine_records_cuts()
    {
        var parts = new[] { new PartRequest { Id = "A", Material = "MDF", Thickness = U.Mm(15), Length = U.Mm(500), Width = U.Mm(300), Quantity = 5 } };
        var r = Nester.Nest(parts, Array.Empty<SheetSpec>(), new NestingSettings { Method = NestingMethod.Guillotine });
        Assert.NotEmpty(r.Sheets[0].Cuts);
    }
}

public class CostAndChecksTests
{
    [Fact]
    public void Budget_matches_by_code_then_family_with_losses()
    {
        var table = new PriceTable
        {
            Items =
            {
                new PriceItem { Code = "CAD-01", Description = "Cadeira", UnitCost = 300, MatchBy = PriceMatch.Code },
                new PriceItem { Code = "POR", Pattern = "Porcelanato*", Description = "Porcelanato", Unit = "m²", UnitCost = 100, LossPercent = 10, MatchBy = PriceMatch.Material },
            },
        };
        var recs = new[]
        {
            new QuantityRecord { Room = "Sala", Category = "Mobiliário", Code = "CAD-01", Quantity = 4 },
            new QuantityRecord { Room = "Sala", Category = "Pisos", Material = "Porcelanato Cinza", Unit = "m²", Quantity = 20 },
            new QuantityRecord { Room = "Sala", Category = "Outros", Code = "???", Quantity = 1 },
        };
        var b = BudgetCalculator.Calculate(recs, table);
        Assert.Equal(1200 + 2200, b.Total, 6);
        Assert.Single(b.Unpriced);
        Assert.NotEmpty(b.ByRoom().Rows);
    }

    [Fact]
    public void Price_table_from_csv()
    {
        var csv = "Codigo;Descricao;Unidade;CustoUnitario;Perda;Categoria;CasarPor;Padrao\nPS-01;Porcelanato;m²;R$ 89,90;10%;Pisos;material;Porcelanato*\n";
        var t = PriceTable.FromCsv(csv);
        Assert.Single(t.Items);
        Assert.Equal(89.90, t.Items[0].UnitCost, 6);
        Assert.Equal(PriceMatch.Material, t.Items[0].MatchBy);
    }

    [Fact]
    public void Rules_required_unique_range()
    {
        var recs = new List<ElementRecord>
        {
            new() { Id = 1, Category = "OST_Rooms", CategoryName = "Ambientes", Values = { ["ROOM_NAME"] = "Sala", ["ROOM_NUMBER"] = "1" }, Present = { "ROOM_NAME", "ROOM_NUMBER" } },
            new() { Id = 2, Category = "OST_Rooms", CategoryName = "Ambientes", Values = { ["ROOM_NAME"] = "", ["ROOM_NUMBER"] = "1" }, Present = { "ROOM_NAME", "ROOM_NUMBER" } },
            new() { Id = 3, Category = "OST_Doors", CategoryName = "Portas", Values = { ["ALL_MODEL_MARK"] = "P1" }, TypeNumbers = { ["DOOR_HEIGHT"] = 2100 }, Present = { "ALL_MODEL_MARK" } },
            new() { Id = 4, Category = "OST_Doors", CategoryName = "Portas", Values = { ["ALL_MODEL_MARK"] = "P2" }, TypeNumbers = { ["DOOR_HEIGHT"] = 1900 }, Present = { "ALL_MODEL_MARK" } },
        };
        var issues = RuleEngine.Run(RuleSet.Default(), recs);
        Assert.Contains(issues, i => i.ElementId == 2 && i.Rule.Parameter == "ROOM_NAME");
        Assert.Equal(2, issues.Count(i => i.Rule.Kind == RuleKind.Unique));
        Assert.Contains(issues, i => i.ElementId == 4 && i.Rule.Kind == RuleKind.Range);
        Assert.DoesNotContain(issues, i => i.ElementId == 3 && i.Rule.Kind == RuleKind.Range);
    }

    [Fact]
    public void Similar_names_ignore_different_numbers()
    {
        var sim = RuleEngine.SimilarNames(new[] { "Porta 80", "Porta  80 ", "Porta 90", "Sala 1", "Sala 2" });
        Assert.Single(sim);
    }

    [Fact]
    public void Turning_space_found_in_empty_room_and_not_in_blocked_room()
    {
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, 2, 2));
        var (found, _, _) = ErgonomicChecker.FindTurningSpace(room, Array.Empty<Footprint>(), 1.5, 0.05);
        Assert.True(found);
        var block = new Footprint { Id = 1, Outline = Polygon2.Rectangle(new Vec2(0.8, 0.8), 0.4, 0.4) };
        var (found2, _, _) = ErgonomicChecker.FindTurningSpace(room, new[] { block }, 1.5, 0.05);
        Assert.False(found2);
    }

    [Fact]
    public void Circulation_flags_narrow_gap()
    {
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, 5, 5));
        var a = new Footprint { Id = 1, Name = "Sofá", Outline = Polygon2.Rectangle(new Vec2(1, 1), 1, 1) };
        var b = new Footprint { Id = 2, Name = "Mesa", Outline = Polygon2.Rectangle(new Vec2(2.5, 1), 1, 1) };
        var issues = ErgonomicChecker.CheckCirculation("Sala", room, new[] { a, b }, new ErgonomicRules());
        Assert.Contains(issues, i => i.ElementIds.Contains(1) && i.ElementIds.Contains(2) && i.Severity == Severity.Warning);
    }
}

public class LayoutAndDocsTests
{
    [Fact]
    public void Lighting_grid_fit_evenly()
    {
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, 4.8, 3.6));
        var pts = GridLayout.Generate(room, new GridLayoutSettings { SpacingX = 1.2, SpacingY = 1.2, AutoOrient = false, MinWallDistance = 0.1 }, out _);
        Assert.Equal(4 * 3, pts.Count);
        Assert.Contains(pts, p => p.AlmostEquals(new Vec2(0.6, 0.6), 1e-9));
    }

    [Fact]
    public void Lumen_method()
    {
        Assert.Equal(9, LumenMethod.Count(300, 20, 1000, 0.6, 0.8) - 4);
        Assert.Equal(300, LumenMethod.Illuminance(25, 20, 1000, 0.6, 0.4), 6);
    }

    [Fact]
    public void Sheet_packer_spills_to_next_sheet()
    {
        var area = new Rect2(0, 0, 1, 1);
        var views = Enumerable.Range(0, 5).Select(i => new ViewBox { Key = "v" + i, Width = 0.45, Height = 0.45 });
        var packed = SheetPacker.Pack(views, area, 0.02);
        Assert.Equal(2, packed.Select(p => p.Sheet).Distinct().Count());
    }
}
