using System.Text;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Log em arquivo diário (%AppData%\TeraTools\logs). Nunca lança exceções.</summary>
public static class Log
{
    private static readonly object Sync = new();

    public static bool Verbose { get; set; }

    public static string CurrentFile => Path.Combine(Paths.Logs, $"TeraTools-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Debug(string message)
    {
        if (Verbose) Write("DEBUG", message);
    }

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(CurrentFile, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // log nunca deve interromper o Revit
        }
    }

    /// <summary>Remove logs com mais de 30 dias.</summary>
    public static void Cleanup()
    {
        try
        {
            foreach (var f in Directory.GetFiles(Paths.Logs, "TeraTools-*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30))
                    File.Delete(f);
        }
        catch
        {
            // ignora
        }
    }
}
