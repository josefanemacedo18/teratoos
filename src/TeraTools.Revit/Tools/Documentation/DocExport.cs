using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Materials;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Documentation;

internal static class ExportUtil
{
    public static string Folder(ToolContext ctx, string? chosen, string sub)
    {
        var f = string.IsNullOrWhiteSpace(chosen) ? Path.Combine(Paths.Exports, sub) : chosen;
        Directory.CreateDirectory(f);
        ctx.Settings.LastExportFolder = f;
        Services.SaveSettings();
        return f;
    }

    public static string FileName(Document doc, View v, string template)
    {
        var values = new Dictionary<string, object?>
        {
            ["Numero"] = v is ViewSheet s ? s.SheetNumber : "",
            ["Nome"] = v.Name,
            ["Projeto"] = RevitGeo.Text(doc.ProjectInformation, BuiltInParameter.PROJECT_NAME) is { Length: > 0 } pn ? pn : doc.Title,
            ["NumeroProjeto"] = RevitGeo.Text(doc.ProjectInformation, BuiltInParameter.PROJECT_NUMBER),
            ["Revisao"] = v is ViewSheet s2 ? RevitGeo.Text(s2, BuiltInParameter.SHEET_CURRENT_REVISION) : "",
            ["Data"] = DateTime.Now.ToString("yyyy-MM-dd"),
        };
        var name = NameTemplate.Apply(template, values).Trim(' ', '-', '_');
        return TextUtil.SafeFileName(name.Length > 0 ? name : v.Name);
    }

    /// <summary>Pranchas e/ou vistas para exportação (pranchas pré-selecionadas pela seleção atual).</summary>
    public static List<View> PickViews(ToolContext ctx, bool includeViews)
    {
        var doc = ctx.Doc;
        var selected = ctx.Selected(e => e is View).Select(e => e.Id.Value).ToHashSet();
        var items = SheetUtil.All(doc).Cast<View>().ToList();
        if (includeViews)
            items.AddRange(Lookup.Collect<View>(doc).Where(v => !v.IsTemplate && v.CanBePrinted && v is not ViewSheet && v is not ViewSchedule)
                .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name, TextUtil.NaturalComparer));
        var chosen = Pick.Many(items, v => v is ViewSheet s ? SheetUtil.Label(s) : v.Name, ctx.Tool.Name,
            Loc.T("O que exportar?", "What to export?"), ctx.Owner, v => selected.Contains(v.Id.Value), v => v is ViewSheet ? Loc.T("Prancha", "Sheet") : v.ViewType.ToString());
        if (chosen.Count == 0) ctx.Cancel();
        return chosen;
    }
}

public enum PdfQuality
{
    [Text("Normal (300 dpi)", "Normal (300 dpi)")] Dpi300,
    [Text("Alta (600 dpi)", "High (600 dpi)")] Dpi600,
    [Text("Rascunho (144 dpi)", "Draft (144 dpi)")] Dpi144,
}

public enum PdfColor
{
    [Text("Colorido", "Color")] Color,
    [Text("Tons de cinza", "Grayscale")] Gray,
    [Text("Preto e branco", "Black lines")] Black,
}

public sealed class PdfOptions
{
    [Opt("Pasta de destino", "Destination folder", Kind = OptKind.Folder, Tip = "Vazio = pasta de exportações do Téra Tools (PDF).|Empty = Téra Tools exports folder (PDF).")]
    public string Folder { get; set; } = "";

    [Opt("Modelo do nome do arquivo", "File name template", Tip = "Marcadores: {Numero} {Nome} {Projeto} {NumeroProjeto} {Revisao} {Data}.|Tokens: {Numero} {Nome} {Projeto} {NumeroProjeto} {Revisao} {Data}.")]
    public string NameTemplate { get; set; } = "{Numero} - {Nome}";

    [Opt("Um único PDF com tudo", "A single PDF with everything")]
    public bool Combine { get; set; }

    [Opt("Nome do PDF único", "Single PDF name", VisibleIf = "Combine")]
    public string CombinedName { get; set; } = "{Projeto} - Pranchas {Data}";

    [Opt("Qualidade", "Quality")]
    public PdfQuality Quality { get; set; } = PdfQuality.Dpi300;

    [Opt("Cores", "Colors")]
    public PdfColor Color { get; set; } = PdfColor.Color;

    [Opt("Ocultar recortes, caixas de escopo e planos de referência", "Hide crop boundaries, scope boxes and reference planes")]
    public bool HideHelpers { get; set; } = true;

    [Opt("Abrir a pasta ao terminar", "Open the folder when done")]
    public bool OpenFolder { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.pdf", Modules.Documentation, "Exportar PDF", "Export PDF", Order = 30, Large = true, Group = "Exportação|Export",
    DescPt = "Exporta pranchas e vistas para PDF (um arquivo por prancha com nome padronizado, ou um único PDF), em cores, cinza ou preto e branco.",
    DescEn = "Exports sheets and views to PDF (one file per sheet with standard naming, or a single PDF), in color, gray or black.",
    Keywords = "exportar pdf pranchas imprimir export pdf sheets print")]
public sealed class PdfCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var views = ExportUtil.PickViews(ctx, includeViews: true);
        var o = ctx.Ask<PdfOptions>();
        var folder = ExportUtil.Folder(ctx, o.Folder, "PDF");
        PDFExportOptions Opts(string name)
        {
            var p = new PDFExportOptions
            {
                FileName = name,
                Combine = true,
                PaperFormat = ExportPaperFormat.Default,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
                PaperPlacement = PaperPlacementType.Center,
                HideCropBoundaries = o.HideHelpers,
                HideScopeBoxes = o.HideHelpers,
                HideReferencePlane = o.HideHelpers,
                HideUnreferencedViewTags = o.HideHelpers,
                ColorDepth = o.Color switch { PdfColor.Gray => ColorDepthType.GrayScale, PdfColor.Black => ColorDepthType.BlackLine, _ => ColorDepthType.Color },
                ExportQuality = o.Quality switch { PdfQuality.Dpi600 => PDFExportQualityType.DPI600, PdfQuality.Dpi144 => PDFExportQualityType.DPI144, _ => PDFExportQualityType.DPI300 },
                StopOnError = false,
            };
            return p;
        }
        if (o.Combine)
        {
            var name = ExportUtil.FileName(doc, views[0], o.CombinedName);
            if (doc.Export(folder, views.Select(v => v.Id).ToList(), Opts(name))) ctx.Report.Success(Path.Combine(folder, name + ".pdf"));
            else ctx.Report.Error(Loc.T("O Revit não conseguiu exportar o PDF.", "Revit could not export the PDF."));
        }
        else
        {
            foreach (var v in views)
            {
                var name = ExportUtil.FileName(doc, v, o.NameTemplate);
                try
                {
                    if (doc.Export(folder, new List<ElementId> { v.Id }, Opts(name))) ctx.Report.Count(Loc.T("PDFs", "PDFs"));
                    else ctx.Report.Warn(Loc.F("Falha ao exportar \"{0}\".", "Failed to export \"{0}\".", v.Name), v.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(v.Name + ": " + ex.Message, v.Id.Value);
                }
            }
            ctx.Report.Success(Loc.F("Arquivos em: {0}", "Files in: {0}", folder));
        }
        if (o.OpenFolder) MatLib.OpenFolder(folder);
    }
}

public sealed class DwgOptions
{
    [Opt("Pasta de destino", "Destination folder", Kind = OptKind.Folder)]
    public string Folder { get; set; } = "";

    [Opt("Modelo do nome do arquivo", "File name template", Tip = "Marcadores: {Numero} {Nome} {Projeto} {NumeroProjeto} {Revisao} {Data}.|Tokens: {Numero} {Nome} {Projeto} {NumeroProjeto} {Revisao} {Data}.")]
    public string NameTemplate { get; set; } = "{Numero} - {Nome}";

    [Opt("Configuração de exportação DWG (vazio = padrão)", "DWG export setup (empty = default)")]
    public string Setup { get; set; } = "";

    [Opt("Vistas da prancha em um único arquivo (sem referências externas)", "Sheet views in a single file (no xrefs)")]
    public bool MergedViews { get; set; } = true;

    [Opt("Abrir a pasta ao terminar", "Open the folder when done")]
    public bool OpenFolder { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.dwg", Modules.Documentation, "Exportar DWG", "Export DWG", Order = 31, Group = "Exportação|Export",
    DescPt = "Exporta pranchas e vistas para DWG com nomes padronizados, usando uma configuração de exportação do projeto.",
    DescEn = "Exports sheets and views to DWG with standard names, using a project export setup.",
    Keywords = "exportar dwg cad autocad export dwg cad")]
public sealed class DwgCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var views = ExportUtil.PickViews(ctx, includeViews: true);
        var o = ctx.LoadOptions<DwgOptions>();
        var setups = ExportDWGSettings.ListNames(doc).ToList();
        if (string.IsNullOrWhiteSpace(o.Setup) && setups.Count > 0) o.Setup = setups[0];
        o = ctx.Ask(o, description: setups.Count > 0 ? Loc.T("Configurações disponíveis: ", "Available setups: ") + string.Join(", ", setups) : null);
        var folder = ExportUtil.Folder(ctx, o.Folder, "DWG");
        DWGExportOptions opts;
        try
        {
            opts = (string.IsNullOrWhiteSpace(o.Setup) ? null : ExportDWGSettings.FindByName(doc, o.Setup)?.GetDWGExportOptions()) ?? new DWGExportOptions();
        }
        catch
        {
            opts = new DWGExportOptions();
        }
        opts.MergedViews = o.MergedViews;
        foreach (var v in views)
        {
            var name = ExportUtil.FileName(doc, v, o.NameTemplate);
            try
            {
                if (doc.Export(folder, name, new List<ElementId> { v.Id }, opts)) ctx.Report.Count("DWG");
                else ctx.Report.Warn(Loc.F("Falha ao exportar \"{0}\".", "Failed to export \"{0}\".", v.Name), v.Id.Value);
            }
            catch (Exception ex)
            {
                ctx.Report.Warn(v.Name + ": " + ex.Message, v.Id.Value);
            }
        }
        ctx.Report.Success(Loc.F("Arquivos em: {0}", "Files in: {0}", folder));
        if (o.OpenFolder) MatLib.OpenFolder(folder);
    }
}
