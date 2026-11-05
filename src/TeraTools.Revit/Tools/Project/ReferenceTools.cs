using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Imaging;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;
using Grid = System.Windows.Controls.Grid;
using Image = System.Windows.Controls.Image;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Visibility = System.Windows.Visibility;

namespace TeraTools.Revit.Tools.Project;

[Transaction(TransactionMode.Manual)]
[Tool("proj.references", Modules.Project, "Referências visuais", "Visual references", Order = 14, Large = true,
    DescPt = "Guarde dentro do próprio .rvt imagens de referência — moodboard, inspirações, fotos de obra, croquis — com título, notas e etiquetas, ligadas (se quiser) a um sistema gerado.",
    DescEn = "Keep reference images inside the .rvt — moodboard, inspirations, site photos, sketches — with title, notes and tags, optionally linked to a generated system.",
    HelpPt = "Adicione imagens de arquivos ou cole da área de transferência (Ctrl+V). Cada imagem é reduzida (lado maior de 1600 px, JPEG) e salva no projeto: viaja com o .rvt, sem depender de pastas.\n\nSe um elemento gerado (coluna, cobertura, lâminas, padrão, marcenaria…) estiver selecionado ao abrir, a janela mostra as referências daquele sistema e as novas já vêm ligadas a ele.\n\nDica: mantenha poucas dezenas de imagens; o tamanho total aparece no rodapé.",
    HelpEn = "Add images from files or paste from the clipboard. Images are reduced (1600 px JPEG) and saved inside the project. Select a generated element first to see and link its references.",
    Keywords = "referencias imagens moodboard inspiracao fotos croqui notas references images moodboard inspiration photos")]
public sealed class ReferencesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var items = ReferenceStore.LoadAll(doc);
        var systems = ProjectStorage.AllSystems(doc)
            .OrderByDescending(kv => kv.Value.Modified == default ? kv.Value.Created : kv.Value.Modified)
            .Select(kv => (Id: kv.Key, Label: $"{kv.Value.Name ?? kv.Value.ToolId} — {kv.Key}"))
            .ToList();
        string? focus = null;
        foreach (var e in ctx.Selected())
            if (ProjectStorage.ReadGenerated(e) is { } g)
            {
                focus = g.SystemId;
                break;
            }

        var w = new ReferenceWindow(ctx, items, systems, focus);
        if (w.ShowDialog() != true) ctx.Cancel();
        var changed = w.Items.Where(i => i.Dirty).ToList();
        if (changed.Count == 0 && w.Deleted.Count == 0)
        {
            ctx.Report.Info(Loc.T("Nenhuma alteração nas referências.", "No changes to the references."));
            return;
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var d in w.Deleted)
            {
                try
                {
                    ReferenceStore.Delete(doc, d);
                    ctx.Report.Deleted();
                }
                catch (Exception ex)
                {
                    ctx.ItemFailed("Excluir referência", "Delete reference", ex);
                }
            }
            foreach (var i in changed)
            {
                try
                {
                    var isNew = i.StorageId < 0;
                    ReferenceStore.Save(doc, i);
                    if (isNew) ctx.Report.Created();
                    else ctx.Report.Modified();
                }
                catch (Exception ex)
                {
                    ctx.ItemFailed("Salvar referência", "Save reference", ex);
                }
            }
        });
        var total = w.Items.Sum(i => (long)i.Jpeg.Length);
        ctx.Report.Info(Loc.F("{0} referência(s) no projeto, {1} no total.", "{0} reference(s) in the project, {1} in total.", w.Items.Count, ImageFit.FormatBytes(total)));
    }
}

internal sealed class ReferenceWindow : Window
{
    private readonly ListBox _list = new();
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBox _title = new();
    private readonly TextBox _note = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox _tags = new();
    private readonly ComboBox _system = new();
    private readonly ComboBox _filter = new();
    private readonly TextBlock _info;
    private readonly TextBlock _footer;
    private readonly TextBlock _error;
    private readonly List<(string Id, string Label)> _systems;
    private readonly string? _focus;
    private bool _loading;

    public List<ReferenceStore.Item> Items { get; }
    public List<ReferenceStore.Item> Deleted { get; } = new();

    public ReferenceWindow(ToolContext ctx, List<ReferenceStore.Item> items, List<(string Id, string Label)> systems, string? focusSystem)
    {
        Items = items;
        _systems = systems;
        _focus = focusSystem;
        Title = "Téra Tools — " + Loc.T("Referências visuais", "Visual references");
        Width = 1100;
        Height = 720;
        MinWidth = 760;
        MinHeight = 480;
        Theme.Apply(this, ctx.Owner);

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _filter.Items.Add(Loc.T("Todas as referências", "All references"));
        if (_focus != null) _filter.Items.Add(Loc.T("Só as do sistema selecionado", "Only the selected system's"));
        _filter.SelectedIndex = _focus != null ? 1 : 0;
        _filter.SelectionChanged += (_, _) => Refresh();
        var head = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        head.Children.Add(UiKit.Col(2,
            UiKit.Title(Loc.T("Referências visuais do projeto", "Project visual references")),
            UiKit.Muted(Loc.T("As imagens ficam salvas dentro deste .rvt (reduzidas para 1600 px). Cole com Ctrl+V ou adicione arquivos.",
                "Images are saved inside this .rvt (reduced to 1600 px). Paste with Ctrl+V or add files."))).At(0));
        head.Children.Add(_filter.At(1));
        head.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(head.At(0, 0));

        // galeria | imagem | dados
        var body = UiKit.Grid(UiKit.Px(250), UiKit.Star(), UiKit.Px(280));
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.SelectionChanged += (_, _) => ShowCurrent();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete) Safe(Delete);
        };
        body.Children.Add(_list.At(0));
        var frame = new Border { Margin = new Thickness(10, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2F, 0x36)), CornerRadius = new CornerRadius(4), Child = _image };
        body.Children.Add(frame.At(1));

        foreach (var (_, label) in new[] { ("", Loc.T("(nenhum)", "(none)")) }.Concat(_systems)) _system.Items.Add(label);
        _title.TextChanged += (_, _) => Edit(i => i.Title = _title.Text);
        _note.TextChanged += (_, _) => Edit(i => i.Note = _note.Text);
        _tags.TextChanged += (_, _) => Edit(i => i.Tags = _tags.Text);
        _system.SelectionChanged += (_, _) => Edit(i => i.SystemId = _system.SelectedIndex <= 0 ? null : _systems[_system.SelectedIndex - 1].Id);
        _info = UiKit.Muted("", 11);
        var side = UiKit.Col(6,
            UiKit.Text(Loc.T("Título", "Title"), bold: true), _title,
            UiKit.Text(Loc.T("Notas", "Notes"), bold: true), _note,
            UiKit.Text(Loc.T("Etiquetas (separe por vírgula)", "Tags (comma separated)"), bold: true), _tags,
            UiKit.Text(Loc.T("Sistema gerado ligado", "Linked generated system"), bold: true), _system,
            _info,
            UiKit.Divider(new Thickness(0, 6, 0, 6)),
            UiKit.Button(Loc.T("Adicionar imagens…", "Add images…"), () => Safe(AddFiles), accent: true),
            UiKit.Button(Loc.T("Colar da área de transferência", "Paste from clipboard"), () => Safe(Paste)),
            UiKit.Button(Loc.T("Exportar imagem…", "Export image…"), () => Safe(Export)),
            UiKit.Button(Loc.T("Abrir no visualizador do Windows", "Open in the Windows viewer"), () => Safe(OpenExternal)),
            UiKit.Button(Loc.T("Excluir", "Delete"), () => Safe(Delete)));
        body.Children.Add(new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }.At(2));
        root.Children.Add(body.At(0, 1));

        _error = UiKit.Text("", colorKey: "TT.Error", margin: new Thickness(0, 8, 0, 0));
        _error.Visibility = Visibility.Collapsed;
        root.Children.Add(_error.At(0, 2));

        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 10, 0, 0);
        _footer = UiKit.Muted("", wrap: false);
        footer.Children.Add(_footer.At(0));
        var save = UiKit.Button(Loc.T("Salvar no projeto", "Save to project"), () => DialogResult = true, accent: true, minWidth: 140);
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => DialogResult = false, minWidth: 90);
        cancel.IsCancel = true;
        footer.Children.Add(UiKit.Row(8, cancel, save).At(1));
        root.Children.Add(footer.At(0, 3));
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && e.OriginalSource is not TextBox)
            {
                Safe(Paste);
                e.Handled = true;
            }
        };
        Refresh();
    }

    private ReferenceStore.Item? Current => (_list.SelectedItem as FrameworkElement)?.Tag as ReferenceStore.Item;

    private void Safe(Action a)
    {
        try
        {
            _error.Visibility = Visibility.Collapsed;
            a();
        }
        catch (Exception ex)
        {
            Log.Error("Referências visuais", ex);
            _error.Text = Loc.T("Erro: ", "Error: ") + ex.Message;
            _error.Visibility = Visibility.Visible;
        }
    }

    private void Edit(Action<ReferenceStore.Item> change)
    {
        if (_loading || Current is not { } item) return;
        change(item);
        item.Dirty = true;
        if (_list.SelectedItem is FrameworkElement fe && fe is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb)
            tb.Text = Caption(item);
    }

    private static string Caption(ReferenceStore.Item i) => string.IsNullOrWhiteSpace(i.Title) ? i.Date.ToString("dd/MM/yyyy HH:mm", Loc.Culture) : i.Title;

    private void Refresh(ReferenceStore.Item? select = null)
    {
        var keep = select ?? Current;
        _list.Items.Clear();
        var onlyFocus = _focus != null && _filter.SelectedIndex == 1;
        foreach (var i in Items)
        {
            if (onlyFocus && i.SystemId != _focus) continue;
            var thumb = ReferenceStore.Decode(i.Jpeg, 220);
            var panel = new StackPanel { Margin = new Thickness(4), Tag = i };
            panel.Children.Add(new Border
            {
                Height = 130,
                Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2F, 0x36)),
                CornerRadius = new CornerRadius(3),
                Child = new Image { Source = thumb, Stretch = Stretch.Uniform },
            });
            panel.Children.Add(UiKit.Text(Caption(i), bold: true, wrap: false, margin: new Thickness(0, 4, 0, 0)));
            if (i.SystemId != null) panel.Children.Add(UiKit.Muted(Loc.T("ligada a um sistema gerado", "linked to a generated system"), 10.5, wrap: false));
            _list.Items.Add(panel);
            if (ReferenceEquals(i, keep)) _list.SelectedItem = panel;
        }
        if (_list.SelectedItem == null && _list.Items.Count > 0) _list.SelectedIndex = 0;
        var total = Items.Sum(i => (long)i.Jpeg.Length);
        _footer.Text = Loc.F("{0} referência(s) · {1} dentro do .rvt", "{0} reference(s) · {1} inside the .rvt", Items.Count, ImageFit.FormatBytes(total));
        if (_list.Items.Count == 0) ShowCurrent();
    }

    private void ShowCurrent()
    {
        _loading = true;
        try
        {
            var i = Current;
            _image.Source = i == null ? null : ReferenceStore.Decode(i.Jpeg);
            _title.Text = i?.Title ?? "";
            _note.Text = i?.Note ?? "";
            _tags.Text = i?.Tags ?? "";
            var idx = i?.SystemId == null ? 0 : _systems.FindIndex(s => s.Id == i.SystemId) + 1;
            _system.SelectedIndex = Math.Max(0, idx);
            _info.Text = i == null ? Loc.T("Adicione imagens para começar.", "Add images to start.")
                : Loc.F("{0} × {1} px · {2} · {3}", "{0} × {1} px · {2} · {3}", i.Width, i.Height, ImageFit.FormatBytes(i.Jpeg.Length), i.Date.ToString("dd/MM/yyyy HH:mm", Loc.Culture));
            foreach (var c in new UIElement[] { _title, _note, _tags, _system }) c.IsEnabled = i != null;
        }
        finally
        {
            _loading = false;
        }
    }

    private ReferenceStore.Item NewItem(byte[] jpeg, int w, int h, string title) => new()
    {
        Jpeg = jpeg,
        Width = w,
        Height = h,
        Title = title,
        SystemId = _focus,
        Dirty = true,
    };

    private void AddFiles()
    {
        var d = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = Loc.T("Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|Todos os arquivos|*.*", "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"),
        };
        if (d.ShowDialog(this) != true) return;
        ReferenceStore.Item? last = null;
        var failed = new List<string>();
        foreach (var f in d.FileNames)
        {
            try
            {
                var (jpeg, w, h) = ReferenceStore.FromFile(f);
                last = NewItem(jpeg, w, h, Path.GetFileNameWithoutExtension(f));
                Items.Add(last);
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(f)}: {ex.Message}");
            }
        }
        Refresh(last);
        if (failed.Count > 0) throw new InvalidOperationException(Loc.T("Algumas imagens não puderam ser lidas:\n", "Some images could not be read:\n") + string.Join("\n", failed.Take(5)));
    }

    private void Paste()
    {
        if (!Clipboard.ContainsImage())
        {
            // arquivos copiados no Explorer também valem
            if (Clipboard.ContainsFileDropList())
            {
                ReferenceStore.Item? lastFile = null;
                foreach (var f in Clipboard.GetFileDropList().Cast<string>())
                {
                    var (jpeg, w, h) = ReferenceStore.FromFile(f);
                    lastFile = NewItem(jpeg, w, h, Path.GetFileNameWithoutExtension(f));
                    Items.Add(lastFile);
                }
                Refresh(lastFile);
                return;
            }
            throw new InvalidOperationException(Loc.T("A área de transferência não tem imagem.", "The clipboard has no image."));
        }
        var img = Clipboard.GetImage() ?? throw new InvalidOperationException(Loc.T("A área de transferência não tem imagem.", "The clipboard has no image."));
        var (bytes, pw, ph) = ReferenceStore.Encode(img);
        var item = NewItem(bytes, pw, ph, Loc.F("Colada em {0}", "Pasted on {0}", DateTime.Now.ToString("dd/MM HH:mm", Loc.Culture)));
        Items.Add(item);
        Refresh(item);
    }

    private void Export()
    {
        if (Current is not { } i) return;
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "JPEG|*.jpg", FileName = TextUtil.SafeFileName(Caption(i)) + ".jpg" };
        if (d.ShowDialog(this) != true) return;
        File.WriteAllBytes(d.FileName, i.Jpeg);
    }

    private void OpenExternal()
    {
        if (Current is not { } i) return;
        var path = Path.Combine(Path.GetTempPath(), "TeraTools_ref_" + i.Id + ".jpg");
        File.WriteAllBytes(path, i.Jpeg);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void Delete()
    {
        if (Current is not { } i) return;
        if (!Msg.Confirm(Loc.F("Excluir a referência \"{0}\"?", "Delete reference \"{0}\"?", Caption(i)))) return;
        Items.Remove(i);
        if (i.StorageId >= 0) Deleted.Add(i);
        Refresh();
    }
}
