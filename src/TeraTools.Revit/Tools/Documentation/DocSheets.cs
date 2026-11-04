using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Documentation;

internal static class SheetUtil
{
    public static List<ViewSheet> All(Document doc) =>
        Lookup.Collect<ViewSheet>(doc).Where(s => !s.IsPlaceholder).OrderBy(s => s.SheetNumber, TextUtil.NaturalComparer).ToList();

    public static string Label(ViewSheet s) => $"{s.SheetNumber} - {s.Name}";

    public static List<ViewSheet> Pick(ToolContext ctx, string promptPt, string promptEn, bool preselectAll = false)
    {
        var selected = ctx.Selected(e => e is ViewSheet).Select(e => e.Id.Value).ToHashSet();
        var sheets = Ui.Pick.Many(All(ctx.Doc), Label, ctx.Tool.Name, Loc.T(promptPt, promptEn), ctx.Owner,
            s => preselectAll && selected.Count == 0 || selected.Contains(s.Id.Value) || s.Id == ctx.ActiveView.Id);
        if (sheets.Count == 0) ctx.Cancel();
        return sheets;
    }

    public static bool NumberExists(Document doc, string number) =>
        Lookup.Collect<ViewSheet>(doc).Any(s => string.Equals(s.SheetNumber, number, StringComparison.OrdinalIgnoreCase));
}

public sealed class SheetsOptions
{
    [Opt("Folhas (Número | Nome)", "Sheets (Number | Name)", Kind = OptKind.MultiLine)]
    public List<string> Sheets { get; set; } = new()
    {
        "A01 | Planta de layout", "A02 | Planta de demolir e construir", "A03 | Planta de pontos elétricos e iluminação", "A04 | Planta de forro",
        "A05 | Paginação de piso", "A06 | Elevações e detalhes", "A07 | Marcenaria", "A08 | Perspectivas",
    };

    [Opt("Ou importar de planilha CSV (Número;Nome;outros parâmetros...)", "Or import from CSV (Number;Name;other parameters...)", Kind = OptKind.File, Filter = "CSV|*.csv",
        Tip = "Colunas extras com o nome de um parâmetro da folha são preenchidas (ex.: Desenhado por).|Extra columns named after a sheet parameter are filled (e.g. Drawn By).")]
    public string Csv { get; set; } = "";

    [Opt("Carimbo", "Title block", Kind = OptKind.TitleBlock)]
    public string TitleBlock { get; set; } = "";

    [Opt("Pular números que já existem", "Skip numbers that already exist")]
    public bool SkipExisting { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.sheets", Modules.Documentation, "Criar pranchas em lote", "Create sheets in batch", Order = 20, Large = true, Group = "Pranchas|Sheets",
    DescPt = "Cria as pranchas do projeto a partir de uma lista ou planilha (número, nome e outros parâmetros), com o carimbo escolhido.",
    DescEn = "Creates the project sheets from a list or spreadsheet (number, name and other parameters), with the chosen title block.",
    Keywords = "criar pranchas folhas lote lista planilha create sheets batch list")]
public sealed class SheetsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<SheetsOptions>();
        var doc = ctx.Doc;
        var rows = new List<string[]>();
        string[]? header = null;
        if (!string.IsNullOrWhiteSpace(o.Csv))
        {
            if (!File.Exists(o.Csv)) ToolContext.Fail("Arquivo CSV não encontrado.", "CSV file not found.");
            var all = CsvWriter.Parse(File.ReadAllText(o.Csv));
            if (all.Count > 0)
            {
                header = all[0];
                rows.AddRange(all.Skip(1));
            }
        }
        else
        {
            rows.AddRange(o.Sheets.Select(l => l.Split('|').Select(p => p.Trim()).ToArray()).Where(p => p.Length > 0 && p[0].Length > 0));
        }
        var tb = SheetPlacer.TitleBlockId(doc, o.TitleBlock);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var r in rows)
            {
                var number = r[0];
                var name = r.Length > 1 ? r[1] : "";
                if (SheetUtil.NumberExists(doc, number))
                {
                    if (o.SkipExisting)
                    {
                        ctx.Report.Skipped();
                        continue;
                    }
                    number += "-" + Guid.NewGuid().ToString("N")[..3];
                }
                var s = ViewSheet.Create(doc, tb);
                s.SheetNumber = number;
                if (name.Length > 0) s.Name = name;
                if (header != null)
                {
                    for (var c = 2; c < header.Length && c < r.Length; c++)
                    {
                        var p = s.LookupParameter(header[c].Trim());
                        if (p == null || p.IsReadOnly || string.IsNullOrWhiteSpace(r[c])) continue;
                        try
                        {
                            if (p.StorageType == StorageType.String) p.Set(r[c]);
                            else p.SetValueString(r[c]);
                        }
                        catch
                        {
                            ctx.Report.Warn(Loc.F("{0}: valor inválido para {1}.", "{0}: invalid value for {1}.", number, header[c]));
                        }
                    }
                }
                ctx.Report.Created();
                ctx.Report.AddResult(s.Id.Value);
            }
        });
    }
}

public enum PlaceTarget
{
    [Text("Prancha existente", "Existing sheet")] Existing,
    [Text("Novas pranchas (criadas conforme necessário)", "New sheets (created as needed)")] NewSheets,
}

public sealed class PlaceViewsOptions
{
    [Opt("Destino", "Target")]
    public PlaceTarget Target { get; set; } = PlaceTarget.NewSheets;

    [Opt("Prancha", "Sheet", Kind = OptKind.View, Filter = "DrawingSheet", VisibleIf = "Target=Existing")]
    public string Sheet { get; set; } = "";

    [Opt("Carimbo das novas pranchas", "Title block for new sheets", Kind = OptKind.TitleBlock, VisibleIf = "Target=NewSheets")]
    public string TitleBlock { get; set; } = "";

    [Opt("Prefixo do número", "Number prefix", VisibleIf = "Target=NewSheets")]
    public string Prefix { get; set; } = "A";

    [Opt("Número inicial", "Start number", Min = 0, VisibleIf = "Target=NewSheets")]
    public int Start { get; set; } = 1;

    [Opt("Dígitos", "Digits", Min = 1, Max = 5, VisibleIf = "Target=NewSheets")]
    public int Digits { get; set; } = 2;

    [Opt("Nome das novas pranchas", "New sheet name", VisibleIf = "Target=NewSheets", Tip = "Marcador: {Vista} (primeira vista).|Token: {Vista} (first view).")]
    public string SheetName { get; set; } = "{Vista}";

    [Opt("Espaço entre vistas (no papel)", "Gap between views (on paper)", Kind = OptKind.Length, Min = 0)]
    public double Gap { get; set; } = U.Mm(10);

    [Opt("Margem (no papel)", "Margin (on paper)", Kind = OptKind.Length, Min = 0)]
    public double Margin { get; set; } = U.Mm(15);

    [Opt("Reserva à direita para o carimbo", "Right reserve for the title block", Kind = OptKind.Length, Min = 0)]
    public double RightReserve { get; set; } = U.Mm(0);
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.placeviews", Modules.Documentation, "Montar pranchas automaticamente", "Lay out sheets automatically", Order = 21, Group = "Pranchas|Sheets",
    DescPt = "Coloca as vistas escolhidas nas pranchas sem sobreposição, usando o tamanho real de cada vista; cria novas pranchas quando não couber.",
    DescEn = "Places the chosen views on sheets without overlap, using each view's real size; creates new sheets when they don't fit.",
    Keywords = "montar pranchas colocar vistas organizar layout automatico place views sheets layout")]
public sealed class PlaceViewsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var placed = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.ViewId.Value).ToHashSet();
        var candidates = Lookup.Collect<View>(doc)
            .Where(v => !v.IsTemplate && v.CanBePrinted && v is not ViewSheet && v is not ViewSchedule && (v.ViewType == ViewType.Legend || !placed.Contains(v.Id.Value)))
            .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name, TextUtil.NaturalComparer).ToList();
        var selected = ctx.Selected(e => e is View).Select(e => e.Id.Value).ToHashSet();
        var views = Pick.Many(candidates, v => v.Name, ctx.Tool.Name, Loc.T("Quais vistas colocar? (apenas vistas ainda fora de pranchas)", "Which views to place? (only views not yet on sheets)"),
            ctx.Owner, v => selected.Contains(v.Id.Value), v => v.ViewType.ToString());
        if (views.Count == 0) ctx.Cancel();
        var o = ctx.Ask<PlaceViewsOptions>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.Target == PlaceTarget.Existing)
            {
                var sheet = Lookup.Collect<ViewSheet>(doc).FirstOrDefault(s => s.Name == o.Sheet || SheetUtil.Label(s) == o.Sheet)
                            ?? throw new ToolException(Loc.T("Escolha a prancha.", "Choose the sheet."));
                var rest = SheetPlacer.Place(doc, sheet, views, o.Gap, SheetPlacer.UsableArea(sheet, o.Margin, o.RightReserve));
                ctx.Report.Count(Loc.T("Vistas colocadas", "Views placed"), views.Count - rest.Count);
                foreach (var v in rest) ctx.Report.Warn(Loc.F("\"{0}\" não coube na prancha.", "\"{0}\" did not fit on the sheet.", v.Name), v.Id.Value);
                ctx.Report.AddResult(sheet.Id.Value);
                return;
            }
            var tb = SheetPlacer.TitleBlockId(doc, o.TitleBlock);
            var seq = new NumberingSequence { Prefix = o.Prefix, Start = o.Start, Digits = o.Digits };
            var remaining = new List<View>(views);
            var n = 0;
            var guard = 0;
            while (remaining.Count > 0 && guard++ < 500)
            {
                string number;
                do number = seq.Format(n++);
                while (SheetUtil.NumberExists(doc, number) && n < 100000);
                var sheet = ViewSheet.Create(doc, tb);
                sheet.SheetNumber = number;
                sheet.Name = DocUtil.Name(o.SheetName, new Dictionary<string, object?> { ["Vista"] = remaining[0].Name });
                var rest = SheetPlacer.Place(doc, sheet, remaining, o.Gap, SheetPlacer.UsableArea(sheet, o.Margin, o.RightReserve));
                if (rest.Count == remaining.Count)
                {
                    // nem a primeira coube: coloca sozinha, centralizada, e avisa
                    var first = remaining[0];
                    if (Viewport.CanAddViewToSheet(doc, sheet.Id, first.Id))
                    {
                        var a = SheetPlacer.UsableArea(sheet, o.Margin, o.RightReserve);
                        Viewport.Create(doc, sheet.Id, first.Id, new XYZ(a.Center.X, a.Center.Y, 0));
                        ctx.Report.Warn(Loc.F("\"{0}\" é maior que a área útil da prancha {1}.", "\"{0}\" is larger than the usable area of sheet {1}.", first.Name, number), first.Id.Value);
                    }
                    rest = remaining.Skip(1).ToList();
                }
                ctx.Report.Count(Loc.T("Pranchas criadas", "Sheets created"));
                ctx.Report.AddResult(sheet.Id.Value);
                remaining = rest;
            }
        });
    }
}

public enum RenumberMode
{
    [Text("Números das pranchas", "Sheet numbers")] Sheets,
    [Text("Números de detalhe das vistas na prancha ativa (por posição)", "Detail numbers of views on the active sheet (by position)")] Details,
}

public sealed class RenumberOptions
{
    [Opt("O que renumerar", "What to renumber")]
    public RenumberMode Mode { get; set; } = RenumberMode.Sheets;

    [Opt("Prefixo", "Prefix")]
    public string Prefix { get; set; } = "A";

    [Opt("Início", "Start", Min = 0)]
    public int Start { get; set; } = 1;

    [Opt("Passo", "Step", Min = 1)]
    public int Step { get; set; } = 1;

    [Opt("Dígitos", "Digits", Min = 1, Max = 6)]
    public int Digits { get; set; } = 2;

    [Opt("Sufixo", "Suffix")]
    public string Suffix { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.renumber", Modules.Documentation, "Renumerar pranchas e detalhes", "Renumber sheets and details", Order = 22, Group = "Pranchas|Sheets",
    DescPt = "Renumera pranchas em sequência (na ordem escolhida) ou os números de detalhe das vistas da prancha ativa, da esquerda para a direita e de cima para baixo.",
    DescEn = "Renumbers sheets in sequence (in the chosen order) or the detail numbers of views on the active sheet, left to right and top to bottom.",
    Keywords = "renumerar pranchas folhas detalhes sequencia renumber sheets detail numbers")]
public sealed class RenumberCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<RenumberOptions>();
        var seq = new NumberingSequence { Prefix = o.Prefix, Start = o.Start, Step = o.Step, Digits = o.Digits, Suffix = o.Suffix };
        if (o.Mode == RenumberMode.Sheets)
        {
            var sheets = SheetUtil.Pick(ctx, "Quais pranchas renumerar (na ordem atual)?", "Which sheets to renumber (in current order)?", preselectAll: true);
            ctx.Modify(ctx.Tool.Name, () =>
            {
                foreach (var s in sheets) s.SheetNumber = "~tt~" + Guid.NewGuid().ToString("N")[..10];
                var i = 0;
                foreach (var s in sheets)
                {
                    string number;
                    do number = seq.Format(i++);
                    while (SheetUtil.NumberExists(doc, number) && i < 100000);
                    s.SheetNumber = number;
                    ctx.Report.Modified();
                }
            });
            return;
        }
        if (ctx.ActiveView is not ViewSheet sheet) throw new ToolException(Loc.T("Abra a prancha cujas vistas serão renumeradas.", "Open the sheet whose views will be renumbered."));
        var vps = sheet.GetAllViewports().Select(id => (Viewport)doc.GetElement(id)).ToList();
        var ordered = vps.Select(v => (Vp: v, C: v.GetBoxCenter())).OrderByDescending(x => Math.Round(x.C.Y / U.Mm(40))).ThenBy(x => x.C.X).Select(x => x.Vp).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var v in ordered) RevitGeo.Set(v, BuiltInParameter.VIEWPORT_DETAIL_NUMBER, "~" + Guid.NewGuid().ToString("N")[..6]);
            var i = 0;
            foreach (var v in ordered)
                if (RevitGeo.Set(v, BuiltInParameter.VIEWPORT_DETAIL_NUMBER, seq.Format(i++))) ctx.Report.Modified();
        });
    }
}

