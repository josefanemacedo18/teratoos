using TeraTools.Core.Joinery;
using TeraTools.Core.Units;
using Xunit;
using Xunit.Abstractions;

namespace TeraTools.Core.Tests;

/// <summary>Nenhum móvel gerado pode ter peças ocupando o mesmo espaço (fora dos encaixes previstos).</summary>
public class JoineryOverlapTests
{
    private readonly ITestOutputHelper _out;
    public JoineryOverlapTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Modules()
    {
        foreach (var k in Enum.GetValues<CabinetKind>())
        {
            yield return new object[] { k.ToString(), CabinetSpec.Defaults(k) };
            // variações: gavetas em cima e em baixo com portas, porta de correr, basculante, tampo
            var a = CabinetSpec.Defaults(k); a.Drawers = 2; a.DrawerZone = DrawerZone.Top; a.Doors = DoorKind.Hinged; a.Dividers = 1;
            yield return new object[] { k + "+gavetas-topo", a };
            var b = CabinetSpec.Defaults(k); b.Drawers = 2; b.DrawerZone = DrawerZone.Bottom; b.Doors = DoorKind.HingedDouble;
            yield return new object[] { k + "+gavetas-base", b };
            var c = CabinetSpec.Defaults(k); c.Doors = DoorKind.Sliding; c.HasCountertop = true;
            yield return new object[] { k + "+correr", c };
            var d = CabinetSpec.Defaults(k); d.Doors = DoorKind.FlipUp; d.Drawers = 0;
            yield return new object[] { k + "+basculante", d };
            var e = CabinetSpec.Defaults(k); e.Drawers = 3; e.Doors = DoorKind.None; e.Dividers = 2;
            yield return new object[] { k + "+so-gavetas", e };
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ModulosSemSobreposicao(string name, CabinetSpec spec)
    {
        CabinetModel m;
        try
        {
            m = CabinetBuilder.Build(spec);
        }
        catch (ArgumentException)
        {
            return; // combinação inválida para o tipo (ex.: medidas pequenas): não é sobreposição
        }
        var o = JoineryCheck.Overlaps(m);
        foreach (var x in o) _out.WriteLine(name + ": " + x.Describe());
        Assert.Empty(o);
    }

    public static IEnumerable<object[]> FreeDesigns()
    {
        for (var i = 0; i < FreeDesign.Templates.Length; i++)
        {
            yield return new object[] { FreeDesign.Templates[i], FreeDesign.FromTemplate(i) };
            var inset = FreeDesign.FromTemplate(i);
            inset.OverlayFronts = false;
            yield return new object[] { FreeDesign.Templates[i] + " (embutidas)", inset };
        }
        // casos mistos: gavetas aparentes ao lado de porta, gavetas internas sob porta, vidro, camarão
        var d = new FreeDesign { Width = U.Mm(1600), Height = U.Mm(900) };
        d.Root.SplitInto(FreeSplit.Columns, 3);
        d.Root.Children[0].Drawers = 4;
        d.Root.Children[1].Door = FreeDoor.Bifold;
        d.Root.Children[1].Shelves = 2;
        d.Root.Children[2].Door = FreeDoor.HingedRight;
        d.Root.Children[2].Drawers = 2;
        d.Root.Children[2].DrawerKind = FreeDrawerKind.Internal;
        d.Root.Children[2].Glass = true;
        yield return new object[] { "misto", d };
    }

    [Theory]
    [MemberData(nameof(FreeDesigns))]
    public void MoveisLivresSemSobreposicao(string name, FreeDesign design)
    {
        var m = FreeBuilder.Build(design);
        var o = JoineryCheck.Overlaps(m);
        foreach (var x in o) _out.WriteLine(name + ": " + x.Describe());
        Assert.Empty(o);
    }
}

public class HandleTests
{
    [Fact]
    public void PortaComBarraTemPuxadorModelado()
    {
        var d = new FreeDesign { Width = U.Mm(800), Height = U.Mm(870), Handle = HandleKind.Bar, HandleLength = U.Mm(160) };
        d.Root.Door = FreeDoor.HingedLeft;
        var m = FreeBuilder.Build(d);
        Assert.Equal(3, m.Accessories.Count); // barra + 2 apoios
        var door = m.Boards.Single(b => b.Role == PanelRole.Door);
        var bar = m.Accessories.OrderByDescending(a => a.Placement.SizeZ).First();
        // vertical, perto da borda direita (dobradiça à esquerda), na frente da porta
        Assert.Equal(U.Mm(160), bar.Placement.SizeZ, 6);
        Assert.True(bar.Placement.Frame.Origin.X > door.Placement.Frame.Origin.X + door.Placement.SizeX / 2);
        Assert.True(bar.Placement.Frame.Origin.Y + bar.Placement.SizeY <= door.Placement.Frame.Origin.Y + 1e-9);
    }

    [Fact]
    public void GavetaTemPuxadorHorizontalEFechoToqueNaoTemPeca()
    {
        var d = new FreeDesign { Handle = HandleKind.Bar };
        d.Root.Drawers = 3;
        var m = FreeBuilder.Build(d);
        Assert.Equal(9, m.Accessories.Count);
        Assert.All(m.Accessories.Where(a => a.Placement.SizeX > U.Mm(50)), a => Assert.True(a.Placement.SizeX > a.Placement.SizeZ));
        d.Handle = HandleKind.PushOpen;
        Assert.Empty(FreeBuilder.Build(d).Accessories);
    }

    [Fact]
    public void ModulosTambemTemPuxadores()
    {
        var m = CabinetBuilder.Build(CabinetSpec.Defaults(CabinetKind.Base));
        Assert.NotEmpty(m.Accessories);
    }
}
