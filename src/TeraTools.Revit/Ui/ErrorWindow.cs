using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Ui;

/// <summary>Erro inesperado: mensagem clara + detalhes técnicos copiáveis para suporte.</summary>
public sealed class ErrorWindow : Window
{
    private ErrorWindow(ToolInfo tool, Exception ex, IntPtr owner)
    {
        Title = "Téra Tools";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Theme.Apply(this, owner);

        var inner = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
        var details = $"Téra Tools {typeof(ErrorWindow).Assembly.GetName().Version} · Revit {Paths.RevitVersion}\n" +
                      $"Ferramenta: {tool.Id} ({tool.Name.Pt})\nData: {DateTime.Now:G}\n\n{inner}";

        var detailsBox = new TextBox
        {
            Text = details,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            Height = 180,
            Visibility = Visibility.Collapsed,
            VerticalContentAlignment = VerticalAlignment.Top,
        };

        var toggle = UiKit.FlatButton(UiKit.Text(Loc.T("▸ Mostrar detalhes técnicos", "▸ Show technical details"), colorKey: "TT.Info"), () => { });
        toggle.Click += (_, _) =>
        {
            detailsBox.Visibility = detailsBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        };

        var buttons = UiKit.Row(8,
            UiKit.Button(Loc.T("Copiar detalhes", "Copy details"), () =>
            {
                Clipboard.SetText(details);
                Msg.Info(Loc.T("Detalhes copiados. Cole-os ao relatar o problema.", "Details copied. Paste them when reporting the problem."));
            }),
            UiKit.Button(Loc.T("Abrir pasta de logs", "Open logs folder"), () => Process.Start(new ProcessStartInfo(Paths.Logs) { UseShellExecute = true })),
            UiKit.Button(Loc.T("Fechar", "Close"), Close, accent: true, minWidth: 90));
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new Border
        {
            Padding = new Thickness(18),
            Child = UiKit.Col(10,
                UiKit.Title(Loc.F("Não foi possível concluir \"{0}\"", "Could not complete \"{0}\"", tool.Name.Value)),
                UiKit.Text(inner.Message),
                UiKit.Muted(Loc.T(
                    "Nenhuma alteração parcial foi mantida no modelo. Se o problema persistir, copie os detalhes e envie para o suporte.",
                    "No partial changes were kept in the model. If the problem persists, copy the details and send them to support.")),
                toggle,
                detailsBox,
                buttons),
        };
    }

    public static void ShowError(ToolInfo tool, Exception ex, IntPtr owner)
    {
        try
        {
            new ErrorWindow(tool, ex, owner).ShowDialog();
        }
        catch
        {
            Msg.Error(ex.Message, tool.Name);
        }
    }
}

/// <summary>Pergunta um texto simples (ex.: nome de preset).</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _box;
    private readonly CheckBox? _check;

    private InputDialog(string title, string prompt, string initial, string? checkText, bool checkValue, IntPtr owner)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Theme.Apply(this, owner);
        _box = new TextBox { Text = initial };
        var col = UiKit.Col(10, UiKit.Text(prompt), _box);
        if (checkText != null)
        {
            _check = new CheckBox { Content = checkText, IsChecked = checkValue };
            col.Children.Add(_check);
        }
        var ok = UiKit.Button("OK", () => { DialogResult = true; }, accent: true, minWidth: 90);
        ok.IsDefault = true;
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => { DialogResult = false; }, minWidth: 90);
        cancel.IsCancel = true;
        var row = UiKit.Row(8, cancel, ok);
        row.HorizontalAlignment = HorizontalAlignment.Right;
        row.Margin = new Thickness(0, 6, 0, 0);
        col.Children.Add(row);
        Content = new Border { Padding = new Thickness(16), Child = col };
        Loaded += (_, _) =>
        {
            _box.Focus();
            _box.SelectAll();
        };
    }

    public static string? Ask(string title, string prompt, string initial = "", IntPtr owner = default) =>
        Ask(title, prompt, initial, null, false, owner).Text;

    public static (string? Text, bool Checked) Ask(string title, string prompt, string initial, string? checkText, bool checkValue, IntPtr owner)
    {
        var d = new InputDialog(title, prompt, initial, checkText, checkValue, owner);
        return d.ShowDialog() == true && !string.IsNullOrWhiteSpace(d._box.Text)
            ? (d._box.Text.Trim(), d._check?.IsChecked == true)
            : (null, false);
    }
}
