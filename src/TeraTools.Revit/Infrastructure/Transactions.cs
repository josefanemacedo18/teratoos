using Autodesk.Revit.DB;
using TeraTools.Core.Reporting;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Pré-processador de falhas: remove avisos (que interromperiam operações em lote com dezenas
/// de janelas) e os registra no relatório da ferramenta. Erros continuam sendo tratados pelo Revit.
/// </summary>
public sealed class WarningCollector : IFailuresPreprocessor
{
    private readonly OperationReport? _report;
    private readonly HashSet<string> _seen = new();

    public WarningCollector(OperationReport? report) => _report = report;

    public int Count { get; private set; }

    public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
    {
        foreach (var f in accessor.GetFailureMessages())
        {
            if (f.GetSeverity() != FailureSeverity.Warning) continue;
            Count++;
            var text = f.GetDescriptionText();
            if (_report != null && _seen.Add(text))
            {
                var ids = f.GetFailingElementIds().Select(id => id.Value);
                _report.Add(Severity.Info, Core.Localization.Loc.F("Aviso do Revit: {0}", "Revit warning: {0}", text), ids);
            }
            accessor.DeleteWarning(f);
        }
        return FailureProcessingResult.Continue;
    }

    public static void Attach(Transaction t, OperationReport? report)
    {
        var opts = t.GetFailureHandlingOptions();
        opts.SetFailuresPreprocessor(new WarningCollector(report));
        opts.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(opts);
    }
}

public static class Tx
{
    /// <summary>Executa uma ação dentro de uma transação, com avisos suprimidos e registrados.</summary>
    public static void Run(Document doc, string name, Action action, OperationReport? report = null)
    {
        if (doc.IsModifiable)
        {
            // Já existe uma transação aberta: executa dentro dela (chamadas aninhadas).
            action();
            return;
        }
        using var t = new Transaction(doc, name);
        WarningCollector.Attach(t, report);
        t.Start();
        try
        {
            action();
            var status = t.Commit();
            if (status != TransactionStatus.Committed)
                throw new ToolException(Core.Localization.Loc.F(
                    "O Revit não aceitou a operação \"{0}\" (estado: {1}).",
                    "Revit did not accept the operation \"{0}\" (status: {1}).", name, status));
        }
        catch
        {
            if (t.HasStarted() && !t.HasEnded()) t.RollBack();
            throw;
        }
    }

    public static T Run<T>(Document doc, string name, Func<T> func, OperationReport? report = null)
    {
        T result = default!;
        Run(doc, name, () => { result = func(); }, report);
        return result;
    }

    /// <summary>Subtransação (dentro de uma transação aberta) que é desfeita se falhar.</summary>
    public static bool TrySub(Document doc, Action action)
    {
        using var st = new SubTransaction(doc);
        st.Start();
        try
        {
            action();
            st.Commit();
            return true;
        }
        catch
        {
            if (st.HasStarted() && !st.HasEnded()) st.RollBack();
            return false;
        }
    }
}

/// <summary>Erro esperado com mensagem clara para o usuário (sem detalhes técnicos).</summary>
public sealed class ToolException : Exception
{
    public ToolException(string message) : base(message) { }
}
