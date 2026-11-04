using System.Globalization;
using System.Text;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Export;

/// <summary>
/// Exportação CSV compatível com o Excel brasileiro: separador ";" e vírgula decimal
/// (em inglês: "," e ponto). Gravado em UTF-8 com BOM para acentos abrirem corretamente.
/// </summary>
public static class CsvWriter
{
    public static string ResolveSeparator(string? setting) => setting switch
    {
        ";" or "semicolon" => ";",
        "," or "comma" => ",",
        "\t" or "tab" => "\t",
        _ => Loc.IsEnglish ? "," : ";",
    };

    public static string ToCsv(TableData table, string separator, CultureInfo culture, bool includeHeader = true)
    {
        var sb = new StringBuilder();
        if (includeHeader)
            sb.AppendLine(string.Join(separator, table.Columns.Select(c => Quote(c.Name, separator))));
        foreach (var row in table.Rows)
        {
            var cells = new string[table.Columns.Count];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = Quote(FormatValue(row[i], table.Columns[i], culture), separator);
            sb.AppendLine(string.Join(separator, cells));
        }
        return sb.ToString();
    }

    public static void Write(TableData table, string path, string? separatorSetting = null)
    {
        var sep = ResolveSeparator(separatorSetting);
        var culture = sep == ";" ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToCsv(table, sep, culture), new UTF8Encoding(true));
    }

    private static string FormatValue(object? v, TableColumn col, CultureInfo culture)
    {
        if (v == null) return "";
        if (col.Kind == ColumnKind.Text)
            return v is IFormattable f ? f.ToString(null, culture) : v.ToString() ?? "";
        var d = TableData.ToDouble(v);
        return col.Kind switch
        {
            ColumnKind.Integer => Math.Round(d).ToString("0", culture),
            ColumnKind.Percent => d.ToString("0.####", culture),
            _ => d.ToString("0.######", culture),
        };
    }

    private static string Quote(string s, string sep)
    {
        if (s.Contains(sep) || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    /// <summary>Leitura simples de CSV (para importar tabelas de preços, listas de ambientes etc.).</summary>
    public static List<string[]> Parse(string text, string? separator = null)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(text)) return rows;
        if (text[0] == '﻿') text = text[1..];
        var sep = separator ?? DetectSeparator(text);
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(ch);
                continue;
            }
            if (ch == '"') { inQuotes = true; continue; }
            if (string.CompareOrdinal(text, i, sep, 0, sep.Length) == 0)
            {
                row.Add(cell.ToString());
                cell.Clear();
                i += sep.Length - 1;
                continue;
            }
            if (ch == '\r') continue;
            if (ch == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                row.Clear();
                continue;
            }
            cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }

    public static string DetectSeparator(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault() ?? "";
        var counts = new[] { ";", ",", "\t" }.Select(s => (s, n: firstLine.Split(s).Length)).OrderByDescending(x => x.n);
        return counts.First().s;
    }
}
