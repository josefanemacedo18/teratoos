using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TeraTools.Core.Export;

/// <summary>
/// Gerador mínimo de planilhas Excel (.xlsx) sem bibliotecas externas.
/// Suporta várias abas, cabeçalho destacado, congelamento, autofiltro e formatos numéricos.
/// </summary>
public static class XlsxWriter
{
    private const int StyleDefault = 0;
    private const int StyleHeader = 1;
    private const int StyleNumber = 2;
    private const int StyleInteger = 3;
    private const int StyleMoney = 4;
    private const int StylePercent = 5;
    private const int StyleTitle = 6;

    public static void Write(string path, params TableData[] sheets) => Write(path, (IEnumerable<TableData>)sheets);

    public static void Write(string path, IEnumerable<TableData> sheets)
    {
        var list = sheets.ToList();
        if (list.Count == 0) list.Add(new TableData { Title = "Planilha" });
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(path)) File.Delete(path);

        using var fs = new FileStream(path, FileMode.CreateNew);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var names = UniqueSheetNames(list.Select(t => t.Title));

        Add(zip, "[Content_Types].xml", ContentTypes(list.Count));
        Add(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>");
        Add(zip, "xl/workbook.xml", Workbook(names));
        Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(list.Count));
        Add(zip, "xl/styles.xml", Styles());
        for (var i = 0; i < list.Count; i++)
            Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(list[i]));
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private static List<string> UniqueSheetNames(IEnumerable<string> titles)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var n = 1;
        foreach (var t in titles)
        {
            var baseName = new string((t ?? "").Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim();
            if (baseName.Length == 0) baseName = "Planilha" + n;
            if (baseName.Length > 31) baseName = baseName[..31];
            var name = baseName;
            var k = 2;
            while (!used.Add(name))
            {
                var suffix = " (" + k++ + ")";
                name = (baseName.Length + suffix.Length > 31 ? baseName[..(31 - suffix.Length)] : baseName) + suffix;
            }
            result.Add(name);
            n++;
        }
        return result;
    }

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (var i = 1; i <= sheetCount; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string Workbook(List<string> names)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (var i = 0; i < names.Count; i++)
            sb.Append($"<sheet name=\"{Esc(names[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string WorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 1; i <= sheetCount; i++)
            sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        sb.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string Styles() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"&quot;R$&quot;\\ #,##0.00\"/></numFmts>" +
        "<fonts count=\"3\">" +
        "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "</fonts>" +
        "<fills count=\"3\">" +
        "<fill><patternFill patternType=\"none\"/></fill>" +
        "<fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE3E9F0\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
        "</fills>" +
        "<borders count=\"2\">" +
        "<border><left/><right/><top/><bottom/><diagonal/></border>" +
        "<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FF8A9BB0\"/></bottom><diagonal/></border>" +
        "</borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"7\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
        "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"10\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";

    private static string Sheet(TableData t)
    {
        var sb = new StringBuilder();
        var hasTitle = !string.IsNullOrWhiteSpace(t.Title);
        var headerRow = hasTitle ? 2 : 1;
        var firstDataRow = headerRow + 1;
        var colCount = Math.Max(1, t.Columns.Count);

        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
        sb.Append($"<pane ySplit=\"{headerRow}\" topLeftCell=\"A{firstDataRow}\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("</sheetView></sheetViews>");

        sb.Append("<cols>");
        for (var c = 0; c < t.Columns.Count; c++)
        {
            var maxLen = t.Columns[c].Name.Length;
            foreach (var r in t.Rows.Take(500))
            {
                var len = (Convert.ToString(r[c], CultureInfo.InvariantCulture) ?? "").Length;
                if (len > maxLen) maxLen = len;
            }
            var width = Math.Clamp(maxLen * 1.15 + 2, 8, 60);
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{width:0.##}\" customWidth=\"1\"/>"));
        }
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        var rowIdx = 1;
        if (hasTitle)
        {
            sb.Append($"<row r=\"{rowIdx}\">");
            sb.Append(InlineString(CellRef(0, rowIdx), t.Title, StyleTitle));
            sb.Append("</row>");
            rowIdx++;
        }
        sb.Append($"<row r=\"{rowIdx}\">");
        for (var c = 0; c < t.Columns.Count; c++)
            sb.Append(InlineString(CellRef(c, rowIdx), t.Columns[c].Name, StyleHeader));
        sb.Append("</row>");
        rowIdx++;

        foreach (var r in t.Rows)
        {
            sb.Append($"<row r=\"{rowIdx}\">");
            for (var c = 0; c < t.Columns.Count; c++)
            {
                var v = r[c];
                if (v == null) continue;
                var cref = CellRef(c, rowIdx);
                var col = t.Columns[c];
                if (col.Kind == ColumnKind.Text || v is string { } s && !IsNumeric(s))
                {
                    if (v is bool b)
                        sb.Append($"<c r=\"{cref}\" t=\"b\"><v>{(b ? 1 : 0)}</v></c>");
                    else
                        sb.Append(InlineString(cref, v is IFormattable f ? f.ToString(null, CultureInfo.CurrentCulture) : v.ToString() ?? "", StyleDefault));
                    continue;
                }
                var d = TableData.ToDouble(v);
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    sb.Append(InlineString(cref, "", StyleDefault));
                    continue;
                }
                var style = col.Kind switch
                {
                    ColumnKind.Integer => StyleInteger,
                    ColumnKind.Money => StyleMoney,
                    ColumnKind.Percent => StylePercent,
                    _ => StyleNumber,
                };
                sb.Append($"<c r=\"{cref}\" s=\"{style}\"><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
            }
            sb.Append("</row>");
            rowIdx++;
        }
        sb.Append("</sheetData>");
        if (t.Columns.Count > 0)
            sb.Append($"<autoFilter ref=\"{CellRef(0, headerRow)}:{CellRef(colCount - 1, Math.Max(headerRow, rowIdx - 1))}\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static bool IsNumeric(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static string InlineString(string cref, string text, int style) =>
        $"<c r=\"{cref}\" t=\"inlineStr\"{(style != 0 ? $" s=\"{style}\"" : "")}><is><t xml:space=\"preserve\">{Esc(text)}</t></is></c>";

    public static string ColumnLetters(int index)
    {
        var sb = new StringBuilder();
        index++;
        while (index > 0)
        {
            var m = (index - 1) % 26;
            sb.Insert(0, (char)('A' + m));
            index = (index - 1) / 26;
        }
        return sb.ToString();
    }

    private static string CellRef(int col, int row) => ColumnLetters(col) + row.ToString(CultureInfo.InvariantCulture);

    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default:
                    if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20) sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }
}
