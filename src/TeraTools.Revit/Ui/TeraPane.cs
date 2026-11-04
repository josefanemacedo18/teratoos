using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.UI;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Revit.Infrastructure;
using Grid = System.Windows.Controls.Grid;
using Border = System.Windows.Controls.Border;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using Image = System.Windows.Controls.Image;

namespace TeraTools.Revit.Ui;

/// <summary>Registro do painel lateral no Revit.</summary>
public sealed class PaneProvider : IDockablePaneProvider
{
    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = TeraPane.Instance;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            MinimumWidth = 280,
        };
        data.VisibleByDefault = Services.Settings.ShowPaneOnStartup;
    }
}

/// <summary>Painel lateral do Téra Tools: busca, módulos, favoritos, recentes, histórico e ajuda.</summary>
public sealed class TeraPane : UserControl
{
    private static TeraPane? _instance;
    public static TeraPane Instance => _instance ??= new TeraPane();

    private readonly TextBox _search = new();
    private readonly TabControl _tabs = new();
    private readonly StackPanel _toolsPanel = new();
    private readonly StackPanel _favPanel = new();
    private readonly StackPanel _recentPanel = new();
    private readonly StackPanel _historyPanel = new();
    private readonly TextBlock _detailTitle = UiKit.Text("", 13, true);
    private readonly TextBlock _detailDesc = UiKit.Muted("");
    private readonly TextBlock _docName = UiKit.Muted("", 11, wrap: false);
    private readonly HashSet<string> _collapsed = new();
    private ToolInfo? _detailTool;

    private TeraPane()
    {
        Theme.Apply(this);
        Build();
        Services.Changed += () => Dispatcher.BeginInvoke(new Action(RefreshLists));
        Theme.Changed += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            Icons.ClearCache();
            Build();
        }));
    }

    public void SetDocument(Autodesk.Revit.DB.Document? doc)
    {
        _docName.Text = doc == null ? Loc.T("Nenhum projeto aberto", "No project open") : doc.Title;
    }

    private void Build()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Cabeçalho
        var header = UiKit.Grid(UiKit.Auto, UiKit.Star(), UiKit.Auto, UiKit.Auto);
        header.Children.Add(new Image { Source = Icons.Vector("mod.general", Modules.Get(Modules.General).ColorHex), Width = 26, Height = 26, Margin = new Thickness(0, 0, 8, 0) }.At(0));
        header.Children.Add(UiKit.Col(0, UiKit.Text("Téra Tools", 15, true, wrap: false), _docName).At(1));
        header.Children.Add(IconButton("general.settings", Loc.T("Configurações", "Settings"), () => RevitTask.RunTool("general.settings")).At(2));
        header.Children.Add(IconButton("general.help", Loc.T("Ajuda", "Help"), HelpPages.OpenIndex).At(3));
        header.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(header.At(0, 0));

        // Busca
        var searchGrid = new Grid();
        var placeholder = UiKit.Muted(Loc.T("Buscar ferramenta…  (Enter executa)", "Search tools…  (Enter runs)"), wrap: false, margin: new Thickness(8, 0, 0, 0));
        placeholder.IsHitTestVisible = false;
        _search.TextChanged += (_, _) =>
        {
            placeholder.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;
            _tabs.SelectedIndex = 0;
            RefreshTools();
        };
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                var first = ToolCatalog.Search(_search.Text).FirstOrDefault();
                if (first != null) Run(first);
            }
            else if (e.Key == Key.Escape)
            {
                _search.Text = "";
            }
        };
        searchGrid.Children.Add(_search);
        searchGrid.Children.Add(placeholder);
        searchGrid.Margin = new Thickness(0, 0, 0, 6);
        root.Children.Add(searchGrid.At(0, 1));

        // Abas
        _tabs.Items.Clear();
        _tabs.Items.Add(new TabItem { Header = Loc.T("Ferramentas", "Tools"), Content = Scroll(_toolsPanel) });
        _tabs.Items.Add(new TabItem { Header = Loc.T("Favoritos", "Favorites"), Content = Scroll(_favPanel) });
        _tabs.Items.Add(new TabItem { Header = Loc.T("Recentes", "Recent"), Content = Scroll(_recentPanel) });
        _tabs.Items.Add(new TabItem { Header = Loc.T("Histórico", "History"), Content = Scroll(_historyPanel) });
        root.Children.Add(_tabs.At(0, 2));

        // Detalhes / ajuda contextual
        var runBtn = UiKit.Button(Loc.T("Executar", "Run"), () => { if (_detailTool != null) Run(_detailTool); }, accent: true);
        var helpBtn = UiKit.Button(Loc.T("Ajuda", "Help"), () => { if (_detailTool != null) HelpPages.Open(_detailTool); });
        var detail = UiKit.Card(UiKit.Col(4, _detailTitle, _detailDesc, UiKit.Row(6, runBtn, helpBtn)), new Thickness(10, 8, 10, 8));
        detail.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(detail.At(0, 3));

        Content = root;
        ShowDetail(ToolCatalog.Get("general.pane"));
        RefreshLists();
    }

    private static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    private static Button IconButton(string icon, string tip, Action onClick)
    {
        var b = UiKit.FlatButton(new Image { Source = Icons.Vector(icon, Modules.Get(Modules.General).ColorHex), Width = 18, Height = 18 }, onClick, tip);
        b.Padding = new Thickness(5);
        return b;
    }

    private void RefreshLists()
    {
        RefreshTools();
        FillList(_favPanel, Services.Settings.Favorites.Select(ToolCatalog.Get).Where(t => t != null)!,
            Loc.T("Nenhum favorito ainda. Clique na estrela ao lado de uma ferramenta.", "No favorites yet. Click the star next to a tool."));
        FillList(_recentPanel, Services.Settings.Recent.Select(ToolCatalog.Get).Where(t => t != null)!,
            Loc.T("As ferramentas usadas recentemente aparecem aqui.", "Recently used tools appear here."));
        RefreshHistory();
    }

    private void RefreshTools()
    {
        _toolsPanel.Children.Clear();
        var q = _search.Text?.Trim() ?? "";
        if (q.Length > 0)
        {
            var found = ToolCatalog.Search(q).ToList();
            if (found.Count == 0)
                _toolsPanel.Children.Add(UiKit.Muted(Loc.T("Nenhuma ferramenta encontrada.", "No tools found."), margin: new Thickness(4, 8, 4, 8)));
            foreach (var t in found) _toolsPanel.Children.Add(ToolRow(t, showModule: true));
            return;
        }
        foreach (var m in Modules.All)
        {
            var tools = ToolCatalog.InModule(m.Id).ToList();
            if (tools.Count == 0) continue;
            var body = new StackPanel { Margin = new Thickness(0, 2, 0, 6) };
            foreach (var t in tools) body.Children.Add(ToolRow(t, showModule: false));
            var collapsed = _collapsed.Contains(m.Id);
            body.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;

            var bar = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = Theme.Brush(m.ColorHex), Margin = new Thickness(0, 0, 8, 0) };
            var arrow = UiKit.Muted(collapsed ? "▸" : "▾", 12, wrap: false, margin: new Thickness(0, 0, 6, 0));
            var hg = UiKit.Grid(UiKit.Auto, UiKit.Auto, UiKit.Star(), UiKit.Auto);
            hg.Children.Add(arrow.At(0));
            hg.Children.Add(bar.At(1));
            hg.Children.Add(UiKit.Text(m.Name.Value, 12.5, true, wrap: false).At(2));
            hg.Children.Add(UiKit.Muted(tools.Count.ToString(), wrap: false).At(3));
            var head = UiKit.FlatButton(hg, () =>
            {
                if (!_collapsed.Remove(m.Id)) _collapsed.Add(m.Id);
                RefreshTools();
            }, m.Description.Value);
            head.Margin = new Thickness(0, 4, 0, 0);
            _toolsPanel.Children.Add(head);
            _toolsPanel.Children.Add(body);
        }
    }

    private void FillList(StackPanel panel, IEnumerable<ToolInfo> tools, string empty)
    {
        panel.Children.Clear();
        var list = tools.ToList();
        if (list.Count == 0)
        {
            panel.Children.Add(UiKit.Muted(empty, margin: new Thickness(4, 8, 4, 8)));
            return;
        }
        foreach (var t in list) panel.Children.Add(ToolRow(t, showModule: true));
    }

    private UIElement ToolRow(ToolInfo t, bool showModule)
    {
        var mod = Modules.Get(t.Module);
        var g = UiKit.Grid(UiKit.Auto, UiKit.Star(), UiKit.Auto);
        g.Children.Add(new Image { Source = Icons.Vector(Icons.Has(t.Icon) ? t.Icon : mod.Icon, mod.ColorHex), Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top }.At(0));
        var texts = UiKit.Col(0, UiKit.Text(t.Name.Value, wrap: false));
        var sub = showModule ? mod.Name.Value + (Services.Settings.PaneShowDescriptions ? " · " + t.Description.Value : "")
            : Services.Settings.PaneShowDescriptions ? t.Description.Value : "";
        if (sub.Length > 0) texts.Children.Add(UiKit.Muted(sub, 11, wrap: false));
        g.Children.Add(texts.At(1));
        var fav = Services.Settings.IsFavorite(t.Id);
        var star = UiKit.FlatButton(UiKit.Text(fav ? "★" : "☆", 14, colorKey: fav ? "TT.Accent" : "TT.Muted", wrap: false), () =>
        {
            Services.Settings.ToggleFavorite(t.Id);
            Services.SaveSettings();
        }, fav ? Loc.T("Remover dos favoritos", "Remove from favorites") : Loc.T("Adicionar aos favoritos", "Add to favorites"));
        star.Padding = new Thickness(4, 0, 4, 0);
        g.Children.Add(star.At(2));
        var row = UiKit.FlatButton(g, () => Run(t), t.Description.Value);
        row.Padding = new Thickness(6, 4, 4, 4);
        row.MouseEnter += (_, _) => ShowDetail(t);
        return row;
    }

    private void ShowDetail(ToolInfo? t)
    {
        _detailTool = t;
        if (t == null) return;
        _detailTitle.Text = t.Name.Value;
        var help = t.Help.Value;
        _detailDesc.Text = string.IsNullOrWhiteSpace(help) ? t.Description.Value : t.Description.Value + "\n\n" + (help.Length > 360 ? help[..360] + "…" : help);
    }

    private void RefreshHistory()
    {
        _historyPanel.Children.Clear();
        var entries = Services.History.ReadLast(120);
        var bar = UiKit.Row(6,
            UiKit.Button(Loc.T("Ver tudo", "View all"), () => RevitTask.RunTool("general.history")),
            UiKit.Button(Loc.T("Limpar", "Clear"), () =>
            {
                if (!Msg.Confirm(Loc.T("Apagar todo o histórico de operações?", "Delete the whole operation history?"))) return;
                Services.History.Clear();
                RefreshHistory();
            }));
        bar.Margin = new Thickness(0, 4, 0, 6);
        _historyPanel.Children.Add(bar);
        if (entries.Count == 0)
        {
            _historyPanel.Children.Add(UiKit.Muted(Loc.T("Nenhuma operação registrada.", "No operations recorded."), margin: new Thickness(4, 8, 4, 8)));
            return;
        }
        foreach (var e in entries)
        {
            var color = e.Status switch
            {
                RunStatus.Failed => "TT.Error",
                RunStatus.Warning => "TT.Warning",
                RunStatus.Cancelled => "TT.Muted",
                _ => "TT.Success",
            };
            var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 5, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            dot.SetResourceReference(Border.BackgroundProperty, color);
            var tool = ToolCatalog.Get(e.ToolId);
            var g = UiKit.Grid(UiKit.Auto, UiKit.Star());
            g.Children.Add(dot.At(0));
            g.Children.Add(UiKit.Col(0,
                UiKit.Text(tool?.Name.Value ?? e.ToolName, wrap: false),
                UiKit.Muted($"{e.Time:dd/MM HH:mm} · {e.Summary}", 11, wrap: true)).At(1));
            var row = UiKit.FlatButton(g, () => { if (tool != null) Run(tool); }, Loc.T("Clique para executar novamente", "Click to run again"));
            row.Padding = new Thickness(6, 4, 4, 4);
            _historyPanel.Children.Add(row);
        }
    }

    private static void Run(ToolInfo t) => RevitTask.RunTool(t.Id);
}
