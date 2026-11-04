using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Checks;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Materials;
using TeraTools.Revit.Ui;
using ElementRecord = TeraTools.Core.Checks.ElementRecord;

namespace TeraTools.Revit.Tools.Quality;

internal static class QaRules
{
    public const string Key = "qa.rules";

    public static RuleSet Load(Document doc)
    {
        try
        {
            if (ProjectStorage.Load(doc).HasSection(Key)) return ProjectStorage.GetSection<RuleSet>(doc, Key);
        }
        catch (Exception ex)
        {
            Log.Warn("Regras de verificação: " + ex.Message);
        }
        var user = Path.Combine(Paths.Libraries, "regras-verificacao.json");
        if (File.Exists(user))
        {
            try { return Json.LoadOrDefault<RuleSet>(user); } catch { /* usa padrão */ }
        }
        return RuleSet.Default();
    }

    public static readonly string[] Header =
        { "Ativa", "Nome", "Severidade", "Categorias", "FiltroFamilia", "Tipo", "Parametro", "ParametroDeTipo", "Min", "Max", "Padrao", "Valores", "Mensagem" };

    public static TableData ToTable(RuleSet set)
    {
        var t = new TableData(Loc.T("Regras de verificação", "Check rules"));
        foreach (var h in Header) t.AddColumn(h);
        foreach (var r in set.Rules)
            t.AddRow(r.Enabled ? "1" : "0", r.Name, r.Severity.ToString(), string.Join(",", r.Categories), r.FamilyFilter, r.Kind.ToString(), r.Parameter,
                r.TypeParameter ? "1" : "0", r.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.Pattern, string.Join("|", r.AllowedValues), r.Message);
        return t;
    }

    public static RuleSet FromCsv(string text, string name)
    {
        var rows = CsvWriter.Parse(text);
        var set = new RuleSet { Name = name };
        if (rows.Count < 2) return set;
        var head = rows[0].Select(TextUtil.NormalizeForCompare).ToArray();
        string G(string[] r, string col)
        {
            var i = Array.IndexOf(head, TextUtil.NormalizeForCompare(col));
            return i >= 0 && i < r.Length ? r[i].Trim() : "";
        }
        foreach (var r in rows.Skip(1))
        {
            var nm = G(r, "Nome");
            var param = G(r, "Parametro");
            if (nm.Length == 0 && param.Length == 0) continue;
            var rule = new CheckRule
            {
                Enabled = G(r, "Ativa") is not ("0" or "nao" or "não" or "false"),
                Name = nm.Length > 0 ? nm : param,
                Severity = Enum.TryParse<Severity>(G(r, "Severidade"), true, out var sv) ? sv : ParseSeverity(G(r, "Severidade")),
                Categories = G(r, "Categorias").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList(),
                FamilyFilter = NullIfEmpty(G(r, "FiltroFamilia")),
                Kind = Enum.TryParse<RuleKind>(G(r, "Tipo"), true, out var k) ? k : ParseKind(G(r, "Tipo")),
                Parameter = param,
                TypeParameter = G(r, "ParametroDeTipo") is "1" or "sim" or "true",
                Min = U.TryParseNumber(G(r, "Min"), out var mn) ? mn : null,
                Max = U.TryParseNumber(G(r, "Max"), out var mx) ? mx : null,
                Pattern = NullIfEmpty(G(r, "Padrao")),
                AllowedValues = G(r, "Valores").Split('|', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList(),
                Message = NullIfEmpty(G(r, "Mensagem")),
            };
            set.Rules.Add(rule);
        }
        return set;
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static Severity ParseSeverity(string s) => TextUtil.NormalizeForCompare(s) switch
    {
        "erro" => Severity.Error,
        "info" or "informacao" => Severity.Info,
        _ => Severity.Warning,
    };

    private static RuleKind ParseKind(string s) => TextUtil.NormalizeForCompare(s) switch
    {
        "faixa" or "intervalo" => RuleKind.Range,
        "padrao" or "regex" => RuleKind.Pattern,
        "valorespermitidos" or "lista" => RuleKind.AllowedValues,
        "unico" => RuleKind.Unique,
        "padraonome" or "nome" => RuleKind.NamePattern,
        _ => RuleKind.Required,
    };

    /// <summary>Monta os registros (valores de parâmetros) dos elementos das categorias usadas nas regras.</summary>
    public static List<ElementRecord> Records(Document doc, RuleSet set)
    {
        var rules = set.Rules.Where(r => r.Enabled).ToList();
        var cats = rules.SelectMany(r => r.Categories).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bics = Lookup.ParseCategories(string.Join(",", cats));
        var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
        IEnumerable<Element> elements = bics.Count > 0
            ? collector.WherePasses(new ElementMulticategoryFilter(bics)).ToElements()
            : collector.WherePasses(new ElementMulticategoryFilter(Lookup.ModelCategories(doc).Select(c => c.Id).ToList())).ToElements();
        var instParams = rules.Where(r => !r.TypeParameter).Select(r => r.Parameter).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var typeParams = rules.Where(r => r.TypeParameter).Select(r => r.Parameter).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var list = new List<ElementRecord>();
        foreach (var e in elements)
        {
            if (e.Category == null) continue;
            if (e is Room room && room.Area <= 0 && room.Location == null) continue;
            var type = doc.GetElement(e.GetTypeId()) as ElementType;
            var rec = new ElementRecord
            {
                Id = e.Id.Value,
                Category = Enum.IsDefined(typeof(BuiltInCategory), (int)e.Category.Id.Value) ? ((BuiltInCategory)e.Category.Id.Value).ToString() : e.Category.Name,
                CategoryName = e.Category.Name,
                Family = type?.FamilyName ?? "",
                Type = type?.Name ?? "",
                Name = e is Room r ? RoomUtilText(r) : e.Name,
            };
            foreach (var name in instParams) Read(e, name, rec.Values, rec.Numbers, rec.Present);
            if (type != null) foreach (var name in typeParams) Read(type, name, rec.TypeValues, rec.TypeNumbers, rec.Present);
            list.Add(rec);
        }
        return list;
    }

    private static string RoomUtilText(Room r) => $"{r.Number} {r.Name}".Trim();

    private static void Read(Element e, string name, Dictionary<string, string?> values, Dictionary<string, double?> numbers, HashSet<string> present)
    {
        Parameter? p = Enum.TryParse<BuiltInParameter>(name, out var bip) ? e.get_Parameter(bip) : null;
        p ??= e.LookupParameter(name);
        if (p == null) return;
        present.Add(name);
        values[name] = p.StorageType switch
        {
            StorageType.String => p.AsString(),
            StorageType.ElementId => p.AsElementId() == ElementId.InvalidElementId ? "" : e.Document.GetElement(p.AsElementId())?.Name ?? p.AsValueString(),
            _ => p.HasValue ? p.AsValueString() : "",
        };
        if (p.StorageType == StorageType.Double && p.HasValue)
        {
            var v = p.AsDouble();
            var dt = p.Definition.GetDataType();
            numbers[name] = dt == SpecTypeId.Length ? U.ToMm(v) : dt == SpecTypeId.Area ? U.SqFtToM2(v) : dt == SpecTypeId.Volume ? U.CuFtToM3(v)
                : dt == SpecTypeId.Angle ? Core.Mathx.MathUtil.ToDegrees(v) : v;
        }
        else if (p.StorageType == StorageType.Integer && p.HasValue)
        {
            numbers[name] = p.AsInteger();
        }
    }
}

public sealed class QaCheckOptions
{
    [Opt("Regras de parâmetros e nomes (editáveis)", "Parameter and naming rules (editable)", Group = "Verificações|Checks")]
    public bool Rules { get; set; } = true;

    [Opt("Avisos do Revit", "Revit warnings", Group = "Verificações|Checks")]
    public bool Warnings { get; set; } = true;

    [Opt("Elementos duplicados no mesmo lugar", "Duplicated elements in the same place", Group = "Verificações|Checks")]
    public bool Duplicates { get; set; } = true;

    [Opt("Famílias no local (in-place) e CAD importado", "In-place families and imported CAD", Group = "Verificações|Checks")]
    public bool Families { get; set; } = true;

    [Opt("Ambientes (não colocados, não fechados, sem nome)", "Rooms (unplaced, not enclosed, unnamed)", Group = "Verificações|Checks")]
    public bool Rooms { get; set; } = true;

    [Opt("Vistas fora de pranchas e pranchas vazias", "Views not on sheets and empty sheets", Group = "Verificações|Checks")]
    public bool Views { get; set; } = true;

    [Opt("Salvar relatório (HTML e Excel)", "Save report (HTML and Excel)", Group = "Saída|Output")]
    public bool Save { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("qa.check", Modules.Quality, "Verificação de qualidade", "Quality check", Order = 10, Large = true,
    DescPt = "Verifica o modelo: regras editáveis de parâmetros/nomes, avisos do Revit, duplicados, famílias no local, CAD importado, ambientes e vistas — com seleção dos problemas.",
    DescEn = "Checks the model: editable parameter/naming rules, Revit warnings, duplicates, in-place families, imported CAD, rooms and views — with problem selection.",
    HelpPt = "As regras ficam no projeto (ou na sua biblioteca) e são editadas em \"Regras de verificação\" (planilha). O relatório pode ser salvo em HTML/Excel para enviar à equipe. Nenhum elemento é alterado.",
    HelpEn = "Rules are stored in the project (or your library) and edited in \"Check rules\" (spreadsheet). The report can be saved as HTML/Excel to share. No element is changed.",
    Keywords = "verificacao qualidade auditoria modelo regras avisos duplicados quality check audit model rules warnings")]
public sealed class QaCheckCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<QaCheckOptions>();
        QaRunner.Run(ctx.Doc, o, ctx.Report);
    }
}

/// <summary>Executa a verificação num documento (usado também no processamento em lote).</summary>
internal static class QaRunner
{
    public static void Run(Document doc, QaCheckOptions o, OperationReport report)
    {
        var summary = new TableData(Loc.T("Resumo da verificação", "Check summary"));
        summary.AddColumn(Loc.T("Verificação", "Check")).AddColumn(Loc.T("Ocorrências", "Occurrences"), ColumnKind.Integer);

        if (o.Rules)
        {
            var set = QaRules.Load(doc);
            var issues = RuleEngine.Run(set, QaRules.Records(doc, set));
            foreach (var g in issues.GroupBy(i => i.Rule.Name))
            {
                foreach (var i in g.Take(300)) report.Add(i.Severity, i.Message, new[] { i.ElementId }, g.Key);
                if (g.Count() > 300) report.Info(Loc.F("{0}: mais {1} ocorrências não listadas.", "{0}: {1} more occurrences not listed.", g.Key, g.Count() - 300));
                summary.AddRowWithIds(g.Select(i => i.ElementId), g.Key, g.Count());
            }
            report.Info(Loc.F("Regras \"{0}\": {1} ativas.", "Rules \"{0}\": {1} active.", set.Name, set.Rules.Count(r => r.Enabled)));
        }
        if (o.Warnings)
        {
            var warnings = doc.GetWarnings();
            foreach (var g in warnings.GroupBy(w => w.GetDescriptionText()).OrderByDescending(g => g.Count()))
            {
                var ids = g.SelectMany(w => w.GetFailingElements().Concat(w.GetAdditionalElements())).Select(id => id.Value).Distinct().ToList();
                report.Warn(Loc.F("Aviso do Revit ({0}×): {1}", "Revit warning ({0}×): {1}", g.Count(), g.Key), ids, Loc.T("Avisos do Revit", "Revit warnings"));
                summary.AddRowWithIds(ids, Loc.T("Aviso: ", "Warning: ") + g.Key, g.Count());
            }
        }
        if (o.Duplicates)
        {
            var dupes = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(f => f.SuperComponent == null && f.Location is LocationPoint)
                .GroupBy(f =>
                {
                    var p = ((LocationPoint)f.Location).Point;
                    return (f.GetTypeId().Value, Math.Round(p.X, 2), Math.Round(p.Y, 2), Math.Round(p.Z, 2), Math.Round(((LocationPoint)f.Location).Rotation, 3));
                })
                .Where(g => g.Count() > 1).ToList();
            foreach (var g in dupes)
                report.Warn(Loc.F("{0} cópias sobrepostas de \"{1}\"", "{0} overlapping copies of \"{1}\"", g.Count(), Lookup.ElementLabel(g.First())), g.Select(f => f.Id.Value), Loc.T("Duplicados", "Duplicates"));
            summary.AddRowWithIds(dupes.SelectMany(g => g.Skip(1)).Select(f => f.Id.Value), Loc.T("Elementos duplicados (excedentes)", "Duplicated elements (extra)"), dupes.Sum(g => g.Count() - 1));
        }
        if (o.Families)
        {
            var inplace = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().Where(f => f.IsInPlace).ToList();
            foreach (var f in inplace) report.Info(Loc.F("Família no local: {0}", "In-place family: {0}", f.Name), f.Id.Value);
            summary.AddRowWithIds(inplace.Select(f => f.Id.Value), Loc.T("Famílias no local", "In-place families"), inplace.Count);
            var imports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().Where(i => !i.IsLinked).ToList();
            foreach (var i in imports) report.Warn(Loc.F("CAD importado (não vinculado): {0}", "Imported CAD (not linked): {0}", i.Category?.Name ?? i.Name), new[] { i.Id.Value }, "CAD");
            summary.AddRowWithIds(imports.Select(i => i.Id.Value), Loc.T("CAD importado", "Imported CAD"), imports.Count);
        }
        if (o.Rooms)
        {
            var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).OfType<Room>().ToList();
            var unplaced = rooms.Where(r => r.Location == null).ToList();
            var unenclosed = rooms.Where(r => r.Location != null && r.Area <= 0).ToList();
            foreach (var r in unplaced) report.Warn(Loc.F("Ambiente não colocado: {0} {1}", "Unplaced room: {0} {1}", r.Number, r.Name), new[] { r.Id.Value }, Loc.T("Ambientes", "Rooms"));
            foreach (var r in unenclosed) report.Error(Loc.F("Ambiente não fechado/redundante: {0} {1}", "Not enclosed/redundant room: {0} {1}", r.Number, r.Name), new[] { r.Id.Value }, Loc.T("Ambientes", "Rooms"));
            summary.AddRowWithIds(unplaced.Select(r => r.Id.Value), Loc.T("Ambientes não colocados", "Unplaced rooms"), unplaced.Count);
            summary.AddRowWithIds(unenclosed.Select(r => r.Id.Value), Loc.T("Ambientes não fechados", "Not enclosed rooms"), unenclosed.Count);
        }
        if (o.Views)
        {
            var placed = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.ViewId.Value).ToHashSet();
            var notPlaced = Lookup.Collect<View>(doc).Where(v => !v.IsTemplate && v.CanBePrinted && v is not ViewSheet && v is not ViewSchedule
                                                                && v.ViewType is not (ViewType.ProjectBrowser or ViewType.SystemBrowser or ViewType.Internal) && !placed.Contains(v.Id.Value)).ToList();
            summary.AddRowWithIds(notPlaced.Select(v => v.Id.Value), Loc.T("Vistas fora de pranchas", "Views not on sheets"), notPlaced.Count);
            var empty = Lookup.Collect<ViewSheet>(doc).Where(s => !s.IsPlaceholder && s.GetAllPlacedViews().Count == 0).ToList();
            foreach (var s in empty) report.Info(Loc.F("Prancha vazia: {0} - {1}", "Empty sheet: {0} - {1}", s.SheetNumber, s.Name), s.Id.Value);
            summary.AddRowWithIds(empty.Select(s => s.Id.Value), Loc.T("Pranchas vazias", "Empty sheets"), empty.Count);
        }
        report.Tables.Insert(0, summary);
        if (!report.HasErrors && !report.HasWarnings) report.Success(Loc.T("Nenhum problema encontrado pelas verificações escolhidas.", "No problem found by the chosen checks."));
        if (o.Save)
        {
            try
            {
                var title = TextUtil.SafeFileName(string.IsNullOrWhiteSpace(doc.Title) ? "projeto" : doc.Title);
                var html = Path.Combine(Paths.Exports, $"{title} - verificacao {DateTime.Now:yyyyMMdd-HHmm}.html");
                HtmlReportWriter.Write(html, Loc.T("Verificação de qualidade", "Quality check"), doc.Title, report, report.Tables);
                XlsxWriter.Write(Path.ChangeExtension(html, ".xlsx"), report.Tables);
                report.Success(Loc.F("Relatório salvo: {0}", "Report saved: {0}", html));
            }
            catch (Exception ex)
            {
                report.Error(ex.Message);
            }
        }
        report.ForceShow = true;
    }
}

public enum RulesAction
{
    [Text("Exportar regras atuais para planilha (editar no Excel)", "Export current rules to spreadsheet (edit in Excel)")] Export,
    [Text("Importar regras de planilha (CSV)", "Import rules from spreadsheet (CSV)")] Import,
    [Text("Restaurar regras padrão", "Restore default rules")] Reset,
    [Text("Mostrar regras atuais", "Show current rules")] Show,
}

public sealed class QaRulesOptions
{
    [Opt("Ação", "Action")]
    public RulesAction Action { get; set; } = RulesAction.Export;

    [Opt("Arquivo CSV", "CSV file", Kind = OptKind.File, Filter = "CSV|*.csv", VisibleIf = "Action=Import")]
    public string File { get; set; } = "";

    [Opt("Salvar também como padrão pessoal (outros projetos)", "Also save as personal default (other projects)", VisibleIf = "Action=Import,Reset")]
    public bool AsDefault { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("qa.rules", Modules.Quality, "Regras de verificação", "Check rules", Order = 11,
    DescPt = "Edita as regras da verificação de qualidade numa planilha: obrigatório, faixa de valores, padrão de texto, lista permitida, valor único e padrão de nome.",
    DescEn = "Edits the quality check rules in a spreadsheet: required, value range, text pattern, allowed list, unique value and naming pattern.",
    HelpPt = "Colunas: Ativa (1/0); Nome; Severidade (Erro/Aviso/Info); Categorias (ex.: OST_Doors,OST_Windows); FiltroFamilia (expressão); Tipo (Obrigatorio, Faixa, Padrao, ValoresPermitidos, Unico, PadraoNome); Parametro (nome ou parâmetro interno, ex.: ALL_MODEL_MARK); ParametroDeTipo (1/0); Min/Max (comprimentos em mm, áreas em m²); Padrao (expressão regular); Valores (separados por |); Mensagem.",
    HelpEn = "Columns: Active (1/0); Name; Severity (Error/Warning/Info); Categories; FamilyFilter (regex); Kind (Required, Range, Pattern, AllowedValues, Unique, NamePattern); Parameter; TypeParameter (1/0); Min/Max (lengths in mm, areas in m²); Pattern (regex); Values (separated by |); Message.",
    Keywords = "regras verificacao qualidade parametros planilha rules quality check parameters spreadsheet")]
public sealed class QaRulesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<QaRulesOptions>();
        var doc = ctx.Doc;
        var userFile = Path.Combine(Paths.Libraries, "regras-verificacao.json");
        switch (o.Action)
        {
            case RulesAction.Export:
            {
                var path = Path.Combine(Paths.Exports, "regras-verificacao.csv");
                CsvWriter.Write(QaRules.ToTable(QaRules.Load(doc)), path, ctx.Settings.CsvSeparator);
                ctx.Report.Success(Loc.F("Regras exportadas: {0}. Edite e use \"Importar\".", "Rules exported: {0}. Edit and use \"Import\".", path));
                MatLib.OpenFolder(path);
                break;
            }
            case RulesAction.Import:
            {
                if (!System.IO.File.Exists(o.File)) ToolContext.Fail("Escolha o arquivo CSV.", "Choose the CSV file.");
                var set = QaRules.FromCsv(System.IO.File.ReadAllText(o.File), Path.GetFileNameWithoutExtension(o.File));
                ctx.Modify(ctx.Tool.Name, () => ProjectStorage.SetSection(doc, QaRules.Key, set));
                if (o.AsDefault) Json.Save(userFile, set);
                ctx.Report.Success(Loc.F("{0} regras importadas para o projeto.", "{0} rules imported into the project.", set.Rules.Count));
                break;
            }
            case RulesAction.Reset:
            {
                var set = RuleSet.Default();
                ctx.Modify(ctx.Tool.Name, () => ProjectStorage.SetSection(doc, QaRules.Key, set));
                if (o.AsDefault) Json.Save(userFile, set);
                ctx.Report.Success(Loc.T("Regras padrão restauradas.", "Default rules restored."));
                break;
            }
        }
        ctx.Report.Tables.Add(QaRules.ToTable(QaRules.Load(doc)));
        ctx.Report.ForceShow = true;
    }
}
