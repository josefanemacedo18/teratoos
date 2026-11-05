using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Serialization;
using TeraTools.Core.Settings;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Filtro de seleção baseado em predicado.</summary>
public sealed class PredicateFilter : ISelectionFilter
{
    private readonly Func<Element, bool> _element;
    private readonly Func<Reference, bool>? _reference;

    public PredicateFilter(Func<Element, bool> element, Func<Reference, bool>? reference = null)
    {
        _element = element;
        _reference = reference;
    }

    public bool AllowElement(Element elem)
    {
        try
        {
            return _element(elem);
        }
        catch
        {
            return false;
        }
    }

    public bool AllowReference(Reference reference, XYZ position) => _reference?.Invoke(reference) ?? true;

    public static PredicateFilter Categories(params BuiltInCategory[] cats)
    {
        var set = cats.Select(c => (long)c).ToHashSet();
        return new PredicateFilter(e => e.Category != null && set.Contains(e.Category.Id.Value));
    }
}

/// <summary>Contexto de execução de uma ferramenta: documento, relatório, opções e utilitários.</summary>
public sealed class ToolContext
{
    public ToolContext(UIApplication app, ToolInfo tool)
    {
        App = app;
        Tool = tool;
        Report = new OperationReport { Title = tool.Name.Value, ToolId = tool.Id };
    }

    public UIApplication App { get; }
    public ToolInfo Tool { get; }
    public OperationReport Report { get; }
    public GlobalSettings Settings => Services.Settings;
    public bool HasDocument => App.ActiveUIDocument != null;

    public UIDocument UiDoc => App.ActiveUIDocument
        ?? throw new ToolException(Loc.T("Nenhum projeto aberto.", "No project open."));

    public Document Doc => UiDoc.Document;
    public View ActiveView => Doc.ActiveView;
    public IntPtr Owner => App.MainWindowHandle;

    public void Cancel() => throw new OperationCanceledException();

    public static void Fail(string pt, string en) => throw new ToolException(Loc.T(pt, en));

    // ------------------------------------------------------------------ Opções

    /// <summary>Carrega as últimas opções usadas (ou padrão).</summary>
    public T LoadOptions<T>(string? key = null) where T : class, new()
    {
        var o = new T();
        if (!Settings.RememberLastOptions) return o;
        try
        {
            var json = Services.Presets.GetLastUsed(key ?? Tool.Id);
            if (!string.IsNullOrWhiteSpace(json)) Json.PopulateObject(o, json);
        }
        catch (Exception ex)
        {
            Log.Warn($"Opções anteriores inválidas para {key ?? Tool.Id}: {ex.Message}");
        }
        return o;
    }

    public void SaveLastOptions<T>(T options, string? key = null) where T : class
    {
        try
        {
            Services.Presets.SetLastUsed(key ?? Tool.Id, Json.Serialize(options, options.GetType(), indented: false));
        }
        catch (Exception ex)
        {
            Log.Warn("Falha ao salvar últimas opções: " + ex.Message);
        }
    }

    /// <summary>
    /// Mostra o diálogo de opções (com presets e pré-visualização). Cancela a ferramenta se o usuário fechar.
    /// </summary>
    public T Ask<T>(T? options = null, string? description = null, Func<T, PreviewModel?>? preview = null, string? okText = null, string? key = null, string? title = null)
        where T : class, new()
    {
        options ??= LoadOptions<T>(key);
        var ok = OptionsDialog.Show(this, options, title ?? Tool.Name.Value, description ?? Tool.Description.Value, key ?? Tool.Id, preview, okText);
        if (!ok) Cancel();
        SaveLastOptions(options, key);
        return options;
    }

    // ------------------------------------------------------------------ Seleção

    public List<Element> Selected(Func<Element, bool>? predicate = null) =>
        UiDoc.Selection.GetElementIds()
            .Select(id => Doc.GetElement(id))
            .Where(e => e != null && (predicate == null || predicate(e)))
            .ToList();

    /// <summary>Usa a seleção atual (se houver elementos válidos) ou pede para selecionar.</summary>
    public List<Element> SelectedOrPick(string promptPt, string promptEn, Func<Element, bool>? predicate = null, bool allowEmpty = false)
    {
        var sel = Selected(predicate);
        if (sel.Count > 0) return sel;
        var filter = new PredicateFilter(predicate ?? (_ => true));
        var refs = UiDoc.Selection.PickObjects(ObjectType.Element, filter, Loc.T(promptPt, promptEn) + Loc.T(" (Concluir na barra de opções)", " (Finish on the options bar)"));
        var list = refs.Select(r => Doc.GetElement(r)).Where(e => e != null).ToList();
        if (list.Count == 0 && !allowEmpty) Cancel();
        return list;
    }

    public Element PickElement(string promptPt, string promptEn, Func<Element, bool>? predicate = null)
    {
        var r = UiDoc.Selection.PickObject(ObjectType.Element, new PredicateFilter(predicate ?? (_ => true)), Loc.T(promptPt, promptEn));
        return Doc.GetElement(r) ?? throw new OperationCanceledException();
    }

    public Reference PickFace(string promptPt, string promptEn, Func<Element, bool>? predicate = null) =>
        UiDoc.Selection.PickObject(ObjectType.Face, new PredicateFilter(predicate ?? (_ => true)), Loc.T(promptPt, promptEn));

    public Reference PickEdgeOrElement(string promptPt, string promptEn, Func<Element, bool>? predicate = null) =>
        UiDoc.Selection.PickObject(ObjectType.Element, new PredicateFilter(predicate ?? (_ => true)), Loc.T(promptPt, promptEn));

    /// <summary>Pede um ponto; garante um plano de trabalho na vista ativa.</summary>
    public XYZ PickPoint(string promptPt, string promptEn)
    {
        EnsureWorkPlane();
        return UiDoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections |
                                         ObjectSnapTypes.Centers | ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular,
            Loc.T(promptPt, promptEn));
    }

    public void EnsureWorkPlane()
    {
        var v = ActiveView;
        if (v.SketchPlane != null) return;
        Tx.Run(Doc, "Téra Tools: plano de trabalho", () =>
        {
            Plane plane;
            if (v is ViewPlan vp && vp.GenLevel != null)
                plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, vp.GenLevel.Elevation));
            else
                plane = Plane.CreateByNormalAndOrigin(v.ViewDirection, v.Origin);
            v.SketchPlane = SketchPlane.Create(Doc, plane);
        });
    }

    // ------------------------------------------------------------------ Modificação

    /// <summary>Quantas transações a ferramenta concluiu (para detectar "rodou e não fez nada").</summary>
    public int Modifications { get; private set; }

    public void Modify(string name, Action action)
    {
        Tx.Run(Doc, "Téra Tools: " + name, action, Report);
        Modifications++;
    }

    public T Modify<T>(string name, Func<T> func)
    {
        var r = Tx.Run(Doc, "Téra Tools: " + name, func, Report);
        Modifications++;
        return r;
    }

    /// <summary>Várias transações agrupadas em um único "Desfazer".</summary>
    public void Group(string name, Action action)
    {
        using var g = new TransactionGroup(Doc, "Téra Tools: " + name);
        g.Start();
        try
        {
            action();
            if (g.GetStatus() == TransactionStatus.Started) g.Assimilate();
        }
        catch
        {
            if (g.GetStatus() == TransactionStatus.Started) g.RollBack();
            throw;
        }
    }

    /// <summary>
    /// Cria os elementos, mostra o resultado no modelo e pergunta se deve manter
    /// ("validar antes de aplicar"). Se o usuário recusar, tudo é desfeito.
    /// </summary>
    public bool PreviewAndConfirm(string name, Action create, Func<string> question)
    {
        using var g = new TransactionGroup(Doc, "Téra Tools: " + name);
        g.Start();
        try
        {
            Tx.Run(Doc, "Téra Tools: " + name, create, Report);
            UiDoc.RefreshActiveView();
            if (Msg.Confirm(question(), Tool.Name))
            {
                g.Assimilate();
                return true;
            }
            g.RollBack();
            Report.ResultElementIds.Clear();
            Cancel();
            return false;
        }
        catch
        {
            if (g.GetStatus() == TransactionStatus.Started) g.RollBack();
            throw;
        }
    }

    /// <summary>Confere a quantidade de elementos contra os limites de desempenho configurados.</summary>
    public void CheckCount(int count, string whatPt, string whatEn)
    {
        if (count <= 0)
            Fail("Nada para criar com as opções atuais.", "Nothing to create with the current options.");
        if (count > Settings.ElementHardLimit)
            throw new ToolException(Loc.F(
                "A operação criaria {0} {1}, acima do limite de {2}. Ajuste as opções ou o limite nas configurações.",
                "The operation would create {0} {1}, above the limit of {2}. Adjust the options or the limit in settings.",
                count, Loc.T(whatPt, whatEn), Settings.ElementHardLimit));
        if (count > Settings.ElementWarningThreshold &&
            !Msg.Confirm(Loc.F("Serão criados {0} {1}. Isso pode deixar o modelo pesado. Continuar?",
                "{0} {1} will be created. This may slow the model down. Continue?", count, Loc.T(whatPt, whatEn)), Tool.Name))
            Cancel();
    }

    // ------------------------------------------------------------------ Falhas por item

    /// <summary>
    /// Registra um item que o Revit recusou (agrupado no relatório, com o motivo, e gravado no log).
    /// Use no lugar de engolir a exceção: o usuário precisa saber o que não foi feito e por quê.
    /// </summary>
    public void ItemFailed(string whatPt, string whatEn, Exception ex, long elementId = -1) =>
        ItemFailed(whatPt, whatEn, ex.Message, elementId);

    public void ItemFailed(string whatPt, string whatEn, string reason, long elementId = -1)
    {
        Report.ItemFailed(Loc.T(whatPt, whatEn), reason, elementId);
        Log.Warn($"{Tool.Id}: {whatPt}: {reason}");
    }

    /// <summary>
    /// Interrompe com uma mensagem clara quando nada foi criado (em vez de terminar "com sucesso" sem resultado).
    /// A mensagem traz o primeiro motivo informado pelo Revit, se houver.
    /// </summary>
    public void RequireCreated(int created, string whatPt, string whatEn)
    {
        if (created > 0) return;
        var reason = Report.FirstFailure;
        throw new ToolException(reason == null
            ? Loc.F("Nenhum item ({0}) foi criado com essas opções.", "No item ({0}) was created with these options.", Loc.T(whatPt, whatEn))
            : Loc.F("Nenhum item ({0}) pôde ser criado. Motivo informado pelo Revit: {1}", "No item ({0}) could be created. Revit reported: {1}", Loc.T(whatPt, whatEn), reason));
    }

    public void SelectResults(IEnumerable<ElementId> ids)
    {
        foreach (var id in ids) Report.AddResult(id.Value);
    }
}
