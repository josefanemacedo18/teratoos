using System.Globalization;
using System.Text.RegularExpressions;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;

namespace TeraTools.Core.Costing;

public enum PriceMatch
{
    /// <summary>Código do elemento (parâmetro TT_Codigo / Marca de tipo).</summary>
    Code,
    /// <summary>"Família : Tipo".</summary>
    FamilyType,
    /// <summary>Nome do material.</summary>
    Material,
    /// <summary>Categoria.</summary>
    Category,
}

/// <summary>Item da tabela de preços (composição de custo).</summary>
public sealed class PriceItem
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "un";
    public double UnitCost { get; set; }
    public double LossPercent { get; set; }
    public string Category { get; set; } = "";
    public string? Supplier { get; set; }
    public PriceMatch MatchBy { get; set; } = PriceMatch.Code;

    /// <summary>Texto ou padrão com curinga (*) para casar com o elemento.</summary>
    public string Pattern { get; set; } = "";

    /// <summary>Custo de mão de obra por unidade (opcional).</summary>
    public double LaborCost { get; set; }

    public bool Matches(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var pat = string.IsNullOrWhiteSpace(Pattern) ? Code : Pattern;
        if (string.IsNullOrWhiteSpace(pat)) return false;
        if (!pat.Contains('*')) return string.Equals(pat.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
        var rx = "^" + Regex.Escape(pat.Trim()).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value.Trim(), rx, RegexOptions.IgnoreCase);
    }
}

public sealed class PriceTable
{
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "BRL";
    public DateTime Date { get; set; } = DateTime.Today;
    public List<PriceItem> Items { get; set; } = new();

    /// <summary>
    /// Importa de CSV com cabeçalho: Codigo;Descricao;Unidade;CustoUnitario;Perda;Categoria;CasarPor;Padrao;MaoDeObra;Fornecedor.
    /// </summary>
    public static PriceTable FromCsv(string text, string name = "")
    {
        var rows = CsvWriter.Parse(text);
        var t = new PriceTable { Name = name };
        if (rows.Count < 2) return t;
        var header = rows[0].Select(h => Text.TextUtil.NormalizeForCompare(h)).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                var i = header.IndexOf(n);
                if (i >= 0) return i;
            }
            return -1;
        }
        int cCode = Col("codigo", "code"), cDesc = Col("descricao", "description"), cUnit = Col("unidade", "unit"),
            cCost = Col("custounitario", "custo unitario", "custo", "unitcost", "unit cost", "preco", "price"),
            cLoss = Col("perda", "perdas", "loss"), cCat = Col("categoria", "category"),
            cMatch = Col("casarpor", "casar por", "matchby", "match"), cPat = Col("padrao", "pattern"),
            cLabor = Col("maodeobra", "mao de obra", "labor"), cSup = Col("fornecedor", "supplier");
        string Get(string[] r, int i) => i >= 0 && i < r.Length ? r[i].Trim() : "";
        double Num(string s) => U.TryParseNumber(s.Replace("R$", "").Replace("%", "").Trim(), out var v) ? v : 0;
        foreach (var r in rows.Skip(1))
        {
            var item = new PriceItem
            {
                Code = Get(r, cCode),
                Description = Get(r, cDesc),
                Unit = string.IsNullOrEmpty(Get(r, cUnit)) ? "un" : Get(r, cUnit),
                UnitCost = Num(Get(r, cCost)),
                LossPercent = Num(Get(r, cLoss)),
                Category = Get(r, cCat),
                Pattern = Get(r, cPat),
                LaborCost = Num(Get(r, cLabor)),
                Supplier = Get(r, cSup),
            };
            var m = Text.TextUtil.NormalizeForCompare(Get(r, cMatch));
            item.MatchBy = m switch
            {
                "familia" or "familiatipo" or "familia:tipo" or "familytype" or "tipo" or "type" => PriceMatch.FamilyType,
                "material" => PriceMatch.Material,
                "categoria" or "category" => PriceMatch.Category,
                _ => PriceMatch.Code,
            };
            if (!string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Pattern))
                t.Items.Add(item);
        }
        return t;
    }

    public TableData ToTable()
    {
        var t = new TableData(Name);
        t.AddColumn("Codigo").AddColumn("Descricao").AddColumn("Unidade")
         .AddColumn("CustoUnitario", ColumnKind.Number).AddColumn("Perda", ColumnKind.Number)
         .AddColumn("Categoria").AddColumn("CasarPor").AddColumn("Padrao")
         .AddColumn("MaoDeObra", ColumnKind.Number).AddColumn("Fornecedor");
        foreach (var i in Items)
            t.AddRow(i.Code, i.Description, i.Unit, i.UnitCost, i.LossPercent, i.Category, i.MatchBy.ToString(), i.Pattern, i.LaborCost, i.Supplier);
        return t;
    }
}

/// <summary>Quantidade levantada do modelo (por elemento ou agregada).</summary>
public sealed class QuantityRecord
{
    public string Room { get; set; } = "";
    public string Category { get; set; } = "";
    public string Code { get; set; } = "";
    public string FamilyType { get; set; } = "";
    public string Material { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "un";
    public double Quantity { get; set; }
    public List<long> ElementIds { get; set; } = new();
}

public sealed class BudgetLine
{
    public string Room { get; set; } = "";
    public string Category { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double QuantityWithLoss { get; set; }
    public double UnitCost { get; set; }
    public double LaborCost { get; set; }
    public double Total { get; set; }
    public bool Priced { get; set; }
    public List<long> ElementIds { get; set; } = new();
}

public sealed class Budget
{
    public List<BudgetLine> Lines { get; } = new();
    public double Total => Lines.Sum(l => l.Total);
    public IEnumerable<BudgetLine> Unpriced => Lines.Where(l => !l.Priced);

    public TableData Detailed()
    {
        var t = new TableData(Loc.T("Orçamento detalhado", "Detailed budget"));
        t.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Categoria", "Category")).AddColumn(Loc.T("Código", "Code"))
         .AddColumn(Loc.T("Descrição", "Description")).AddColumn(Loc.T("Unid.", "Unit"))
         .AddColumn(Loc.T("Quantidade", "Quantity"), ColumnKind.Number).AddColumn(Loc.T("Qtd. c/ perdas", "Qty. w/ losses"), ColumnKind.Number)
         .AddColumn(Loc.T("Custo unit.", "Unit cost"), ColumnKind.Money).AddColumn(Loc.T("Mão de obra unit.", "Unit labor"), ColumnKind.Money)
         .AddColumn(Loc.T("Total", "Total"), ColumnKind.Money);
        foreach (var l in Lines.OrderBy(l => l.Room).ThenBy(l => l.Category).ThenBy(l => l.Description))
            t.AddRowWithIds(l.ElementIds, l.Room, l.Category, l.Code, l.Description, l.Unit, l.Quantity, l.QuantityWithLoss, l.UnitCost, l.LaborCost, l.Total);
        t.AddTotalsRow(Loc.T("TOTAL", "TOTAL"), 9);
        return t;
    }

    public TableData ByRoom() => Group(l => l.Room, Loc.T("Orçamento por ambiente", "Budget by room"), Loc.T("Ambiente", "Room"));

    public TableData ByCategory() => Group(l => l.Category, Loc.T("Orçamento por categoria", "Budget by category"), Loc.T("Categoria", "Category"));

    private TableData Group(Func<BudgetLine, string> key, string title, string keyName)
    {
        var t = new TableData(title);
        t.AddColumn(keyName).AddColumn(Loc.T("Itens", "Items"), ColumnKind.Integer).AddColumn(Loc.T("Total", "Total"), ColumnKind.Money)
         .AddColumn(Loc.T("% do total", "% of total"), ColumnKind.Percent, 1);
        var total = Total;
        foreach (var g in Lines.GroupBy(l => string.IsNullOrWhiteSpace(key(l)) ? Loc.T("(sem)", "(none)") : key(l)).OrderByDescending(g => g.Sum(l => l.Total)))
        {
            var sum = g.Sum(l => l.Total);
            t.AddRowWithIds(g.SelectMany(l => l.ElementIds), g.Key, g.Count(), sum, total <= 0 ? 0 : sum / total);
        }
        t.AddTotalsRow(Loc.T("TOTAL", "TOTAL"), 2);
        return t;
    }
}

public static class BudgetCalculator
{
    public static Budget Calculate(IEnumerable<QuantityRecord> records, PriceTable table)
    {
        var b = new Budget();
        foreach (var r in records)
        {
            var item = FindPrice(r, table);
            var loss = item?.LossPercent ?? 0;
            var q = r.Quantity;
            var ql = q * (1 + loss / 100.0);
            b.Lines.Add(new BudgetLine
            {
                Room = r.Room,
                Category = string.IsNullOrWhiteSpace(item?.Category) ? r.Category : item!.Category,
                Code = item?.Code ?? r.Code,
                Description = string.IsNullOrWhiteSpace(item?.Description) ? r.Description : item!.Description,
                Unit = item?.Unit ?? r.Unit,
                Quantity = q,
                QuantityWithLoss = ql,
                UnitCost = item?.UnitCost ?? 0,
                LaborCost = item?.LaborCost ?? 0,
                Total = item == null ? 0 : ql * item.UnitCost + q * item.LaborCost,
                Priced = item != null,
                ElementIds = r.ElementIds,
            });
        }
        return b;
    }

    public static PriceItem? FindPrice(QuantityRecord r, PriceTable t) =>
        t.Items.FirstOrDefault(i => i.MatchBy == PriceMatch.Code && i.Matches(r.Code))
        ?? t.Items.FirstOrDefault(i => i.MatchBy == PriceMatch.FamilyType && i.Matches(r.FamilyType))
        ?? t.Items.FirstOrDefault(i => i.MatchBy == PriceMatch.Material && i.Matches(r.Material))
        ?? t.Items.FirstOrDefault(i => i.MatchBy == PriceMatch.Category && i.Matches(r.Category));
}

/// <summary>Divergências entre dois levantamentos (ex.: atual × referência salva).</summary>
public static class QuantityDiff
{
    public static TableData Compare(IEnumerable<QuantityRecord> reference, IEnumerable<QuantityRecord> current, double tolerance = 1e-6)
    {
        static string Key(QuantityRecord r) => $"{r.Category}|{r.FamilyType}|{r.Material}|{r.Unit}|{r.Room}";
        var a = reference.GroupBy(Key).ToDictionary(g => g.Key, g => (Q: g.Sum(x => x.Quantity), R: g.First()));
        var b = current.GroupBy(Key).ToDictionary(g => g.Key, g => (Q: g.Sum(x => x.Quantity), R: g.First()));
        var t = new TableData(Loc.T("Divergências de quantitativos", "Quantity differences"));
        t.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Categoria", "Category")).AddColumn(Loc.T("Item", "Item"))
         .AddColumn(Loc.T("Unid.", "Unit")).AddColumn(Loc.T("Referência", "Reference"), ColumnKind.Number)
         .AddColumn(Loc.T("Atual", "Current"), ColumnKind.Number).AddColumn(Loc.T("Diferença", "Difference"), ColumnKind.Number);
        foreach (var k in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            a.TryGetValue(k, out var ra);
            b.TryGetValue(k, out var rb);
            var qa = ra.R == null ? 0 : ra.Q;
            var qb = rb.R == null ? 0 : rb.Q;
            if (Math.Abs(qa - qb) <= tolerance) continue;
            var r = rb.R ?? ra.R!;
            t.AddRowWithIds(rb.R?.ElementIds, r.Room, r.Category, string.IsNullOrEmpty(r.FamilyType) ? r.Material : r.FamilyType, r.Unit, qa, qb, qb - qa);
        }
        return t;
    }

    public static string FormatQuantity(double q, string unit) =>
        q.ToString(unit == "un" ? "N0" : "N2", Loc.Culture) + " " + unit;

    internal static string Invariant(double v) => v.ToString(CultureInfo.InvariantCulture);
}
