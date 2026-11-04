using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.General;

[Transaction(TransactionMode.Manual)]
[Tool("general.pane", Modules.General, "Painel Téra", "Téra panel", Large = true, Order = 0, RequiresDocument = false,
    DescPt = "Mostra ou oculta o painel lateral com busca, favoritos, recentes e histórico.",
    DescEn = "Shows or hides the side panel with search, favorites, recent tools and history.",
    HelpPt = "O painel lateral reúne todas as ferramentas do Téra Tools organizadas por módulo.\n\n• Digite na busca para filtrar por nome (com ou sem acento); Enter executa a primeira.\n• Clique na estrela para favoritar.\n• A aba Histórico mostra as últimas operações; clique para repetir.",
    HelpEn = "The side panel gathers all Téra Tools organized by module.\n\n• Type in the search box to filter by name; Enter runs the first match.\n• Click the star to add a favorite.\n• The History tab lists the latest operations; click to repeat.",
    Keywords = "painel busca procurar favoritos recentes panel search")]
public sealed class PaneCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var pane = ctx.App.GetDockablePane(App.PaneId);
        if (pane.IsShown()) pane.Hide();
        else pane.Show();
    }
}

/// <summary>Opções editáveis das configurações globais.</summary>
public sealed class SettingsOptions
{
    [Opt("Idioma", "Language", Group = "Interface|Interface", Tip = "Vale após reiniciar o Revit.|Takes effect after restarting Revit.")]
    public LanguageChoice Language { get; set; } = LanguageChoice.Auto;

    [Opt("Mostrar painel ao abrir o Revit", "Show panel when Revit starts", Group = "Interface|Interface")]
    public bool ShowPaneOnStartup { get; set; } = true;

    [Opt("Mostrar descrições no painel", "Show descriptions in panel", Group = "Interface|Interface")]
    public bool PaneShowDescriptions { get; set; } = true;

    [Opt("Quantidade de recentes", "Recent tools count", Group = "Interface|Interface", Min = 3, Max = 40)]
    public int RecentMax { get; set; } = 12;

    [Opt("Exibir relatório ao final", "Show report at the end", Group = "Execução|Execution", Tip = "Quando houver avisos, erros ou tabelas.|When there are warnings, errors or tables.")]
    public bool ShowReportAfterRun { get; set; } = true;

    [Opt("Selecionar elementos criados", "Select created elements", Group = "Execução|Execution")]
    public bool SelectResults { get; set; } = true;

    [Opt("Lembrar últimas opções", "Remember last options", Group = "Execução|Execution")]
    public bool RememberLastOptions { get; set; } = true;

    [Opt("Pedir confirmação acima de", "Ask for confirmation above", Group = "Desempenho|Performance", Suffix = "elementos|elements", Min = 50, Max = 1000000)]
    public int ElementWarningThreshold { get; set; } = 2000;

    [Opt("Limite máximo por operação", "Maximum per operation", Group = "Desempenho|Performance", Min = 100, Max = 2000000)]
    public int ElementHardLimit { get; set; } = 30000;

    [Opt("Pasta de templates de família", "Family templates folder", Group = "Pastas|Folders", Kind = OptKind.Folder, Tip = "Vazio = pasta padrão do Revit.|Empty = Revit default folder.")]
    public string FamilyTemplateFolder { get; set; } = "";

    [Opt("Biblioteca compartilhada (equipe)", "Shared library (team)", Group = "Pastas|Folders", Kind = OptKind.Folder, Tip = "Pasta de rede com presets e bibliotecas da equipe (somente leitura).|Network folder with team presets and libraries (read-only).")]
    public string SharedLibraryFolder { get; set; } = "";

    [Opt("Separador CSV", "CSV separator", Group = "Exportação|Export")]
    public CsvSeparatorChoice CsvSeparator { get; set; } = CsvSeparatorChoice.Auto;

    [Opt("Log detalhado (diagnóstico)", "Verbose log (diagnostics)", Group = "Avançado|Advanced")]
    public bool VerboseLog { get; set; }
}

public enum LanguageChoice
{
    [Text("Automático (idioma do Revit)", "Automatic (Revit language)")] Auto,
    [Text("Português (Brasil)", "Portuguese (Brazil)")] Portuguese,
    [Text("Inglês", "English")] English,
}

public enum CsvSeparatorChoice
{
    [Text("Automático (; em português)", "Automatic (; in Portuguese)")] Auto,
    [Text("Ponto e vírgula (;)", "Semicolon (;)")] Semicolon,
    [Text("Vírgula (,)", "Comma (,)")] Comma,
    [Text("Tabulação", "Tab")] Tab,
}

[Transaction(TransactionMode.Manual)]
[Tool("general.settings", Modules.General, "Configurações", "Settings", Order = 10, RequiresDocument = false,
    DescPt = "Idioma, painel, limites de desempenho, pastas e exportação.",
    DescEn = "Language, panel, performance limits, folders and export.",
    HelpPt = "Configurações globais valem para todos os projetos e ficam em %AppData%\\TeraTools.\nAs configurações por projeto ficam em Projeto › Dados e padrões do projeto.",
    HelpEn = "Global settings apply to all projects and are stored in %AppData%\\TeraTools.\nPer-project settings are in Project › Project data and standards.",
    Keywords = "configuracoes opcoes idioma language preferences")]
public sealed class SettingsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var s = ctx.Settings;
        var o = new SettingsOptions
        {
            Language = s.Language switch { "pt" => LanguageChoice.Portuguese, "en" => LanguageChoice.English, _ => LanguageChoice.Auto },
            ShowPaneOnStartup = s.ShowPaneOnStartup,
            PaneShowDescriptions = s.PaneShowDescriptions,
            RecentMax = s.RecentMax,
            ShowReportAfterRun = s.ShowReportAfterRun,
            SelectResults = s.SelectResults,
            RememberLastOptions = s.RememberLastOptions,
            ElementWarningThreshold = s.ElementWarningThreshold,
            ElementHardLimit = s.ElementHardLimit,
            FamilyTemplateFolder = s.FamilyTemplateFolder ?? "",
            SharedLibraryFolder = s.SharedLibraryFolder ?? "",
            CsvSeparator = s.CsvSeparator switch { ";" => CsvSeparatorChoice.Semicolon, "," => CsvSeparatorChoice.Comma, "tab" => CsvSeparatorChoice.Tab, _ => CsvSeparatorChoice.Auto },
            VerboseLog = s.VerboseLog,
        };
        var oldLang = s.Language;
        if (!OptionsDialog.ShowStandalone(ctx.HasDocument ? ctx.Doc : null, o, ctx.Tool.Name, ctx.Tool.Description, "general.settings", ctx.Owner, Loc.T("Salvar", "Save")))
            ctx.Cancel();
        s.Language = o.Language switch { LanguageChoice.Portuguese => "pt", LanguageChoice.English => "en", _ => "auto" };
        s.ShowPaneOnStartup = o.ShowPaneOnStartup;
        s.PaneShowDescriptions = o.PaneShowDescriptions;
        s.RecentMax = o.RecentMax;
        s.ShowReportAfterRun = o.ShowReportAfterRun;
        s.SelectResults = o.SelectResults;
        s.RememberLastOptions = o.RememberLastOptions;
        s.ElementWarningThreshold = o.ElementWarningThreshold;
        s.ElementHardLimit = Math.Max(o.ElementHardLimit, o.ElementWarningThreshold);
        s.FamilyTemplateFolder = string.IsNullOrWhiteSpace(o.FamilyTemplateFolder) ? null : o.FamilyTemplateFolder;
        s.SharedLibraryFolder = string.IsNullOrWhiteSpace(o.SharedLibraryFolder) ? null : o.SharedLibraryFolder;
        s.CsvSeparator = o.CsvSeparator switch { CsvSeparatorChoice.Semicolon => ";", CsvSeparatorChoice.Comma => ",", CsvSeparatorChoice.Tab => "tab", _ => "auto" };
        s.VerboseLog = o.VerboseLog;
        Services.SaveSettings();
        if (oldLang != s.Language)
            Msg.Info(Loc.T("O novo idioma será aplicado na próxima vez que o Revit for aberto.", "The new language will apply the next time Revit starts."));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("general.help", Modules.General, "Ajuda", "Help", Order = 20, RequiresDocument = false,
    DescPt = "Abre a documentação integrada com todas as ferramentas.",
    DescEn = "Opens the built-in documentation with all tools.",
    Keywords = "ajuda manual documentacao help documentation")]
public sealed class HelpCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        HelpPages.WriteAll();
        HelpPages.OpenIndex();
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("general.history", Modules.General, "Histórico de operações", "Operation history", Order = 30, RequiresDocument = false,
    DescPt = "Lista as operações executadas pelo plugin, com status, duração e resumo.",
    DescEn = "Lists operations run by the plugin, with status, duration and summary.",
    Keywords = "historico log registro operacoes history")]
public sealed class HistoryCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var t = new TableData(Loc.T("Histórico", "History"));
        t.AddColumn(Loc.T("Data", "Date")).AddColumn(Loc.T("Ferramenta", "Tool")).AddColumn(Loc.T("Projeto", "Project"))
         .AddColumn(Loc.T("Status", "Status")).AddColumn(Loc.T("Duração (s)", "Duration (s)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Resumo", "Summary"));
        foreach (var e in Services.History.ReadLast(2000))
        {
            var status = e.Status switch
            {
                RunStatus.Failed => Loc.T("Falhou", "Failed"),
                RunStatus.Cancelled => Loc.T("Cancelado", "Cancelled"),
                RunStatus.Warning => Loc.T("Com avisos", "With warnings"),
                _ => Loc.T("Concluído", "Completed"),
            };
            t.AddRow(e.Time.ToString("g", Loc.Culture), ToolCatalog.Get(e.ToolId)?.Name.Value ?? e.ToolName, e.Document ?? "", status, e.DurationMs / 1000.0, e.Summary ?? "");
        }
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
        ctx.Report.Info(Loc.F("Arquivo do histórico: {0}", "History file: {0}", Services.History.FilePath));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("general.about", Modules.General, "Diagnóstico", "Diagnostics", Order = 40, RequiresDocument = false,
    DescPt = "Versão, pastas, templates de família encontrados e informações para suporte.",
    DescEn = "Version, folders, family templates found and support information.",
    Keywords = "sobre versao diagnostico suporte about version support")]
public sealed class AboutCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var app = ctx.App.Application;
        var t = new TableData(Loc.T("Diagnóstico", "Diagnostics"));
        t.AddColumn(Loc.T("Item", "Item")).AddColumn(Loc.T("Valor", "Value"));
        t.AddRow("Téra Tools", typeof(App).Assembly.GetName().Version?.ToString());
        t.AddRow("Revit", $"{app.VersionName} ({app.VersionBuild})");
        t.AddRow(Loc.T("Idioma do plugin", "Plugin language"), Loc.Current.ToString());
        t.AddRow(Loc.T("Ferramentas carregadas", "Tools loaded"), ToolCatalog.All.Count);
        t.AddRow(Loc.T("Pasta de dados", "Data folder"), Paths.Root);
        t.AddRow(Loc.T("Pasta do plugin", "Plugin folder"), Paths.AddinFolder);
        t.AddRow(Loc.T("Log de hoje", "Today's log"), Log.CurrentFile);
        t.AddRow(Loc.T("Templates de família", "Family templates"), app.FamilyTemplatePath);
        var tpl = Families.FamilyTemplates.Find(app, Families.TemplateKind.GenericModel, ctx.Settings.FamilyTemplateFolder);
        t.AddRow(Loc.T("Template de Modelo Genérico", "Generic Model template"), tpl ?? Loc.T("NÃO ENCONTRADO — configure a pasta de templates", "NOT FOUND — set the templates folder"));
        if (ctx.HasDocument)
        {
            t.AddRow(Loc.T("Projeto", "Project"), ctx.Doc.Title);
            t.AddRow(Loc.T("Unidade de comprimento", "Length unit"), UnitFormatUtils.Format(ctx.Doc.GetUnits(), SpecTypeId.Length, U.M(1), false));
        }
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
        if (tpl == null)
            ctx.Report.Warn(Loc.T("Template de família não encontrado: os geradores precisam dele para criar famílias paramétricas.",
                "Family template not found: generators need it to create parametric families."));
        else
            ctx.Report.Success(Loc.T("Ambiente pronto para os geradores.", "Environment ready for generators."));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("general.logs", Modules.General, "Abrir pasta de dados", "Open data folder", Order = 50, RequiresDocument = false, Ribbon = false,
    DescPt = "Abre a pasta com configurações, presets, logs e exportações do plugin.",
    DescEn = "Opens the folder with plugin settings, presets, logs and exports.",
    Keywords = "pasta dados logs presets folder")]
public sealed class OpenDataFolderCommand : ToolCommand
{
    public override void Run(ToolContext ctx) =>
        Process.Start(new ProcessStartInfo(Paths.Root) { UseShellExecute = true });
}
