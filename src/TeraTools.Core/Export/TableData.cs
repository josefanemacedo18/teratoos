using System.Globalization;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Export;

public enum ColumnKind
{
    Text,
    Number,
    Integer,
    Money,
    Percent,
}

public sealed class TableColumn
{
    public string Name { get; set; } = "";
    public ColumnKind Kind { get; set; } = ColumnKind.Text;
    public int Decimals { get; set; } = 2;

    public TableColumn() { }

    public TableColumn(string name, ColumnKind kind = ColumnKind.Text, int decimals = 2)
    {
        Name = name;
        Kind = kind;
        Decimals = decimals;
    }
}

/// <summary>Tabela genérica para relatórios, quantitativos e exportação (CSV, Excel, HTML).</summary>
public sealed class TableData
{
    public string Title { get; set; } = "";
    public List<TableColumn> Columns { get; } = new();
    public List<object?[]> Rows { get; } = new();

    /// <summary>Ids de elementos associados a cada linha (opcional, para seleção no modelo).</summary>
    public List<long[]?> RowElementIds { get; } = new();

    public TableData() { }

    public TableData(string title, params TableColumn[] columns)
    {
        Title = title;
        Columns.AddRange(columns);
    }

    public TableData AddColumn(string name, ColumnKind kind = ColumnKind.Text, int decimals = 2)
    {
        Columns.Add(new TableColumn(name, kind, decimals));
        return this;
    }

    public void AddRow(params object?[] values) => AddRowWithIds(null, values);

    public void AddRowWithIds(IEnumerable<long>? ids, params object?[] values)
    {
        var row = new object?[Columns.Count];
        for (var i = 0; i < row.Length && i < values.Length; i++) row[i] = values[i];
        Rows.Add(row);
        RowElementIds.Add(ids?.ToArray());
    }

    public int RowCount => Rows.Count;

    /// <summary>Soma de uma coluna numérica.</summary>
    public double Sum(int column) => Rows.Sum(r => ToDouble(r[column]));

    /// <summary>Adiciona uma linha de totais para as colunas numéricas indicadas.</summary>
    public void AddTotalsRow(string label, params int[] numericColumns)
    {
        var row = new object?[Columns.Count];
        if (row.Length > 0) row[0] = label;
        foreach (var c in numericColumns)
            if (c >= 0 && c < row.Length) row[c] = Sum(c);
        Rows.Add(row);
        RowElementIds.Add(null);
    }

    public static double ToDouble(object? v) => v switch
    {
        null => 0,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) => r,
        _ => 0,
    };

    /// <summary>Texto formatado para exibição na interface.</summary>
    public string FormatCell(int row, int col)
    {
        var v = Rows[row][col];
        if (v == null) return "";
        var c = Columns[col];
        return c.Kind switch
        {
            ColumnKind.Number => ToDouble(v).ToString("N" + c.Decimals, Loc.Culture),
            ColumnKind.Integer => ToDouble(v).ToString("N0", Loc.Culture),
            ColumnKind.Money => ToDouble(v).ToString("C2", Loc.Culture),
            ColumnKind.Percent => (ToDouble(v) * 100).ToString("N" + c.Decimals, Loc.Culture) + "%",
            _ => v is IFormattable f ? f.ToString(null, Loc.Culture) : v.ToString() ?? "",
        };
    }

    /// <summary>Agrupa linhas por uma coluna e soma as colunas numéricas.</summary>
    public TableData GroupBy(int keyColumn, string title, params int[] sumColumns)
    {
        var result = new TableData { Title = title };
        result.Columns.Add(Columns[keyColumn]);
        foreach (var c in sumColumns) result.Columns.Add(Columns[c]);
        foreach (var g in Rows.Select((r, i) => (r, i)).GroupBy(x => Convert.ToString(x.r[keyColumn], CultureInfo.InvariantCulture) ?? ""))
        {
            var values = new List<object?> { g.Key };
            foreach (var c in sumColumns) values.Add(g.Sum(x => ToDouble(x.r[c])));
            var ids = g.SelectMany(x => RowElementIds[x.i] ?? Array.Empty<long>()).Distinct();
            result.AddRowWithIds(ids, values.ToArray());
        }
        return result;
    }
}
