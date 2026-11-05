using TeraTools.Core.Export;
using TeraTools.Core.Finishes;
using TeraTools.Core.Localization;
using TeraTools.Core.Tiling;
using Xunit;

namespace TeraTools.Core.Tests;

public class FinishCatalogTests
{
    private static string Csv(FinishCatalog c) => CsvWriter.ToCsv(c.ToTable(), ";", Loc.Culture);

    [Fact]
    public void ExemplosSaoValidos()
    {
        var c = FinishCatalog.Starter();
        Assert.NotEmpty(c.Items);
        Assert.Empty(c.Validate());
    }

    [Fact]
    public void PlanilhaIdaEVoltaPreservaOsItens()
    {
        var original = FinishCatalog.Starter();
        var copy = new FinishCatalog();
        var (added, updated, errors) = copy.ImportCsv(Csv(original));
        Assert.Empty(errors);
        Assert.Equal(original.Items.Count, added);
        Assert.Equal(0, updated);
        foreach (var o in original.Items)
        {
            var i = copy.Find(o.Code)!;
            Assert.Equal(o.Name, i.Name);
            Assert.Equal(o.Kind, i.Kind);
            Assert.Equal(o.Use, i.Use);
            Assert.Equal(o.ColorHex, i.ColorHex);
            Assert.Equal(o.LengthMm, i.LengthMm, 3);
            Assert.Equal(o.WidthMm, i.WidthMm, 3);
            Assert.Equal(o.ThicknessMm, i.ThicknessMm, 3);
            Assert.Equal(o.JointMm, i.JointMm, 3);
            Assert.Equal(o.Layout, i.Layout);
            Assert.Equal(o.Tones, i.Tones);
            Assert.Equal(o.LossPercent, i.LossPercent, 3);
            Assert.Equal(o.PriceUnit, i.PriceUnit);
        }
    }

    [Fact]
    public void ImportarAtualizaPeloCodigoEAceitaRotulosSemAcento()
    {
        var c = FinishCatalog.Starter();
        var csv = "Codigo;Nome;Tipo;Paginacao;Comprimento;Preco (R$)\n" +
                  "PI-01;Porcelanato polido 90x90;porcelanato;espinha de peixe;1.200;R$ 189,90\n" +
                  ";Piso novo;pedra;alinhada;400;\n";
        var (added, updated, errors) = c.ImportCsv(csv);
        Assert.Empty(errors);
        Assert.Equal(1, updated);
        Assert.Equal(1, added);
        var pi = c.Find("PI-01")!;
        Assert.Equal("Porcelanato polido 90x90", pi.Name);
        Assert.Equal(TileLayout.Herringbone, pi.Layout);
        Assert.Equal(189.90, pi.Price, 2);
        var novo = c.Find("Piso novo")!;
        Assert.Equal(FinishKind.NaturalStone, novo.Kind);
        Assert.Equal(FinishUse.Floor, novo.Use);
        Assert.StartsWith("PI-", novo.Code);
    }

    [Fact]
    public void CodigoRepetidoECorInvalidaSaoApontados()
    {
        var c = new FinishCatalog();
        c.Items.Add(new FinishItem { Code = "PI-01", Name = "A" });
        c.Items.Add(new FinishItem { Code = "pi-01", Name = "B", ColorHex = "azul" });
        var errors = c.Validate();
        Assert.Contains(errors, e => e.Contains("PI-01"));
        Assert.Contains(errors, e => e.Contains("azul"));
    }

    [Fact]
    public void TonalidadesSaoDistintasEEstaveis()
    {
        var item = new FinishItem { ColorHex = "#A47148", Tones = 4, ToneVariation = 12 };
        var a = item.ToneColors();
        var b = item.ToneColors();
        Assert.Equal(4, a.Count);
        Assert.Equal(a, b);
        Assert.Equal(4, a.Distinct().Count());
        Assert.Equal("#A47148", a[0].Hex);
    }

    [Fact]
    public void NovoCodigoUsaOPrefixoDaAplicacao()
    {
        var c = FinishCatalog.Starter();
        Assert.Equal("PI-05", c.NewCode(FinishUse.Floor));
        Assert.Equal("FO-02", c.NewCode(FinishUse.Ceiling));
    }
}
