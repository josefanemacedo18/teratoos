using TeraTools.Core.Export;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Reporting;

public enum Severity
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed class ReportItem
{
    public Severity Severity { get; init; }
    public string Message { get; init; } = "";
    public List<long> ElementIds { get; init; } = new();
    public string? Group { get; init; }
}

/// <summary>
/// Relatório de execução de uma ferramenta: mensagens, contadores, elementos afetados e tabelas.
/// É exibido ao final da operação e também alimenta o histórico.
/// </summary>
public sealed class OperationReport
{
    private readonly object _lock = new();

    public string Title { get; set; } = "";
    public string ToolId { get; set; } = "";
    public DateTime Started { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }
    public List<ReportItem> Items { get; } = new();

    /// <summary>Contadores na ordem de inserção (ex.: "Criados" → 120).</summary>
    public List<KeyValuePair<string, int>> Counters { get; } = new();

    public List<TableData> Tables { get; } = new();

    /// <summary>Elementos resultantes (para selecionar ao final).</summary>
    public HashSet<long> ResultElementIds { get; } = new();

    /// <summary>Força exibir o relatório mesmo sem avisos.</summary>
    public bool ForceShow { get; set; }

    public bool HasErrors => Items.Any(i => i.Severity == Severity.Error);
    public bool HasWarnings => Items.Any(i => i.Severity == Severity.Warning);
    public int WarningCount => Items.Count(i => i.Severity == Severity.Warning);
    public int ErrorCount => Items.Count(i => i.Severity == Severity.Error);
    public bool IsEmpty => Items.Count == 0 && Counters.Count == 0 && Tables.Count == 0;

    public void Add(Severity s, string message, IEnumerable<long>? ids = null, string? group = null)
    {
        lock (_lock)
            Items.Add(new ReportItem { Severity = s, Message = message, ElementIds = ids?.ToList() ?? new(), Group = group });
    }

    public void Info(string message, params long[] ids) => Add(Severity.Info, message, ids);
    public void Success(string message, params long[] ids) => Add(Severity.Success, message, ids);
    public void Warn(string message, params long[] ids) => Add(Severity.Warning, message, ids);
    public void Error(string message, params long[] ids) => Add(Severity.Error, message, ids);

    public void Info(string message, IEnumerable<long> ids, string? group = null) => Add(Severity.Info, message, ids, group);
    public void Warn(string message, IEnumerable<long> ids, string? group = null) => Add(Severity.Warning, message, ids, group);
    public void Error(string message, IEnumerable<long> ids, string? group = null) => Add(Severity.Error, message, ids, group);

    public void Count(string key, int add = 1)
    {
        lock (_lock)
        {
            var idx = Counters.FindIndex(c => c.Key == key);
            if (idx < 0) Counters.Add(new KeyValuePair<string, int>(key, add));
            else Counters[idx] = new KeyValuePair<string, int>(key, Counters[idx].Value + add);
        }
    }

    public int GetCount(string key) => Counters.FirstOrDefault(c => c.Key == key).Value;

    public void Created(int n = 1) => Count(Loc.T("Criados", "Created"), n);
    public void Modified(int n = 1) => Count(Loc.T("Modificados", "Modified"), n);
    public void Deleted(int n = 1) => Count(Loc.T("Excluídos", "Deleted"), n);
    public void Skipped(int n = 1) => Count(Loc.T("Ignorados", "Skipped"), n);

    public void AddResult(long id) => ResultElementIds.Add(id);
    public void AddResults(IEnumerable<long> ids)
    {
        foreach (var id in ids) ResultElementIds.Add(id);
    }

    /// <summary>Resumo curto em uma linha.</summary>
    public string Summary()
    {
        var parts = Counters.Where(c => c.Value != 0).Select(c => $"{c.Key}: {c.Value}").ToList();
        if (ErrorCount > 0) parts.Add(Loc.F("{0} erro(s)", "{0} error(s)", ErrorCount));
        if (WarningCount > 0) parts.Add(Loc.F("{0} aviso(s)", "{0} warning(s)", WarningCount));
        return parts.Count == 0 ? Loc.T("Concluído.", "Done.") : string.Join(" · ", parts);
    }

    /// <summary>Indica se vale a pena abrir a janela de relatório.</summary>
    public bool ShouldShow => ForceShow || HasErrors || HasWarnings || Tables.Count > 0 || Items.Count > 1;
}
