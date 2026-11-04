using System.Windows;
using System.Windows.Controls;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using CheckBox = System.Windows.Controls.CheckBox;
using Grid = System.Windows.Controls.Grid;
using TextBox = System.Windows.Controls.TextBox;
using Visibility = System.Windows.Visibility;

namespace TeraTools.Revit.Ui;

/// <summary>Escolha de itens de uma lista (com busca, seleção múltipla e "marcar todos").</summary>
public static class Pick
{
    public static List<T> Many<T>(IEnumerable<T> items, Func<T, string> label, string title, string? prompt = null,
        IntPtr owner = default, Func<T, bool>? preselect = null, Func<T, string>? detail = null)
    {
        var list = items.ToList();
        var w = new ListPickerWindow(list.Select(i => (label(i), detail?.Invoke(i) ?? "", preselect?.Invoke(i) ?? false)).ToList(), title, prompt, true, owner);
        if (w.ShowDialog() != true) return new List<T>();
        return w.Selected.Select(i => list[i]).ToList();
    }

    public static T? One<T>(IEnumerable<T> items, Func<T, string> label, string title, string? prompt = null,
        IntPtr owner = default, Func<T, string>? detail = null) where T : class
    {
        var list = items.ToList();
        var w = new ListPickerWindow(list.Select(i => (label(i), detail?.Invoke(i) ?? "", false)).ToList(), title, prompt, false, owner);
        if (w.ShowDialog() != true || w.Selected.Count == 0) return null;
        return list[w.Selected[0]];
    }
}

internal sealed class ListPickerWindow : Window
{
    private readonly List<(string Label, string Detail, bool Checked)> _items;
    private readonly bool _multi;
    private readonly StackPanel _panel = new();
    private readonly TextBox _search = new();
    private readonly List<(int Index, FrameworkElement Row, CheckBox? Box, RadioButton? Radio)> _rows = new();
    private readonly TextBlock _count;

    public List<int> Selected { get; } = new();

    public ListPickerWindow(List<(string, string, bool)> items, string title, string? prompt, bool multi, IntPtr owner)
    {
        _items = items;
        _multi = multi;
        Title = "Téra Tools — " + title;
        Width = 520;
        Height = Math.Min(640, 220 + items.Count * 30);
        MinHeight = 300;
        Theme.Apply(this, owner);

        var root = new Grid { Margin = new Thickness(14) };
        for (var i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        var head = UiKit.Col(2, UiKit.Title(title));
        if (!string.IsNullOrWhiteSpace(prompt)) head.Children.Add(UiKit.Muted(prompt!));
        head.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(head.At(0, 0));

        var sg = new Grid();
        var ph = UiKit.Muted(Loc.T("Filtrar…", "Filter…"), wrap: false, margin: new Thickness(8, 0, 0, 0));
        ph.IsHitTestVisible = false;
        _search.TextChanged += (_, _) =>
        {
            ph.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;
            Filter();
        };
        sg.Children.Add(_search);
        sg.Children.Add(ph);
        sg.Margin = new Thickness(0, 0, 0, 6);
        root.Children.Add(sg.At(0, 1));

        RadioButton? firstRadio = null;
        for (var i = 0; i < items.Count; i++)
        {
            var (label, detail, isChecked) = items[i];
            var content = UiKit.Col(0, UiKit.Text(label, wrap: false));
            if (!string.IsNullOrWhiteSpace(detail)) content.Children.Add(UiKit.Muted(detail, 11, wrap: false));
            if (multi)
            {
                var cb = new CheckBox { Content = content, IsChecked = isChecked, Margin = new Thickness(2, 3, 2, 3) };
                cb.Checked += (_, _) => UpdateCount();
                cb.Unchecked += (_, _) => UpdateCount();
                _panel.Children.Add(cb);
                _rows.Add((i, cb, cb, null));
            }
            else
            {
                var rb = new RadioButton { Content = content, GroupName = "pick", Margin = new Thickness(2, 3, 2, 3) };
                rb.MouseDoubleClick += (_, _) => Accept();
                firstRadio ??= rb;
                _panel.Children.Add(rb);
                _rows.Add((i, rb, null, rb));
            }
        }
        if (firstRadio != null) firstRadio.IsChecked = true;
        root.Children.Add(new ScrollViewer { Content = _panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }.At(0, 2));

        _count = UiKit.Muted("", wrap: false, margin: new Thickness(0, 6, 0, 0));
        root.Children.Add(_count.At(0, 3));

        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 10, 0, 0);
        if (multi)
        {
            footer.Children.Add(UiKit.Row(6,
                UiKit.Button(Loc.T("Marcar todos", "Check all"), () => SetAll(true)),
                UiKit.Button(Loc.T("Desmarcar", "Uncheck all"), () => SetAll(false))).At(0));
        }
        var ok = UiKit.Button("OK", Accept, accent: true, minWidth: 90);
        ok.IsDefault = true;
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => DialogResult = false, minWidth: 90);
        cancel.IsCancel = true;
        footer.Children.Add(UiKit.Row(8, cancel, ok).At(1));
        root.Children.Add(footer.At(0, 4));
        Content = root;
        UpdateCount();
        Loaded += (_, _) => _search.Focus();
    }

    private void Filter()
    {
        var q = TextUtil.RemoveDiacritics(_search.Text ?? "").ToLowerInvariant();
        foreach (var (idx, row, _, _) in _rows)
        {
            var hay = TextUtil.RemoveDiacritics(_items[idx].Label + " " + _items[idx].Detail).ToLowerInvariant();
            row.Visibility = q.Length == 0 || hay.Contains(q) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SetAll(bool value)
    {
        foreach (var (_, row, box, _) in _rows)
            if (box != null && row.Visibility == Visibility.Visible) box.IsChecked = value;
    }

    private void UpdateCount()
    {
        if (!_multi)
        {
            _count.Text = Loc.F("{0} item(ns)", "{0} item(s)", _items.Count);
            return;
        }
        _count.Text = Loc.F("{0} de {1} marcado(s)", "{0} of {1} checked", _rows.Count(r => r.Box?.IsChecked == true), _items.Count);
    }

    private void Accept()
    {
        Selected.Clear();
        foreach (var (idx, _, box, radio) in _rows)
            if (box?.IsChecked == true || radio?.IsChecked == true)
                Selected.Add(idx);
        if (Selected.Count == 0)
        {
            Msg.Info(Loc.T("Selecione pelo menos um item.", "Select at least one item."));
            return;
        }
        DialogResult = true;
    }
}
