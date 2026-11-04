using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Revit.Infrastructure;
using Grid = System.Windows.Controls.Grid;
using Border = System.Windows.Controls.Border;
using TextBlock = System.Windows.Controls.TextBlock;

namespace TeraTools.Revit.Ui;

/// <summary>Relatório de execução (não modal): mensagens, tabelas, seleção de elementos e exportação.</summary>
public sealed class ReportWindow : Window
{
    private readonly OperationReport _report;
    private readonly ToolInfo _tool;
    private readonly ListBox _messages = new() { BorderThickness = new Thickness(0) };
    private readonly TabControl _tabs = new();
    private readonly List<(TableData Table, DataGrid Grid)> _grids = new();

    private ReportWindow(OperationReport report, ToolInfo tool, IntPtr owner)
    {
        _report = report;
        _tool = tool;
        Title = "Téra Tools — " + Loc.T("Relatório", "Report") + ": " + tool.Name.Value;
        Width = 820;
        Height = 560;
        MinWidth = 520;
        MinHeight = 320;
        Theme.Apply(this, owner);

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var chips = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        foreach (var c in report.Counters)
            chips.Children.Add(Chip($"{c.Key}: {c.Value:N0}", "TT.Text"));
        if (report.ErrorCount > 0) chips.Children.Add(Chip(Loc.F("{0} erro(s)", "{0} error(s)", report.ErrorCount), "TT.Error"));
        if (report.WarningCount > 0) chips.Children.Add(Chip(Loc.F("{0} aviso(s)", "{0} warning(s)", report.WarningCount), "TT.Warning"));
        chips.Children.Add(Chip(Loc.F("{0:0.0} s", "{0:0.0} s", report.Duration.TotalSeconds), "TT.Muted"));
        root.Children.Add(UiKit.Col(2, UiKit.Title(tool.Name.Value), chips).At(0, 0));

        // Mensagens
        foreach (var item in report.Items.OrderByDescending(i => i.Severity))
            _messages.Items.Add(MessageRow(item));
        _messages.MouseDoubleClick += (_, _) => SelectFromMessages();
        if (report.Items.Count > 0)
            _tabs.Items.Add(new TabItem { Header = Loc.F("Mensagens ({0})", "Messages ({0})", report.Items.Count), Content = _messages });

        // Tabelas
        foreach (var t in report.Tables)
        {
            var dg = new DataGrid
            {
                IsReadOnly = true,
                AutoGenerateColumns = true,
                CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Extended,
                ItemsSource = ToDataTable(t).DefaultView,
            };
            dg.MouseDoubleClick += (_, _) => SelectFromGrid(t, dg);
            _grids.Add((t, dg));
            _tabs.Items.Add(new TabItem { Header = $"{t.Title} ({t.RowCount})", Content = dg });
        }
        if (_tabs.Items.Count == 0)
            _tabs.Items.Add(new TabItem { Header = Loc.T("Resumo", "Summary"), Content = UiKit.Text(report.Summary(), margin: new Thickness(8)) });
        _tabs.SelectedIndex = report.Tables.Count > 0 && report.Items.Count == 0 ? 0 : 0;
        root.Children.Add(_tabs.At(0, 1));

        var left = UiKit.Row(8,
            UiKit.Button(Loc.T("Selecionar no modelo", "Select in model"), SelectCurrent, tooltip: Loc.T("Seleciona os elementos da linha/mensagem escolhida (ou todos)", "Selects the elements of the chosen row/message (or all)")),
            UiKit.Button(Loc.T("Exportar Excel", "Export Excel"), ExportExcel),
            UiKit.Button("CSV", ExportCsv),
            UiKit.Button(Loc.T("Relatório HTML", "HTML report"), ExportHtml),
            UiKit.Button(Loc.T("Copiar", "Copy"), CopySummary));
        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 10, 0, 0);
        footer.Children.Add(new ScrollViewer { Content = left, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }.At(0));
        var close = UiKit.Button(Loc.T("Fechar", "Close"), Close, accent: true, minWidth: 90);
        close.IsCancel = true;
        footer.Children.Add(close.At(1));
        root.Children.Add(footer.At(0, 2));
        Content = root;
    }

    public static void ShowReport(OperationReport report, ToolInfo tool, IntPtr owner)
    {
        var w = new ReportWindow(report, tool, owner);
        w.Show();
    }

    private static Border Chip(string text, string colorKey)
    {
        var b = new Border
        {
            Child = UiKit.Text(text, 11.5, colorKey: colorKey, wrap: false),
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 6, 4),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        };
        b.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        b.SetResourceReference(Border.BackgroundProperty, "TT.Surface");
        return b;
    }

    private static ListBoxItem MessageRow(ReportItem item)
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = UiKit.SeverityBrush(item.Severity), Margin = new Thickness(0, 5, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        var g = UiKit.Grid(UiKit.Auto, UiKit.Star(), UiKit.Auto);
        g.Children.Add(dot.At(0));
        g.Children.Add(UiKit.Text(item.Message).At(1));
        if (item.ElementIds.Count > 0)
            g.Children.Add(UiKit.Muted(Loc.F("{0} elem.", "{0} elem.", item.ElementIds.Count), wrap: false, margin: new Thickness(8, 0, 0, 0)).At(2));
        return new ListBoxItem { Content = g, Tag = item, ToolTip = item.ElementIds.Count > 0 ? Loc.T("Clique duas vezes para selecionar no modelo", "Double-click to select in the model") : null };
    }

    private static DataTable ToDataTable(TableData t)
    {
        var dt = new DataTable(t.Title);
        var names = new HashSet<string>();
        foreach (var c in t.Columns)
        {
            var n = c.Name;
            var k = 2;
            while (!names.Add(n)) n = $"{c.Name} ({k++})";
            dt.Columns.Add(n.Replace(".", "·").Replace("/", "∕").Replace("[", "(").Replace("]", ")"), typeof(string));
        }
        for (var r = 0; r < t.Rows.Count; r++)
        {
            var row = dt.NewRow();
            for (var c = 0; c < t.Columns.Count; c++) row[c] = t.FormatCell(r, c);
            dt.Rows.Add(row);
        }
        return dt;
    }

    private void SelectIds(IEnumerable<long> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            Msg.Info(Loc.T("Não há elementos associados a este item.", "There are no elements associated with this item."));
            return;
        }
        RevitTask.Run(app =>
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            var eids = list.Select(v => new ElementId(v)).Where(id => uidoc.Document.GetElement(id) != null).ToList();
            if (eids.Count == 0) return;
            uidoc.Selection.SetElementIds(eids);
            try
            {
                uidoc.ShowElements(eids);
            }
            catch
            {
                // alguns elementos (ex.: vistas) não podem ser mostrados
            }
        });
    }

    private void SelectFromMessages()
    {
        if (_messages.SelectedItem is ListBoxItem { Tag: ReportItem it }) SelectIds(it.ElementIds);
    }

    private void SelectFromGrid(TableData t, DataGrid dg)
    {
        var ids = new List<long>();
        foreach (var sel in dg.SelectedItems)
        {
            if (sel is DataRowView drv)
            {
                var idx = drv.Row.Table.Rows.IndexOf(drv.Row);
                if (idx >= 0 && idx < t.RowElementIds.Count && t.RowElementIds[idx] != null) ids.AddRange(t.RowElementIds[idx]!);
            }
        }
        SelectIds(ids);
    }

    private void SelectCurrent()
    {
        if (_tabs.SelectedItem is TabItem { Content: ListBox })
        {
            if (_messages.SelectedItem is ListBoxItem { Tag: ReportItem it })
                SelectIds(it.ElementIds);
            else
                SelectIds(_report.Items.SelectMany(i => i.ElementIds).Concat(_report.ResultElementIds));
            return;
        }
        var g = _grids.FirstOrDefault(x => _tabs.SelectedItem is TabItem ti && ReferenceEquals(ti.Content, x.Grid));
        if (g.Grid != null)
        {
            if (g.Grid.SelectedItems.Count > 0) SelectFromGrid(g.Table, g.Grid);
            else SelectIds(g.Table.RowElementIds.Where(r => r != null).SelectMany(r => r!));
            return;
        }
        SelectIds(_report.ResultElementIds);
    }

    private TableData MessagesTable()
    {
        var t = new TableData(Loc.T("Mensagens", "Messages"));
        t.AddColumn(Loc.T("Tipo", "Type")).AddColumn(Loc.T("Mensagem", "Message")).AddColumn(Loc.T("Elementos", "Elements"));
        foreach (var i in _report.Items)
            t.AddRowWithIds(i.ElementIds, HtmlReportWriter.SeverityText(i.Severity), i.Message, string.Join(", ", i.ElementIds.Take(200)));
        return t;
    }

    private IEnumerable<TableData> AllTables()
    {
        var list = new List<TableData>();
        if (_report.Items.Count > 0) list.Add(MessagesTable());
        list.AddRange(_report.Tables);
        return list;
    }

    private string DefaultName(string ext) =>
        Core.Text.TextUtil.SafeFileName($"{_tool.Name.Value} {DateTime.Now:yyyy-MM-dd HHmm}") + ext;

    private string? AskSave(string filter, string ext)
    {
        var d = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = DefaultName(ext),
            InitialDirectory = Services.Settings.LastExportFolder is { } f && Directory.Exists(f) ? f : Paths.Exports,
        };
        if (d.ShowDialog(this) != true) return null;
        Services.Settings.LastExportFolder = Path.GetDirectoryName(d.FileName);
        Services.SaveSettings();
        return d.FileName;
    }

    private void ExportExcel()
    {
        var path = AskSave("Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (path == null) return;
        XlsxWriter.Write(path, AllTables());
        OpenFile(path);
    }

    private void ExportCsv()
    {
        var table = _tabs.SelectedItem is TabItem ti ? _grids.FirstOrDefault(g => ReferenceEquals(g.Grid, ti.Content)).Table : null;
        table ??= _report.Tables.FirstOrDefault() ?? MessagesTable();
        var path = AskSave("CSV (*.csv)|*.csv", ".csv");
        if (path == null) return;
        CsvWriter.Write(table, path, Services.Settings.CsvSeparator);
        OpenFile(path);
    }

    private void ExportHtml()
    {
        var path = Path.Combine(Paths.Exports, DefaultName(".html"));
        HtmlReportWriter.Write(path, _tool.Name.Value, Loc.T("Relatório de execução", "Run report"), _report, _report.Tables);
        OpenFile(path);
    }

    private void CopySummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(_tool.Name.Value).AppendLine(_report.Summary());
        foreach (var i in _report.Items) sb.AppendLine($"[{HtmlReportWriter.SeverityText(i.Severity)}] {i.Message}");
        Clipboard.SetText(sb.ToString());
    }

    private static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Msg.Warn(ex.Message);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        base.OnKeyDown(e);
    }
}
