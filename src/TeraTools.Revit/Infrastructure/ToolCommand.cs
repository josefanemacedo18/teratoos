using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Classe base de todos os comandos. A ferramenta implementa apenas <see cref="Run"/>;
/// erros, cancelamento, histórico, relatório e seleção de resultados são tratados aqui.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public abstract class ToolCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) =>
        ToolRunner.Run(this, commandData.Application, ref message);

    public abstract void Run(ToolContext ctx);
}

public static class ToolRunner
{
    public static Result Run(ToolCommand cmd, UIApplication app, ref string message)
    {
        var info = ToolCatalog.Get(cmd.GetType());
        var ctx = new ToolContext(app, info);
        var sw = Stopwatch.StartNew();
        var status = RunStatus.Succeeded;
        string? errorDetail = null;
        Log.Info($"Executando {info.Id}");
        try
        {
            if (info.RequiresDocument && app.ActiveUIDocument == null)
            {
                Msg.Info(Loc.T("Abra um projeto antes de usar esta ferramenta.", "Open a project before using this tool."));
                return Result.Cancelled;
            }
            cmd.Run(ctx);
            if (ctx.Report.HasErrors || ctx.Report.HasWarnings) status = RunStatus.Warning;
        }
        catch (OperationCanceledException)
        {
            status = RunStatus.Cancelled;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            status = RunStatus.Cancelled;
        }
        catch (ToolException ex)
        {
            status = RunStatus.Failed;
            errorDetail = ex.Message;
            Log.Warn($"{info.Id}: {ex.Message}");
            Msg.Warn(ex.Message, info.Name);
        }
        catch (Exception ex)
        {
            status = RunStatus.Failed;
            errorDetail = ex.ToString();
            Log.Error($"Falha em {info.Id}", ex);
            try
            {
                ErrorWindow.ShowError(info, ex, app.MainWindowHandle);
            }
            catch (Exception ex2)
            {
                Log.Error("Falha ao mostrar a janela de erro", ex2);
                try { TaskDialog.Show("Téra Tools", ex.Message); } catch { /* sem interface */ }
            }
        }
        finally
        {
            sw.Stop();
            ctx.Report.Duration = sw.Elapsed;
            Finish(ctx, info, status, errorDetail, sw.ElapsedMilliseconds);
        }
        return status == RunStatus.Cancelled ? Result.Cancelled : Result.Succeeded;
    }

    private static void Finish(ToolContext ctx, ToolInfo info, RunStatus status, string? error, long ms)
    {
        try
        {
            if (info.Module != Modules.General || info.Id is not ("general.pane" or "general.settings"))
                Services.Settings.TouchRecent(info.Id);
            Services.History.Append(new HistoryEntry
            {
                ToolId = info.Id,
                ToolName = info.Name.Pt,
                Document = ctx.HasDocument ? ctx.Doc.Title : null,
                Status = status,
                DurationMs = ms,
                Summary = status == RunStatus.Cancelled ? Loc.T("Cancelado", "Cancelled") : ctx.Report.Summary(),
                Warnings = ctx.Report.WarningCount,
                Errors = ctx.Report.ErrorCount,
                ErrorDetail = error,
            });
            Services.SaveSettings();

            if (status == RunStatus.Cancelled || status == RunStatus.Failed) return;

            if (ctx.HasDocument && Services.Settings.SelectResults && ctx.Report.ResultElementIds.Count > 0)
            {
                var ids = ctx.Report.ResultElementIds.Select(v => new ElementId(v))
                    .Where(id => ctx.Doc.GetElement(id) != null).ToList();
                if (ids.Count > 0) ctx.UiDoc.Selection.SetElementIds(ids);
            }
            if (Services.Settings.ShowReportAfterRun && ctx.Report.ShouldShow)
                ReportWindow.ShowReport(ctx.Report, info, ctx.App.MainWindowHandle);
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao finalizar ferramenta", ex);
        }
    }
}
