using TeraTools.Core.Checks;
using TeraTools.Core.Geometry;
using TeraTools.Core.Units;
using Xunit;

namespace TeraTools.Core.Tests;

/// <summary>Verificações de referência da NBR 9050:2020 (valores editáveis).</summary>
public class AccessibilityTests
{
    private static readonly ErgonomicRules R = new();

    private static Footprint Box(long id, string name, double x, double y, double w, double d, Vec2? facing = null) => new()
    {
        Id = id,
        Name = name,
        Outline = Polygon2.Rectangle(new Vec2(x, y), w, d),
        Facing = facing ?? Vec2.UnitY,
    };

    [Fact]
    public void ValoresPadraoSaoOsDaNbr9050()
    {
        Assert.Equal(U.M(0.90), R.MinCirculation, 9);
        Assert.Equal(U.M(1.50), R.TurningDiameter, 9);
        Assert.Equal(U.M(0.80), R.MinDoorWidth, 9);
        Assert.Equal(U.M(0.78), R.LavatoryMinHeight, 9);
        Assert.Equal(U.M(0.80), R.LavatoryMaxHeight, 9);
        Assert.Contains("NBR 9050", R.ReferenceNote);
        Assert.Contains("não declara conformidade", R.ReferenceNote);
    }

    [Fact]
    public void CamaEncostadaNasDuasLateraisSemFaixaLivreGeraAviso()
    {
        // quarto de 1,60 m de largura com cama de casal de 1,40 m: sobra 0,10 m de cada lado
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, U.M(1.60), U.M(3)));
        var bed = Box(1, "Cama casal", U.M(0.10), 0, U.M(1.40), U.M(2.00));
        var issues = ErgonomicChecker.CheckBeds("Quarto", room, new[] { bed }, new[] { bed }, R);
        Assert.Single(issues);
    }

    [Fact]
    public void CamaComFaixaLivreDeUmLadoPassa()
    {
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, U.M(3), U.M(3)));
        var bed = Box(1, "Cama casal", 0, 0, U.M(1.40), U.M(2.00));
        Assert.Empty(ErgonomicChecker.CheckBeds("Quarto", room, new[] { bed }, new[] { bed }, R));
    }

    [Fact]
    public void CamaComMovelBloqueandoOLadoLivreGeraAviso()
    {
        var room = new Region2(Polygon2.Rectangle(Vec2.Zero, U.M(2.40), U.M(3)));
        var bed = Box(1, "Cama", 0, 0, U.M(1.40), U.M(2.00));
        var chest = Box(2, "Cômoda", U.M(1.60), U.M(0.5), U.M(0.5), U.M(0.5));
        Assert.Single(ErgonomicChecker.CheckBeds("Quarto", room, new[] { bed, chest }, new[] { bed }, R));
    }

    [Fact]
    public void TomadasEInterruptoresForaDaFaixa()
    {
        var pts = new[]
        {
            new ControlPoint(1, "Tomada baixa", false, U.M(0.30)),
            new ControlPoint(2, "Tomada ok", false, U.M(0.40)),
            new ControlPoint(3, "Interruptor alto", true, U.M(1.30)),
            new ControlPoint(4, "Interruptor ok", true, U.M(1.00)),
        };
        var issues = ErgonomicChecker.CheckControls(pts, R);
        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.ElementIds.Contains(1));
        Assert.Contains(issues, i => i.ElementIds.Contains(3));
    }

    [Fact]
    public void PortaEstreitaGeraAviso()
    {
        var doors = new[] { (1L, "P60", U.M(0.60), Vec2.Zero), (2L, "P80", U.M(0.80), Vec2.Zero) };
        var issues = ErgonomicChecker.CheckDoorWidths(doors, R);
        Assert.Single(issues);
        Assert.Contains(1L, issues[0].ElementIds);
    }

    [Fact]
    public void LavatorioForaDaFaixa()
    {
        var lav = Box(1, "Lavatório", 0, 0, U.M(0.5), U.M(0.4));
        lav.TopZ = U.M(0.86);
        Assert.Single(ErgonomicChecker.CheckLavatories(new[] { lav }, 0, R));
        lav.TopZ = U.M(0.79);
        Assert.Empty(ErgonomicChecker.CheckLavatories(new[] { lav }, 0, R));
    }
}
