using System.IO.Compression;
using TeraTools.Core.Export;
using TeraTools.Core.Mathx;
using TeraTools.Core.Serialization;
using TeraTools.Core.Settings;
using TeraTools.Core.Text;
using TeraTools.Core.Units;

namespace TeraTools.Core.Tests;

public class ExpressionTests
{
    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("-2 ^ 2", -4)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("sin(30)", 0.5)]
    [InlineData("max(1, 5, 3)", 5)]
    [InlineData("if(2 > 1, 10, 20)", 10)]
    [InlineData("if(2 < 1; 0,5; 1,5)", 1.5)]
    [InlineData("7 % 3", 1)]
    [InlineData("not 0", 1)]
    [InlineData("1 = 1 and 2 <> 3", 1)]
    [InlineData("round(12.34, 0.5)", 12.5)]
    [InlineData("clamp(15, 0, 10)", 10)]
    public void Evaluates_constant_expressions(string formula, double expected)
    {
        Assert.Equal(expected, Expression.Eval(formula), 9);
    }

    [Fact]
    public void Evaluates_variables_case_insensitive()
    {
        var e = Expression.Parse("Abertura * i + J");
        Assert.Contains("Abertura", e.Variables);
        var v = e.Evaluate(new Dictionary<string, double> { ["abertura"] = 10, ["I"] = 2, ["j"] = 1 });
        Assert.Equal(21, v, 9);
    }

    [Fact]
    public void If_short_circuits_division_by_zero()
    {
        Assert.Equal(1, Expression.Eval("if(x = 0, 1, 1 / x)", ("x", 0)), 9);
    }

    [Theory]
    [InlineData("1 +")]
    [InlineData("foo(1)")]
    [InlineData("(1")]
    [InlineData("1 $ 2")]
    public void Invalid_expressions_report_errors(string formula)
    {
        Assert.False(Expression.TryParse(formula, out _, out var err));
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public void Unknown_variable_throws_on_evaluate()
    {
        var e = Expression.Parse("a + 1");
        Assert.Throws<ExpressionException>(() => e.Evaluate());
    }
}

public class UnitsAndTextTests
{
    [Fact]
    public void Length_parsing_with_units_and_comma()
    {
        Assert.True(U.TryParseLength("60 cm", LengthUnit.Millimeters, out var f1));
        Assert.Equal(600, U.ToMm(f1), 6);
        Assert.True(U.TryParseLength("0,6m", LengthUnit.Millimeters, out var f2));
        Assert.Equal(600, U.ToMm(f2), 6);
        Assert.True(U.TryParseLength("600", LengthUnit.Millimeters, out var f3));
        Assert.Equal(600, U.ToMm(f3), 6);
        Assert.True(U.TryParseNumber("1.234,5", out var n));
        Assert.Equal(1234.5, n, 9);
    }

    [Fact]
    public void Numbering_sequences()
    {
        var s = new NumberingSequence { Prefix = "AMB-", Start = 1, Digits = 3 };
        Assert.Equal("AMB-001", s.Format(0));
        Assert.Equal("AMB-012", s.Format(11));
        var l = new NumberingSequence { Style = NumberingStyle.Letters };
        Assert.Equal("A", l.Format(0));
        Assert.Equal("AA", l.Format(26));
        Assert.Equal("XIV", TextUtil.ToRoman(14));
    }

    [Fact]
    public void Name_template()
    {
        var r = NameTemplate.Apply("{Nivel}-{Numero:000} {Nome} {X}", new Dictionary<string, object?> { ["nivel"] = "T", ["Numero"] = 7, ["Nome"] = "Sala" });
        Assert.Equal("T-007 Sala {X}", r);
    }

    [Fact]
    public void Natural_sort()
    {
        var list = new List<string> { "Sala 10", "Sala 2", "sala 1" };
        list.Sort(TextUtil.NaturalComparer);
        Assert.Equal(new[] { "sala 1", "Sala 2", "Sala 10" }, list);
    }

    [Fact]
    public void Rename_rule_title_case_keeps_prepositions()
    {
        var r = new RenameRule { Case = TextCase.Title };
        Assert.Equal("Sala de Estar", r.Apply("SALA DE ESTAR"));
    }
}

public class ExportAndSettingsTests
{
    private static string TempFile(string ext) => Path.Combine(Path.GetTempPath(), "tt_" + Guid.NewGuid().ToString("N") + ext);

    [Fact]
    public void Xlsx_is_a_valid_zip_with_expected_parts()
    {
        var t = new TableData("Mobiliário");
        t.AddColumn("Item").AddColumn("Qtd", ColumnKind.Integer).AddColumn("Valor", ColumnKind.Money);
        t.AddRow("Cadeira <A&B>", 4, 350.5);
        t.AddRow("Mesa", 1, 1200);
        var path = TempFile(".xlsx");
        XlsxWriter.Write(path, t, new TableData("Mobiliário"));
        try
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var names = zip.Entries.Select(e => e.FullName).ToList();
                Assert.Contains("xl/workbook.xml", names);
                Assert.Contains("xl/worksheets/sheet2.xml", names);
                string xml;
                using (var r = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open())) xml = r.ReadToEnd();
                Assert.Contains("Cadeira &lt;A&amp;B&gt;", xml);
                System.Xml.Linq.XDocument.Parse(xml);
                string wbXml;
                using (var wb = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open())) wbXml = wb.ReadToEnd();
                Assert.Contains("Mobiliário (2)", wbXml);
            }
        }
        finally
        {
            // No Windows o arquivo precisa estar fechado antes de ser apagado.
            File.Delete(path);
        }
    }

    [Fact]
    public void Csv_roundtrip_with_semicolon_and_quotes()
    {
        var t = new TableData("x");
        t.AddColumn("Nome").AddColumn("Área", ColumnKind.Number);
        t.AddRow("Sala; \"principal\"", 12.5);
        var csv = CsvWriter.ToCsv(t, ";", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        var rows = CsvWriter.Parse(csv);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Sala; \"principal\"", rows[1][0]);
        Assert.Equal("12,5", rows[1][1]);
    }

    [Fact]
    public void Preset_store_save_list_delete_and_last_used()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tt_presets_" + Guid.NewGuid().ToString("N"));
        var store = new PresetStore(dir);
        store.Save("tool.a", new Preset { Name = "Padrão 2", Json = "{\"A\":2}" });
        store.Save("tool.a", new Preset { Name = "Padrão 10", Json = "{\"A\":10}" });
        store.SetLastUsed("tool.a", "{\"A\":5}");
        var list = store.List("tool.a");
        Assert.Equal(new[] { "Padrão 2", "Padrão 10" }, list.Select(p => p.Name));
        Assert.Equal("{\"A\":5}", store.GetLastUsed("tool.a"));
        Assert.True(store.Delete("tool.a", "padrão 2"));
        Assert.Single(store.List("tool.a"));
        Directory.Delete(dir, true);
    }

    private sealed class Sample
    {
        public int A { get; set; } = 1;
        public string B { get; set; } = "x";
    }

    [Fact]
    public void Populate_object_keeps_unspecified_defaults()
    {
        var s = new Sample();
        Json.PopulateObject(s, "{\"a\": 5}");
        Assert.Equal(5, s.A);
        Assert.Equal("x", s.B);
    }

    [Fact]
    public void Project_settings_sections_roundtrip()
    {
        var ps = new ProjectSettings();
        ps.SetSection(ProjectSettings.SectionInfo, new ProjectInfoSection { ClientName = "Cliente" });
        var back = ProjectSettings.FromJson(ps.ToJson());
        Assert.Equal("Cliente", back.GetSection<ProjectInfoSection>(ProjectSettings.SectionInfo).ClientName);
    }

    [Fact]
    public void Json_diff_detects_changes()
    {
        var d = JsonDiff.Compare("{\"a\":1,\"b\":{\"c\":2}}", "{\"a\":1,\"b\":{\"c\":3},\"d\":true}");
        Assert.Equal(2, d.Count);
        Assert.Contains(d, x => x.Path == "$.b.c" && x.Kind == DiffKind.Changed);
        Assert.Contains(d, x => x.Path == "$.d" && x.Kind == DiffKind.Added);
    }

    [Fact]
    public void Global_settings_recent_and_favorites()
    {
        var g = new GlobalSettings { RecentMax = 2 };
        g.TouchRecent("a");
        g.TouchRecent("b");
        g.TouchRecent("a");
        g.TouchRecent("c");
        Assert.Equal(new[] { "c", "a" }, g.Recent);
        Assert.True(g.ToggleFavorite("x"));
        Assert.False(g.ToggleFavorite("X"));
        Assert.Empty(g.Favorites);
    }
}
