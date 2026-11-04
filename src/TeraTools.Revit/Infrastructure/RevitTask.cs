using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Fila de ações executadas no contexto da API do Revit (via ExternalEvent).
/// Usada pelo painel lateral e janelas não modais (ex.: selecionar elementos de um relatório).
/// </summary>
public static class RevitTask
{
    private static ExternalEvent? _event;
    private static readonly ConcurrentQueue<Action<UIApplication>> Queue = new();

    private sealed class Handler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            while (Queue.TryDequeue(out var action))
            {
                try
                {
                    action(app);
                }
                catch (Exception ex)
                {
                    Log.Error("Falha em ação agendada", ex);
                    Ui.Msg.Error(ex.Message);
                }
            }
        }

        public string GetName() => "Téra Tools";
    }

    /// <summary>Deve ser chamado em OnStartup (contexto válido da API).</summary>
    public static void Initialize() => _event ??= ExternalEvent.Create(new Handler());

    public static void Run(Action<UIApplication> action)
    {
        Queue.Enqueue(action);
        if (_event == null)
        {
            Log.Warn("RevitTask não inicializado.");
            return;
        }
        var r = _event.Raise();
        if (r != ExternalEventRequest.Accepted && r != ExternalEventRequest.Pending)
            Log.Warn("ExternalEvent recusado: " + r);
    }

    /// <summary>Executa uma ferramenta pelo id (a partir do painel).</summary>
    public static void RunTool(string toolId)
    {
        var info = ToolCatalog.Get(toolId);
        if (info == null) return;
        Run(app =>
        {
            var cmd = (ToolCommand)Activator.CreateInstance(info.Type)!;
            var msg = "";
            ToolRunner.Run(cmd, app, ref msg);
        });
    }
}
