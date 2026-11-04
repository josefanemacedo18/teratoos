using System.Diagnostics;
using System.Net;
using System.Text;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Ui;

/// <summary>
/// Gera a ajuda integrada (páginas HTML locais por ferramenta + índice) e as abre no navegador.
/// Também é usada como "ajuda contextual" (F1) nos botões da faixa de opções.
/// </summary>
public static class HelpPages
{
    public static string PathFor(string toolId) => Path.Combine(Paths.Help, Core.Text.TextUtil.SafeFileName(toolId) + ".html");

    public static string IndexPath => Path.Combine(Paths.Help, "index.html");

    public static void WriteAll()
    {
        try
        {
            foreach (var t in ToolCatalog.All)
                File.WriteAllText(PathFor(t.Id), Page(t), new UTF8Encoding(false));
            File.WriteAllText(IndexPath, Index(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao gerar a ajuda", ex);
        }
    }

    public static void Open(ToolInfo tool) => OpenFile(File.Exists(PathFor(tool.Id)) ? PathFor(tool.Id) : IndexPath);

    public static void OpenIndex() => OpenFile(IndexPath);

    private static void OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path)) WriteAll();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Msg.Error(ex.Message);
        }
    }

    private const string Css = @"
:root{--ink:#1f2933;--muted:#5f6b7a;--line:#d9dee5;--accent:#b5652b;--bg:#fff;--soft:#f5f6f8}
@media (prefers-color-scheme: dark){:root{--ink:#e8eaed;--muted:#a9b0bb;--line:#4a515e;--bg:#2a2e35;--soft:#31363f;--accent:#d07a3a}}
body{font-family:'Segoe UI',Arial,sans-serif;color:var(--ink);background:var(--bg);max-width:880px;margin:0 auto;padding:28px 20px;line-height:1.55}
h1{font-size:24px;margin:0 0 6px;border-bottom:3px solid var(--accent);padding-bottom:8px}h2{font-size:17px;margin-top:26px}
a{color:var(--accent)}.muted{color:var(--muted)}.card{background:var(--soft);border:1px solid var(--line);border-radius:8px;padding:12px 16px;margin:12px 0}
ul.tools{list-style:none;padding:0}ul.tools li{padding:6px 0;border-bottom:1px solid var(--line)}
.tag{display:inline-block;font-size:11px;padding:1px 8px;border-radius:10px;border:1px solid var(--line);color:var(--muted);margin-left:6px}
pre{white-space:pre-wrap}";

    private static string H(string s) => WebUtility.HtmlEncode(s);

    private static string Paragraphs(string text)
    {
        var sb = new StringBuilder();
        foreach (var block in text.Replace("\r", "").Split("\n\n"))
        {
            var lines = block.Split('\n');
            if (lines.All(l => l.TrimStart().StartsWith("•") || l.TrimStart().StartsWith("-")))
            {
                sb.Append("<ul>");
                foreach (var l in lines) sb.Append("<li>").Append(H(l.TrimStart('•', '-', ' '))).Append("</li>");
                sb.Append("</ul>");
            }
            else
            {
                sb.Append("<p>").Append(H(block).Replace("\n", "<br>")).Append("</p>");
            }
        }
        return sb.ToString();
    }

    private static string Page(ToolInfo t)
    {
        var mod = Modules.Get(t.Module);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(H(t.Name.Value)).Append(" — Téra Tools</title><style>").Append(Css).Append("</style></head><body>");
        sb.Append("<p class='muted'><a href='index.html'>Téra Tools</a> › ").Append(H(mod.Name.Value)).Append("</p>");
        sb.Append("<h1>").Append(H(t.Name.Value)).Append("</h1>");
        sb.Append("<p>").Append(H(t.Description.Value)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(t.Help.Value))
            sb.Append("<div class='card'>").Append(Paragraphs(t.Help.Value)).Append("</div>");
        sb.Append("<h2>").Append(H(Loc.T("Dicas gerais", "General tips"))).Append("</h2><ul>");
        sb.Append("<li>").Append(H(Loc.T("Tudo o que a ferramenta faz pode ser desfeito com um único Ctrl+Z.", "Everything the tool does can be undone with a single Ctrl+Z."))).Append("</li>");
        sb.Append("<li>").Append(H(Loc.T("Use presets para salvar combinações de opções; presets do projeto acompanham o arquivo .rvt.", "Use presets to save option sets; project presets travel with the .rvt file."))).Append("</li>");
        sb.Append("<li>").Append(H(Loc.T("O histórico e o relatório de cada execução ficam no painel Téra Tools.", "The history and the report of each run are in the Téra Tools panel."))).Append("</li>");
        sb.Append("</ul><p class='muted'>").Append(H(t.Id)).Append("</p></body></html>");
        return sb.ToString();
    }

    private static string Index()
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>Téra Tools — ").Append(H(Loc.T("Ajuda", "Help"))).Append("</title><style>").Append(Css).Append("</style></head><body>");
        sb.Append("<h1>Téra Tools</h1><p class='muted'>").Append(H(Loc.T(
            "Plugin para Revit 2027 focado em projeto de interiores, geradores paramétricos, marcenaria e documentação.",
            "Revit 2027 plugin focused on interior design, parametric generators, joinery and documentation."))).Append("</p>");
        sb.Append("<div class='card'>").Append(Paragraphs(Loc.T(
            "Como usar:\n• Abra a aba \"Téra Tools\" na faixa de opções ou o painel lateral (botão Painel).\n• No painel, use a busca para encontrar qualquer ferramenta pelo nome.\n• Clique na estrela para marcar favoritas; as recentes aparecem automaticamente.\n• Pressione F1 sobre um botão da faixa de opções para abrir a ajuda daquela ferramenta.",
            "How to use:\n• Open the \"Téra Tools\" ribbon tab or the side panel (Panel button).\n• In the panel, use search to find any tool by name.\n• Click the star to mark favorites; recent tools appear automatically.\n• Press F1 over a ribbon button to open that tool's help."))).Append("</div>");
        foreach (var m in Modules.All)
        {
            var tools = ToolCatalog.InModule(m.Id).ToList();
            if (tools.Count == 0) continue;
            sb.Append("<h2>").Append(H(m.Name.Value)).Append("<span class='tag'>").Append(tools.Count).Append("</span></h2><p class='muted'>").Append(H(m.Description.Value)).Append("</p><ul class='tools'>");
            foreach (var t in tools)
                sb.Append("<li><a href='").Append(H(Path.GetFileName(PathFor(t.Id)))).Append("'>").Append(H(t.Name.Value)).Append("</a> — <span class='muted'>").Append(H(t.Description.Value)).Append("</span></li>");
            sb.Append("</ul>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
