using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Settings;
using TeraTools.Core.Text;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Materials;
using TeraTools.Revit.Tools.Quality;
using TeraTools.Revit.Tools.Quantities;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Project;

public enum PresetAction
{
    [Text("Listar todos os presets", "List all presets")] List,
    [Text("Exportar presets de uma ferramenta (arquivo)", "Export a tool's presets (file)")] Export,
    [Text("Importar presets de arquivo", "Import presets from file")] Import,
    [Text("Copiar presets pessoais para o projeto", "Copy personal presets into the project")] ToProject,
    [Text("Copiar presets do projeto para os pessoais", "Copy project presets to personal")] ToUser,
    [Text("Excluir presets", "Delete presets")] Delete,
}

public sealed class PresetManagerOptions
{
    [Opt("Ação", "Action")]
    public PresetAction Action { get; set; } = PresetAction.List;

    [Opt("Arquivo", "File", Kind = OptKind.File, Filter = "Presets|*.json", VisibleIf = "Action=Import")]
    public string File { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("proj.presets", Modules.Project, "Presets e bibliotecas", "Presets and libraries", Order = 11,
    DescPt = "Gerencia os presets de todas as ferramentas: lista, exporta/importa arquivos para compartilhar, copia entre pessoal e projeto e exclui.",
    DescEn = "Manages presets of all tools: list, export/import files to share, copy between personal and project, and delete.",
    HelpPt = "Presets pessoais ficam em %AppData%\\TeraTools\\presets; presets do projeto ficam dentro do .rvt; uma pasta de biblioteca da equipe pode ser definida nas configurações gerais (somente leitura).",
    HelpEn = "Personal presets are in %AppData%\\TeraTools\\presets; project presets are inside the .rvt; a team library folder can be set in general settings (read-only).",
    Keywords = "presets bibliotecas exportar importar compartilhar presets libraries export import share")]
public sealed class PresetManagerCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<PresetManagerOptions>();
        var store = Services.Presets;
        var keys = ToolCatalog.All.Select(t => t.Id).Concat(new[] { "furn.layout" })
            .Concat(Enum.GetNames<Core.Joinery.CabinetKind>().Select(k => "join.module." + k)).Distinct().ToList();
        string Label(string key) => ToolCatalog.Get(key)?.Name.Value ?? key;
        var project = ProjectStorage.LoadPresets(doc);
        var all = keys.SelectMany(k => store.List(k).Select(p => (Key: k, P: p)).Concat(project.List(k).Select(p => (Key: k, P: p)))).ToList();
        switch (o.Action)
        {
            case PresetAction.Export:
            {
                var key = Pick.One(all.Select(a => a.Key).Distinct().ToList(), Label, ctx.Tool.Name, Loc.T("Presets de qual ferramenta?", "Presets of which tool?"), ctx.Owner);
                if (key == null) ctx.Cancel();
                var path = Path.Combine(Paths.Exports, "presets-" + TextUtil.SafeFileName(key!) + ".json");
                store.Export(path, key!, all.Where(a => a.Key == key).Select(a => a.P));
                ctx.Report.Success(Loc.F("Presets exportados: {0}", "Presets exported: {0}", path));
                MatLib.OpenFolder(path);
                break;
            }
            case PresetAction.Import:
            {
                if (!System.IO.File.Exists(o.File)) ToolContext.Fail("Escolha o arquivo de presets.", "Choose the presets file.");
                var n = 0;
                foreach (var k in keys)
                {
                    try { n += store.Import(o.File, k); } catch { /* chave diferente */ }
                }
                ctx.Report.Success(Loc.F("{0} presets importados.", "{0} presets imported.", n));
                break;
            }
            case PresetAction.ToProject:
            {
                var items = Pick.Many(all.Where(a => a.P.Scope == PresetScope.User).ToList(), a => $"{Label(a.Key)} — {a.P.Name}", ctx.Tool.Name,
                    Loc.T("Quais presets copiar para o projeto?", "Which presets to copy into the project?"), ctx.Owner);
                if (items.Count == 0) ctx.Cancel();
                ctx.Modify(ctx.Tool.Name, () =>
                {
                    foreach (var (k, p) in items) project.Save(k, new Preset { Name = p.Name, Key = k, Json = p.Json, Description = p.Description, Scope = PresetScope.Project });
                    ProjectStorage.SavePresets(doc, project);
                });
                ctx.Report.Count(Loc.T("Copiados", "Copied"), items.Count);
                break;
            }
            case PresetAction.ToUser:
            {
                var items = Pick.Many(all.Where(a => a.P.Scope == PresetScope.Project).ToList(), a => $"{Label(a.Key)} — {a.P.Name}", ctx.Tool.Name,
                    Loc.T("Quais presets do projeto copiar para os pessoais?", "Which project presets to copy to personal?"), ctx.Owner);
                foreach (var (k, p) in items) store.Save(k, new Preset { Name = p.Name, Key = k, Json = p.Json, Description = p.Description });
                ctx.Report.Count(Loc.T("Copiados", "Copied"), items.Count);
                break;
            }
            case PresetAction.Delete:
            {
                var items = Pick.Many(all.Where(a => a.P.Scope != PresetScope.Library && a.P.Name != PresetStore.LastUsedName).ToList(),
                    a => $"{Label(a.Key)} — {a.P.Name} ({a.P.Scope})", ctx.Tool.Name, Loc.T("Quais presets excluir?", "Which presets to delete?"), ctx.Owner);
                if (items.Count == 0) ctx.Cancel();
                if (!Msg.Confirm(Loc.F("Excluir {0} presets?", "Delete {0} presets?", items.Count), ctx.Tool.Name)) ctx.Cancel();
                var projectChanged = false;
                foreach (var (k, p) in items)
                {
                    if (p.Scope == PresetScope.Project) projectChanged |= project.Delete(k, p.Name);
                    else store.Delete(k, p.Name);
                    ctx.Report.Deleted();
                }
                if (projectChanged) ctx.Modify(ctx.Tool.Name, () => ProjectStorage.SavePresets(doc, project));
                break;
            }
        }
        var t = new TableData(Loc.T("Presets", "Presets"));
        t.AddColumn(Loc.T("Ferramenta", "Tool")).AddColumn(Loc.T("Nome", "Name")).AddColumn(Loc.T("Onde", "Where")).AddColumn(Loc.T("Modificado", "Modified"));
        foreach (var (k, p) in all.Where(a => a.P.Name != PresetStore.LastUsedName).OrderBy(a => Label(a.Key)).ThenBy(a => a.P.Name))
            t.AddRow(Label(k), p.Name, p.Scope switch { PresetScope.Project => Loc.T("Projeto", "Project"), PresetScope.Library => Loc.T("Biblioteca", "Library"), _ => Loc.T("Pessoal", "Personal") },
                p.Modified.ToString("g", Loc.Culture));
        ctx.Report.Tables.Add(t);
        ctx.Report.ForceShow = true;
    }
}

internal sealed class UseDestinationTypes : IDuplicateTypeNamesHandler
{
    public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args) => DuplicateTypeAction.UseDestinationTypes;
}

public sealed class TransferOptions
{
    [Opt("Modelos de vista", "View templates", Group = "Padrões do Revit|Revit standards")] public bool Templates { get; set; } = true;
    [Opt("Filtros de vista", "View filters", Group = "Padrões do Revit|Revit standards")] public bool Filters { get; set; } = true;
    [Opt("Materiais", "Materials", Group = "Padrões do Revit|Revit standards")] public bool Materials { get; set; } = true;
    [Opt("Padrões de preenchimento e de linha", "Fill and line patterns", Group = "Padrões do Revit|Revit standards")] public bool Patterns { get; set; } = true;
    [Opt("Tipos de texto, cota e região preenchida", "Text, dimension and filled region types", Group = "Padrões do Revit|Revit standards")] public bool AnnotationTypes { get; set; } = true;
    [Opt("Tipos de parede, piso, forro e cobertura", "Wall, floor, ceiling and roof types", Group = "Padrões do Revit|Revit standards")] public bool HostTypes { get; set; }
    [Opt("Tabelas", "Schedules", Group = "Padrões do Revit|Revit standards")] public bool Schedules { get; set; }
    [Opt("Presets do projeto", "Project presets", Group = "Téra Tools")] public bool Presets { get; set; } = true;
    [Opt("Regras de verificação e de marcenaria", "Check and joinery rules", Group = "Téra Tools")] public bool Rules { get; set; } = true;
    [Opt("Temas de cor", "Color themes", Group = "Téra Tools")] public bool Themes { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("proj.transfer", Modules.Project, "Transferir padrões entre projetos", "Transfer standards between projects", Order = 12,
    DescPt = "Copia de outro projeto aberto os modelos de vista, filtros, materiais, padrões, tipos, tabelas e as configurações do Téra Tools (presets, regras, temas).",
    DescEn = "Copies from another open project the view templates, filters, materials, patterns, types, schedules and Téra Tools settings (presets, rules, themes).",
    HelpPt = "Abra o projeto de origem (ou o seu arquivo-modelo) no Revit, volte ao projeto de destino e rode a ferramenta. Itens com o mesmo nome no destino são mantidos (não são sobrescritos).",
    HelpEn = "Open the source project (or your template file) in Revit, return to the target project and run the tool. Items with the same name in the target are kept (not overwritten).",
    Keywords = "transferir padroes projeto modelos de vista filtros materiais template transfer standards")]
public sealed class TransferCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var sources = ctx.App.Application.Documents.Cast<Document>().Where(d => !d.IsFamilyDocument && !d.Equals(doc) && !d.IsLinked).ToList();
        if (sources.Count == 0) ToolContext.Fail("Abra no Revit o projeto de origem (ou arquivo-modelo) e tente de novo.", "Open the source project (or template) in Revit and try again.");
        var src = Pick.One(sources, d => d.Title, ctx.Tool.Name, Loc.T("Copiar padrões de qual projeto?", "Copy standards from which project?"), ctx.Owner);
        if (src == null) ctx.Cancel();
        var o = ctx.Ask<TransferOptions>();
        var ids = new List<(string Group, ICollection<ElementId> Ids)>();
        void Add(string group, IEnumerable<Element> els)
        {
            var list = els.Select(e => e.Id).ToList();
            if (list.Count > 0) ids.Add((group, list));
        }
        if (o.Templates) Add(Loc.T("Modelos de vista", "View templates"), Lookup.Collect<View>(src!).Where(v => v.IsTemplate));
        if (o.Filters) Add(Loc.T("Filtros", "Filters"), Lookup.Collect<ParameterFilterElement>(src!));
        if (o.Materials) Add(Loc.T("Materiais", "Materials"), Lookup.Collect<Material>(src!));
        if (o.Patterns)
        {
            Add(Loc.T("Padrões de preenchimento", "Fill patterns"), Lookup.Collect<FillPatternElement>(src!));
            Add(Loc.T("Padrões de linha", "Line patterns"), Lookup.Collect<LinePatternElement>(src!));
        }
        if (o.AnnotationTypes)
        {
            Add(Loc.T("Tipos de texto", "Text types"), Lookup.Collect<TextNoteType>(src!));
            Add(Loc.T("Tipos de cota", "Dimension types"), Lookup.Collect<DimensionType>(src!).Where(d => d.StyleType == DimensionStyleType.Linear || d.StyleType == DimensionStyleType.Angular));
            Add(Loc.T("Tipos de região preenchida", "Filled region types"), Lookup.Collect<FilledRegionType>(src!));
        }
        if (o.HostTypes) Add(Loc.T("Tipos de sistema", "System types"), Lookup.Collect<HostObjAttributes>(src!));
        if (o.Schedules) Add(Loc.T("Tabelas", "Schedules"), Lookup.Collect<ViewSchedule>(src!).Where(v => !v.IsTemplate && !v.IsTitleblockRevisionSchedule && !v.IsInternalKeynoteSchedule && v.Definition.CategoryId != ElementId.InvalidElementId));
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var cpo = new CopyPasteOptions();
            cpo.SetDuplicateTypeNamesHandler(new UseDestinationTypes());
            foreach (var (group, list) in ids)
            {
                try
                {
                    var copied = ElementTransformUtils.CopyElements(src!, list, doc, Transform.Identity, cpo);
                    ctx.Report.Count(group, copied.Count);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(group + ": " + ex.Message);
                }
            }
            var s = ProjectStorage.Load(src!);
            var d = ProjectStorage.Load(doc);
            var keys = new List<string>();
            if (o.Presets) keys.Add(ProjectSettings.SectionPresets);
            if (o.Rules) keys.AddRange(new[] { QaRules.Key, Joinery.JoineryGen.RulesKey });
            if (o.Themes) keys.Add(ProjectSettings.SectionThemes);
            var n = 0;
            foreach (var k in keys)
            {
                if (!s.Sections.TryGetValue(k, out var json)) continue;
                d.Sections[k] = json;
                n++;
            }
            if (n > 0)
            {
                ProjectStorage.Save(doc, d);
                ctx.Report.Count(Loc.T("Configurações do Téra Tools", "Téra Tools settings"), n);
            }
        });
    }
}

public enum BatchAction
{
    [Text("Exportar PDF de todas as pranchas", "Export PDF of all sheets")] Pdf,
    [Text("Verificação de qualidade (relatório)", "Quality check (report)")] Quality,
    [Text("Levantamento de quantitativos (Excel)", "Quantity takeoff (Excel)")] Quantities,
    [Text("Atualizar e salvar na versão atual do Revit", "Upgrade and save in the current Revit version")] Upgrade,
}

public sealed class BatchOptions
{
    [Opt("Pasta com os arquivos .rvt", "Folder with .rvt files", Kind = OptKind.Folder, Required = true)]
    public string Folder { get; set; } = "";

    [Opt("Incluir subpastas", "Include subfolders")]
    public bool Recursive { get; set; }

    [Opt("Ação", "Action")]
    public BatchAction Action { get; set; } = BatchAction.Quality;
}

[Transaction(TransactionMode.Manual)]
[Tool("proj.batch", Modules.Project, "Processamento em lote", "Batch processing", Order = 13,
    DescPt = "Abre vários arquivos .rvt em segundo plano e executa uma ação em cada um: PDF das pranchas, verificação de qualidade, quantitativos ou atualização de versão.",
    DescEn = "Opens several .rvt files in the background and runs an action on each: sheet PDFs, quality check, quantities or version upgrade.",
    HelpPt = "Os arquivos são abertos sem interface e fechados ao final (somente \"Atualizar e salvar\" grava). Arquivos com compartilhamento de trabalho são abertos desanexados para as ações de leitura. Os resultados ficam em exportacoes\\lote.",
    HelpEn = "Files are opened without UI and closed at the end (only \"Upgrade and save\" writes). Workshared files are opened detached for read actions. Results go to exportacoes\\lote.",
    Keywords = "lote varios arquivos batch processamento exportar verificar multiple files batch")]
public sealed class BatchCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<BatchOptions>();
        if (!Directory.Exists(o.Folder)) ToolContext.Fail("Pasta não encontrada.", "Folder not found.");
        var files = Directory.GetFiles(o.Folder, "*.rvt", o.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"\.\d{4}\.rvt$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .OrderBy(f => f).ToList();
        var chosen = Pick.Many(files, f => Path.GetFileName(f), ctx.Tool.Name, Loc.T("Quais arquivos processar?", "Which files to process?"), ctx.Owner, _ => true, f => Path.GetDirectoryName(f) ?? "");
        if (chosen.Count == 0) ctx.Cancel();
        var app = ctx.App.Application;
        var outRoot = Path.Combine(Paths.Exports, "lote", DateTime.Now.ToString("yyyyMMdd-HHmm"));
        Directory.CreateDirectory(outRoot);
        var summary = new TableData(Loc.T("Processamento em lote", "Batch processing"));
        summary.AddColumn(Loc.T("Arquivo", "File")).AddColumn(Loc.T("Resultado", "Result")).AddColumn(Loc.T("Detalhes", "Details"));
        var open = app.Documents.Cast<Document>().Select(d => d.PathName).Where(p => !string.IsNullOrEmpty(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in chosen)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (open.Contains(file))
            {
                summary.AddRow(name, Loc.T("Ignorado", "Skipped"), Loc.T("O arquivo já está aberto no Revit.", "The file is already open in Revit."));
                continue;
            }
            Document? d = null;
            try
            {
                var info = BasicFileInfo.Extract(file);
                var oo = new OpenOptions();
                if (info.IsWorkshared && o.Action != BatchAction.Upgrade) oo.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
                if (info.IsWorkshared && o.Action == BatchAction.Upgrade)
                {
                    summary.AddRow(name, Loc.T("Ignorado", "Skipped"), Loc.T("Arquivo central com compartilhamento: atualize pelo próprio Revit.", "Workshared central file: upgrade it in Revit itself."));
                    continue;
                }
                d = app.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(file), oo);
                var folder = Path.Combine(outRoot, TextUtil.SafeFileName(name));
                Directory.CreateDirectory(folder);
                var details = o.Action switch
                {
                    BatchAction.Pdf => ExportPdfs(d, folder),
                    BatchAction.Quality => Quality(d, folder),
                    BatchAction.Quantities => Quantities(d, folder),
                    _ => Upgrade(d),
                };
                summary.AddRow(name, Loc.T("OK", "OK"), details);
                ctx.Report.Count(Loc.T("Arquivos processados", "Files processed"));
            }
            catch (Exception ex)
            {
                summary.AddRow(name, Loc.T("Erro", "Error"), ex.Message);
                ctx.Report.Warn(name + ": " + ex.Message);
            }
            finally
            {
                try { d?.Close(false); } catch { /* já fechado */ }
            }
        }
        ctx.Report.Tables.Add(summary);
        ctx.Report.Success(Loc.F("Resultados em: {0}", "Results in: {0}", outRoot));
        MatLib.OpenFolder(outRoot);
        ctx.Report.ForceShow = true;
    }

    private static string ExportPdfs(Document d, string folder)
    {
        var sheets = Lookup.Collect<ViewSheet>(d).Where(s => !s.IsPlaceholder).OrderBy(s => s.SheetNumber, TextUtil.NaturalComparer).ToList();
        var n = 0;
        foreach (var s in sheets)
        {
            var opts = new PDFExportOptions { FileName = TextUtil.SafeFileName($"{s.SheetNumber} - {s.Name}"), Combine = true, ZoomType = ZoomType.Zoom, ZoomPercentage = 100, HideCropBoundaries = true, HideScopeBoxes = true };
            if (d.Export(folder, new List<ElementId> { s.Id }, opts)) n++;
        }
        return Loc.F("{0} de {1} pranchas exportadas", "{0} of {1} sheets exported", n, sheets.Count);
    }

    private static string Quality(Document d, string folder)
    {
        var report = new Core.Reporting.OperationReport { Title = Loc.T("Verificação de qualidade", "Quality check") };
        QaRunner.Run(d, new QaCheckOptions { Save = false }, report);
        var html = Path.Combine(folder, "verificacao.html");
        HtmlReportWriter.Write(html, report.Title, d.Title, report, report.Tables);
        XlsxWriter.Write(Path.ChangeExtension(html, ".xlsx"), report.Tables);
        return Loc.F("{0} erros, {1} avisos", "{0} errors, {1} warnings",
            report.Items.Count(i => i.Severity == Core.Reporting.Severity.Error), report.Items.Count(i => i.Severity == Core.Reporting.Severity.Warning));
    }

    private static string Quantities(Document d, string folder)
    {
        var records = QuantityCollector.Collect(d, new QuantityScope { RoomFinishes = true });
        var t1 = QuantityCollector.Table(QuantityCollector.Aggregate(records, false), Loc.T("Resumo de quantitativos", "Quantity summary"), false);
        var t2 = QuantityCollector.Table(QuantityCollector.Aggregate(records, true), Loc.T("Quantitativos por ambiente", "Quantities by room"), true);
        XlsxWriter.Write(Path.Combine(folder, "quantitativos.xlsx"), t1, t2);
        return Loc.F("{0} itens", "{0} items", t1.Rows.Count);
    }

    private static string Upgrade(Document d)
    {
        d.Save(new SaveOptions());
        return Loc.T("Salvo na versão atual", "Saved in the current version");
    }
}
