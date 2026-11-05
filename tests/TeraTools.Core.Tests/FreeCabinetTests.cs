using TeraTools.Core.Joinery;
using TeraTools.Core.Units;
using Xunit;

namespace TeraTools.Core.Tests;

public class FreeCabinetTests
{
    private static double Mm(double v) => U.Mm(v);

    [Fact]
    public void TodosOsModelosGeramPecas()
    {
        for (var i = 0; i < FreeDesign.Templates.Length; i++)
        {
            var d = FreeDesign.FromTemplate(i);
            var m = FreeBuilder.Build(d);
            Assert.True(m.Boards.Count >= 4, FreeDesign.Templates[i]);
            Assert.DoesNotContain(m.Warnings, w => w.Contains("dimensão nula"));
            Assert.All(m.Boards, b => Assert.True(b.Length > 0 && b.Width > 0 && b.Thickness > 0));
        }
    }

    [Fact]
    public void ColunasRespeitamMedidaFixaEDividemORestante()
    {
        var d = new FreeDesign { Width = Mm(1215), Thickness = Mm(15) };
        d.Root.SplitInto(FreeSplit.Columns, 3);
        d.Root.Children[0].Size = Mm(400);
        var rects = FreeBuilder.Layout(d).ToDictionary(r => r.Id);
        var c = d.Root.Children.Select(x => rects[x.Id]).ToArray();
        Assert.Equal(Mm(400), c[0].Width, 6);
        // interior 1215 - 2×15 = 1185; menos 2 divisórias = 1155; restante 755 em duas partes
        Assert.Equal(Mm(377.5), c[1].Width, 6);
        Assert.Equal(Mm(377.5), c[2].Width, 6);
        Assert.Equal(c[0].X1 + Mm(15), c[1].X0, 6);
    }

    [Fact]
    public void LinhasVaoDeCimaParaBaixo()
    {
        var d = new FreeDesign();
        d.Root.SplitInto(FreeSplit.Rows, 2);
        d.Root.Children[0].Size = Mm(400);
        var rects = FreeBuilder.Layout(d).ToDictionary(r => r.Id);
        var top = rects[d.Root.Children[0].Id];
        var (_, _, _, iz1) = FreeBuilder.Interior(d);
        Assert.Equal(iz1, top.Z1, 6);
        Assert.Equal(Mm(400), top.Height, 6);
    }

    [Fact]
    public void FrentesSobrepostasSeEncontramNoMeioDaDivisoria()
    {
        var d = new FreeDesign();
        d.Root.SplitInto(FreeSplit.Columns, 2);
        var rects = FreeBuilder.Layout(d).ToDictionary(r => r.Id);
        var a = rects[d.Root.Children[0].Id];
        var b = rects[d.Root.Children[1].Id];
        Assert.Equal(a.FX1, b.FX0, 9);
        Assert.Equal(a.X1 + d.Thickness / 2, a.FX1, 9);
        Assert.Equal(0, a.FX0, 9);
        Assert.Equal(d.Width, b.FX1, 9);
    }

    [Fact]
    public void PortaDuplaTemDuasFolhasComFolga()
    {
        var d = new FreeDesign { Width = Mm(1000), FrontGap = Mm(3) };
        d.Root.Door = FreeDoor.HingedDouble;
        var m = FreeBuilder.Build(d);
        var doors = m.Boards.Where(b => b.Role == PanelRole.Door).ToList();
        Assert.Equal(2, doors.Count);
        Assert.All(doors, x => Assert.Equal(Mm((1000 - 3 - 3) / 2.0), x.Placement.SizeX, 6));
        Assert.Contains(m.Hardware, h => h.Kind == HardwareKind.Hinge && h.Quantity == 2 * new JoineryRules().HingesFor(doors[0].Placement.SizeZ));
    }

    [Fact]
    public void PortaDeCorrerLargaTemTresFolhas()
    {
        var d = new FreeDesign { Width = Mm(2400) };
        d.Root.Door = FreeDoor.Sliding;
        var m = FreeBuilder.Build(d);
        Assert.Equal(3, m.Boards.Count(b => b.Role == PanelRole.Door));
        Assert.Contains(m.Hardware, h => h.Kind == HardwareKind.SlidingTrack);
    }

    [Fact]
    public void GavetaAparenteSobPortaViraInterna()
    {
        var d = new FreeDesign();
        d.Root.Door = FreeDoor.HingedLeft;
        d.Root.Drawers = 2;
        var m = FreeBuilder.Build(d);
        Assert.DoesNotContain(m.Boards, b => b.Role == PanelRole.DrawerFront);
        Assert.Equal(4, m.Boards.Count(b => b.Role == PanelRole.DrawerSide));
        Assert.Contains(m.Warnings, w => w.Contains("internas"));
    }

    [Fact]
    public void PortaDentroDeVaoComPortaEhIgnorada()
    {
        var d = new FreeDesign();
        d.Root.Door = FreeDoor.Sliding;
        d.Root.SplitInto(FreeSplit.Columns, 2);
        d.Root.Children[0].Door = FreeDoor.HingedLeft;
        var m = FreeBuilder.Build(d);
        Assert.DoesNotContain(m.Boards, b => b.Name.Contains("esquerda") && b.Role == PanelRole.Door);
        Assert.Contains(m.Warnings, w => w.Contains("porta interna"));
    }

    [Fact]
    public void PecasDaCaixaENaoSeSobrepoem()
    {
        var d = FreeDesign.FromTemplate(3); // roupeiro
        var m = FreeBuilder.Build(d);
        var solid = m.Boards.Where(b => b.Role is not (PanelRole.Back or PanelRole.DrawerBottom or PanelRole.Door or PanelRole.DrawerFront or PanelRole.Countertop or PanelRole.Plinth)).ToList();
        double Overlap(Board a, Board b)
        {
            var pa = a.Placement; var pb = b.Placement;
            double O(double a0, double a1, double b0, double b1) => Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));
            var oa = pa.Frame.Origin; var ob = pb.Frame.Origin;
            return O(oa.X, oa.X + pa.SizeX, ob.X, ob.X + pb.SizeX) * O(oa.Y, oa.Y + pa.SizeY, ob.Y, ob.Y + pb.SizeY) * O(oa.Z, oa.Z + pa.SizeZ, ob.Z, ob.Z + pb.SizeZ);
        }
        for (var i = 0; i < solid.Count; i++)
            for (var j = i + 1; j < solid.Count; j++)
                Assert.True(Overlap(solid[i], solid[j]) < 1e-9, $"{solid[i].Name} × {solid[j].Name}");
    }

    [Fact]
    public void DividirEJuntarMantemOConteudo()
    {
        var c = new FreeCell { Shelves = 3 };
        c.SplitInto(FreeSplit.Rows, 3);
        Assert.Equal(3, c.Children.Count);
        Assert.Equal(3, c.Children[0].Shelves);
        Assert.Equal(0, c.Shelves);
        c.Merge();
        Assert.True(c.IsLeaf);
    }
}

public class FreeCabinetSerializationTests
{
    [Fact]
    public void DesenhoSobreviveAGravacaoNoProjeto()
    {
        var d = FreeDesign.FromTemplate(3);
        d.Root.Children[1].Children[1].FrontMaterial = "Laca verde";
        var json = TeraTools.Core.Serialization.Json.Serialize(d, typeof(FreeDesign), indented: false);
        var back = TeraTools.Core.Serialization.Json.Deserialize<FreeDesign>(json)!;
        Assert.Equal(d.Root.All().Count(), back.Root.All().Count());
        Assert.Equal(d.Root.All().Select(c => c.Id), back.Root.All().Select(c => c.Id));
        Assert.Equal("Laca verde", back.Root.Children[1].Children[1].FrontMaterial);
        Assert.Equal(FreeBuilder.Build(d).Boards.Count, FreeBuilder.Build(back).Boards.Count);
        Assert.Equal(d.Root.Door, back.Root.Door);
    }
}
