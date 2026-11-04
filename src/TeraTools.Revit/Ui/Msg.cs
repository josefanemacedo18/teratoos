using Autodesk.Revit.UI;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Ui;

/// <summary>Mensagens padronizadas (diálogo nativo do Revit).</summary>
public static class Msg
{
    private const string App = "Téra Tools";

    public static void Info(string text, string? title = null) => Show(text, title, TaskDialogIcon.TaskDialogIconInformation);

    public static void Warn(string text, string? title = null) => Show(text, title, TaskDialogIcon.TaskDialogIconWarning);

    public static void Error(string text, string? title = null) => Show(text, title, TaskDialogIcon.TaskDialogIconError);

    private static void Show(string text, string? title, TaskDialogIcon icon)
    {
        try
        {
            var td = new TaskDialog(App)
            {
                MainInstruction = title ?? App,
                MainContent = text,
                MainIcon = icon,
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            td.Show();
        }
        catch
        {
            System.Windows.MessageBox.Show(text, title ?? App);
        }
    }

    public static bool Confirm(string text, string? title = null)
    {
        try
        {
            var td = new TaskDialog(App)
            {
                MainInstruction = title ?? App,
                MainContent = text,
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.Yes,
            };
            return td.Show() == TaskDialogResult.Yes;
        }
        catch
        {
            return System.Windows.MessageBox.Show(text, title ?? App, System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes;
        }
    }

    /// <summary>Escolha entre até 4 opções (links de comando). Retorna o índice ou -1 se cancelado.</summary>
    public static int Choose(string text, string? title, params string[] options)
    {
        var td = new TaskDialog(App)
        {
            MainInstruction = title ?? App,
            MainContent = text,
            CommonButtons = TaskDialogCommonButtons.Cancel,
        };
        var ids = new[] { TaskDialogCommandLinkId.CommandLink1, TaskDialogCommandLinkId.CommandLink2, TaskDialogCommandLinkId.CommandLink3, TaskDialogCommandLinkId.CommandLink4 };
        for (var i = 0; i < options.Length && i < 4; i++) td.AddCommandLink(ids[i], options[i]);
        var r = td.Show();
        return r switch
        {
            TaskDialogResult.CommandLink1 => 0,
            TaskDialogResult.CommandLink2 => 1,
            TaskDialogResult.CommandLink3 => 2,
            TaskDialogResult.CommandLink4 => 3,
            _ => -1,
        };
    }

    public static string T(string pt, string en) => Loc.T(pt, en);
}
