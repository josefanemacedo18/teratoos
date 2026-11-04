using System.Text;
using System.Text.Json;
using TeraTools.Core.Serialization;

namespace TeraTools.Core.Reporting;

public enum RunStatus
{
    Succeeded,
    Warning,
    Cancelled,
    Failed,
}

/// <summary>Registro de uma operação executada (histórico de operações do plugin).</summary>
public sealed class HistoryEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string? Document { get; set; }
    public RunStatus Status { get; set; }
    public long DurationMs { get; set; }
    public string? Summary { get; set; }
    public int Warnings { get; set; }
    public int Errors { get; set; }
    public string? ErrorDetail { get; set; }
}

/// <summary>Histórico persistido em arquivo JSON Lines (uma operação por linha), com rotação por tamanho.</summary>
public sealed class HistoryLog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _lock = new();

    public HistoryLog(string path, long maxBytes = 2_000_000)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    public string FilePath => _path;

    public event Action<HistoryEntry>? EntryAdded;

    public void Append(HistoryEntry entry)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(_path) && new FileInfo(_path).Length > _maxBytes)
                {
                    var old = _path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_path, old);
                }
                File.AppendAllText(_path, Json.Serialize(entry, indented: false) + "\n", new UTF8Encoding(false));
            }
            catch
            {
                // O histórico nunca deve derrubar uma operação.
            }
        }
        EntryAdded?.Invoke(entry);
    }

    public List<HistoryEntry> ReadLast(int count = 200)
    {
        lock (_lock)
        {
            var list = new List<HistoryEntry>();
            try
            {
                if (!File.Exists(_path)) return list;
                foreach (var line in File.ReadLines(_path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var e = JsonSerializer.Deserialize<HistoryEntry>(line, Json.Options);
                        if (e != null) list.Add(e);
                    }
                    catch (JsonException)
                    {
                        // linha corrompida: ignora
                    }
                }
            }
            catch
            {
                // ignora erros de leitura
            }
            return list.Skip(Math.Max(0, list.Count - count)).Reverse().ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch
            {
                // ignora
            }
        }
    }
}
