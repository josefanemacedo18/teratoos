using System.Net;
using System.Text;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;

namespace TeraTools.Core.Export;

/// <summary>Relatório HTML imprimível (abre no navegador; pode ser salvo em PDF pelo próprio navegador).</summary>
public static class HtmlReportWriter
{
    public static string Build(string title, string? subtitle, OperationReport? report, IEnumerable<TableData> tables)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"").Append(Loc.IsEnglish ? "en" : "pt-BR").Append("\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(H(title)).Append("</title><style>");
        sb.Append(@"
:root{--ink:#1f2933;--muted:#5f6b7a;--line:#d9dee5;--accent:#b5652b;--ok:#2f7d4f;--warn:#b7791f;--err:#b83232;--bg:#ffffff;--head:#f2f4f7}
*{box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;color:var(--ink);background:var(--bg);margin:0;padding:32px;font-size:13px}
header{border-bottom:3px solid var(--accent);padding-bottom:12px;margin-bottom:20px}
h1{font-size:22px;margin:0 0 4px}h2{font-size:16px;margin:28px 0 8px}
.sub{color:var(--muted)}.chips{display:flex;flex-wrap:wrap;gap:8px;margin:12px 0}
.chip{border:1px solid var(--line);border-radius:14px;padding:4px 12px;background:var(--head)}
table{border-collapse:collapse;width:100%;margin:6px 0 16px}th,td{border-bottom:1px solid var(--line);padding:6px 8px;text-align:left;vertical-align:top}
th{background:var(--head);font-weight:600}td.n{text-align:right;font-variant-numeric:tabular-nums}
.sev-Error{color:var(--err);font-weight:600}.sev-Warning{color:var(--warn);font-weight:600}.sev-Success{color:var(--ok)}
footer{margin-top:32px;color:var(--muted);font-size:11px}
@media print{body{padding:0}h2{break-after:avoid}tr{break-inside:avoid}}
");
        sb.Append("</style></head><body><header>");
        sb.Append("<h1>").Append(H(title)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(subtitle)) sb.Append("<div class=\"sub\">").Append(H(subtitle)).Append("</div>");
        sb.Append("<div class=\"sub\">").Append(H(DateTime.Now.ToString("g", Loc.Culture))).Append("</div>");
        sb.Append("</header>");

        if (report != null)
        {
            if (report.Counters.Count > 0)
            {
                sb.Append("<div class=\"chips\">");
                foreach (var c in report.Counters)
                    sb.Append("<span class=\"chip\">").Append(H(c.Key)).Append(": <b>").Append(c.Value.ToString("N0", Loc.Culture)).Append("</b></span>");
                sb.Append("</div>");
            }
            if (report.Items.Count > 0)
            {
                sb.Append("<h2>").Append(H(Loc.T("Mensagens", "Messages"))).Append("</h2><table><tr><th>")
                  .Append(H(Loc.T("Tipo", "Type"))).Append("</th><th>").Append(H(Loc.T("Mensagem", "Message")))
                  .Append("</th><th>").Append(H(Loc.T("Elementos", "Elements"))).Append("</th></tr>");
                foreach (var it in report.Items.OrderByDescending(i => i.Severity))
                {
                    sb.Append("<tr><td class=\"sev-").Append(it.Severity).Append("\">").Append(H(SeverityText(it.Severity)))
                      .Append("</td><td>").Append(H(it.Message)).Append("</td><td>")
                      .Append(H(string.Join(", ", it.ElementIds.Take(30)) + (it.ElementIds.Count > 30 ? " …" : "")))
                      .Append("</td></tr>");
                }
                sb.Append("</table>");
            }
        }

        foreach (var t in tables)
        {
            sb.Append("<h2>").Append(H(t.Title)).Append("</h2><table><tr>");
            foreach (var c in t.Columns) sb.Append("<th>").Append(H(c.Name)).Append("</th>");
            sb.Append("</tr>");
            for (var r = 0; r < t.Rows.Count; r++)
            {
                sb.Append("<tr>");
                for (var c = 0; c < t.Columns.Count; c++)
                {
                    var numeric = t.Columns[c].Kind != ColumnKind.Text;
                    sb.Append(numeric ? "<td class=\"n\">" : "<td>").Append(H(t.FormatCell(r, c))).Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<footer>Téra Tools</footer></body></html>");
        return sb.ToString();
    }

    public static void Write(string path, string title, string? subtitle, OperationReport? report, IEnumerable<TableData> tables)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Build(title, subtitle, report, tables), new UTF8Encoding(false));
    }

    public static string SeverityText(Severity s) => s switch
    {
        Severity.Error => Loc.T("Erro", "Error"),
        Severity.Warning => Loc.T("Aviso", "Warning"),
        Severity.Success => Loc.T("Sucesso", "Success"),
        _ => Loc.T("Info", "Info"),
    };

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
}
