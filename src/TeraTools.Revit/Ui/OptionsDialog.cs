using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Serialization;
using TeraTools.Core.Settings;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using Grid = System.Windows.Controls.Grid;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using TextBlock = System.Windows.Controls.TextBlock;
using Visibility = System.Windows.Visibility;
using Expression = TeraTools.Core.Mathx.Expression;

namespace TeraTools.Revit.Ui;

/// <summary>Diálogo genérico de opções montado a partir dos atributos [Opt] da classe.</summary>
public static class OptionsDialog
{
    public static bool Show<T>(ToolContext? ctx, T options, string title, string? description, string presetKey,
        Func<T, PreviewModel?>? preview = null, string? okText = null) where T : class
    {
        var doc = ctx != null && ctx.HasDocument ? ctx.Doc : null;
        var w = new OptionsWindow(doc, options, title, description, presetKey,
            preview == null ? null : o => preview((T)o), okText, ctx?.Owner ?? IntPtr.Zero, ctx?.Tool);
        return w.ShowDialog() == true;
    }

    public static bool ShowStandalone<T>(Document? doc, T options, string title, string? description, string presetKey, IntPtr owner, string? okText = null) where T : class
    {
        var w = new OptionsWindow(doc, options, title, description, presetKey, null, okText, owner, null);
        return w.ShowDialog() == true;
    }
}

internal sealed class OptionsWindow : Window
{
    private readonly Document? _doc;
    private readonly object _options;
    private readonly Type _type;
    private readonly string _presetKey;
    private readonly Func<object, PreviewModel?>? _preview;
    private readonly List<FieldEditor> _editors = new();
    private readonly TextBlock _errors;
    private readonly Button _ok;
    private readonly PreviewCanvas? _canvas;
    private readonly Preview3D? _view3d;
    private readonly ToggleButton? _tab3d, _tab2d;
    private bool _show3d = true;
    private readonly TextBlock? _previewInfo;
    private readonly DispatcherTimer _timer;
    private readonly ComboBox _presetCombo;
    private readonly Dictionary<string, (Border Header, List<FieldEditor> Fields)> _groups = new();
    private bool _loading;
    private List<Preset> _presetItems = new();

    public OptionsWindow(Document? doc, object options, string title, string? description, string presetKey,
        Func<object, PreviewModel?>? preview, string? okText, IntPtr owner, ToolInfo? tool)
    {
        _doc = doc;
        _options = options;
        _type = options.GetType();
        _presetKey = presetKey;
        _preview = preview;
        Title = "Téra Tools — " + title;
        Width = preview != null ? 980 : 560;
        Height = Math.Min(SystemParameters.WorkArea.Height * 0.9, 760);
        MinWidth = 460;
        MinHeight = 360;
        ResizeMode = ResizeMode.CanResize;
        Theme.Apply(this, owner);

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = UiKit.Col(2, UiKit.Title(title));
        if (!string.IsNullOrWhiteSpace(description)) header.Children.Add(UiKit.Muted(description!));
        header.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(header.At(0, 0));

        // Barra de presets
        _presetCombo = new ComboBox { MinWidth = 220, ToolTip = Loc.T("Presets salvos para esta ferramenta", "Saved presets for this tool") };
        _presetCombo.SelectionChanged += (_, _) => ApplySelectedPreset();
        var presetBar = UiKit.Row(6,
            UiKit.Muted(Loc.T("Preset:", "Preset:"), wrap: false),
            _presetCombo,
            UiKit.Button(Loc.T("Salvar…", "Save…"), SavePreset, tooltip: Loc.T("Salvar as opções atuais como preset", "Save current options as a preset")),
            UiKit.Button(Loc.T("Excluir", "Delete"), DeletePreset),
            UiKit.Button(Loc.T("Importar…", "Import…"), ImportPresets),
            UiKit.Button(Loc.T("Exportar…", "Export…"), ExportPresets),
            UiKit.Button(Loc.T("Padrão", "Default"), ResetDefaults, tooltip: Loc.T("Restaurar valores padrão", "Restore default values")));
        var presetCard = UiKit.Card(new ScrollViewer { Content = presetBar, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, new Thickness(8, 6, 8, 6));
        presetCard.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(presetCard.At(0, 1));

        // Formulário
        var form = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        BuildForm(form);
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        if (preview != null)
        {
            var split = UiKit.Grid(UiKit.Star(1), UiKit.Px(12), UiKit.Star(1.15));
            split.Children.Add(scroll.At(0));
            _canvas = new PreviewCanvas { MinHeight = 200 };
            _previewInfo = UiKit.Muted("", margin: new Thickness(0, 6, 0, 0));
            var pv = new Grid();
            pv.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            pv.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            pv.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _view3d = new Preview3D { Visibility = Visibility.Collapsed };
            _tab3d = new ToggleButton { Content = "3D", MinWidth = 44, Padding = new Thickness(8, 2, 8, 2), Visibility = Visibility.Collapsed, IsChecked = true };
            _tab2d = new ToggleButton { Content = Loc.T("2D", "2D"), MinWidth = 44, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0), Visibility = Visibility.Collapsed };
            _tab3d.Click += (_, _) => Safe(() => { _show3d = true; ApplyPreviewTab(); });
            _tab2d.Click += (_, _) => Safe(() => { _show3d = false; ApplyPreviewTab(); });
            var pvHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = false };
            var pvTitle = UiKit.Text(Loc.T("Pré-visualização", "Preview"), 12, true);
            DockPanel.SetDock(pvTitle, Dock.Left);
            pvHeader.Children.Add(pvTitle);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal };
            tabs.Children.Add(_tab3d);
            tabs.Children.Add(_tab2d);
            DockPanel.SetDock(tabs, Dock.Right);
            pvHeader.Children.Add(tabs);
            pv.Children.Add(pvHeader.At(0, 0));
            var host = new Grid();
            host.Children.Add(_canvas);
            host.Children.Add(_view3d);
            var canvasBorder = new Border { Child = host, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            canvasBorder.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
            pv.Children.Add(canvasBorder.At(0, 1));
            pv.Children.Add(_previewInfo.At(0, 2));
            split.Children.Add(pv.At(2));
            root.Children.Add(split.At(0, 2));
        }
        else
        {
            root.Children.Add(scroll.At(0, 2));
        }

        _errors = UiKit.Text("", colorKey: "TT.Error", margin: new Thickness(0, 8, 0, 0));
        root.Children.Add(_errors.At(0, 3));

        // Rodapé
        var footer = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        footer.Margin = new Thickness(0, 12, 0, 0);
        if (tool != null && !string.IsNullOrWhiteSpace(tool.Help.Pt))
            footer.Children.Add(UiKit.Button(Loc.T("Ajuda", "Help"), () => HelpPages.Open(tool)).At(0));
        _ok = UiKit.Button(okText ?? Loc.T("OK", "OK"), Accept, accent: true, minWidth: 110);
        _ok.IsDefault = true;
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => { DialogResult = false; }, minWidth: 90);
        cancel.IsCancel = true;
        var right = UiKit.Row(8, cancel, _ok);
        footer.Children.Add(right.At(1));
        root.Children.Add(footer.At(0, 4));

        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Safe(UpdatePreview);
        };

        LoadPresetList(null);
        LoadAll();
        Loaded += (_, _) => Safe(UpdatePreview);
        Closed += (_, _) => _timer.Stop();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F1 && tool != null) Safe(() => HelpPages.Open(tool));
        };
    }

    // ------------------------------------------------------------------ Formulário

    private void BuildForm(StackPanel form)
    {
        var props = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Prop: p, Opt: p.GetCustomAttribute<OptAttribute>()))
            .Where(x => x.Opt != null && x.Prop.CanRead && x.Prop.CanWrite)
            .OrderBy(x => x.Prop.MetadataToken)
            .ToList();

        Grid? currentGrid = null;
        string? currentGroup = null;
        var row = 0;
        foreach (var (prop, opt) in props)
        {
            var groupName = opt!.GroupText ?? "";
            if (currentGrid == null || groupName != currentGroup)
            {
                currentGroup = groupName;
                if (!_groups.TryGetValue(groupName, out var g))
                {
                    var headerText = UiKit.Text(string.IsNullOrEmpty(groupName) ? Loc.T("Opções", "Options") : groupName, 12.5, true, "TT.Accent");
                    var hb = new Border { Child = headerText, Margin = new Thickness(0, form.Children.Count == 0 ? 0 : 14, 0, 6), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 3) };
                    hb.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
                    form.Children.Add(hb);
                    currentGrid = UiKit.Grid(new GridLength(0.42, GridUnitType.Star), new GridLength(0.58, GridUnitType.Star));
                    form.Children.Add(currentGrid);
                    _groups[groupName] = (hb, new List<FieldEditor>());
                    row = 0;
                }
                else
                {
                    currentGrid = UiKit.Grid(new GridLength(0.42, GridUnitType.Star), new GridLength(0.58, GridUnitType.Star));
                    form.Children.Add(currentGrid);
                    row = 0;
                }
            }

            var editor = FieldEditor.Create(prop, opt, _doc);
            editor.Changed += OnChanged;
            _editors.Add(editor);
            _groups[groupName].Fields.Add(editor);

            currentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = UiKit.Text(opt.Label + (string.IsNullOrEmpty(opt.Suffix) ? "" : $" ({OptAttribute.Split(opt.Suffix)})"), margin: new Thickness(0, 4, 10, 4));
            if (opt.TipText != null)
            {
                label.ToolTip = opt.TipText;
                label.Cursor = Cursors.Help;
                editor.Control.ToolTip = opt.TipText;
            }
            editor.Label = label;
            currentGrid.Children.Add(label.At(0, row));
            editor.Control.Margin = new Thickness(0, 3, 0, 3);
            currentGrid.Children.Add(editor.Control.At(1, row));
            row++;
        }
    }

    private void LoadAll()
    {
        _loading = true;
        foreach (var e in _editors)
        {
            try
            {
                e.Load(_options);
            }
            catch (Exception ex)
            {
                Log.Error($"Carregar campo \"{e.Opt.Label}\"", ex);
            }
        }
        _loading = false;
        Safe(() =>
        {
            UpdateVisibility();
            Validate();
        });
    }

    private void OnChanged()
    {
        if (_loading) return;
        Safe(() =>
        {
            StoreAll();
            UpdateVisibility();
            Validate();
            _timer.Stop();
            _timer.Start();
        });
    }

    /// <summary>Grava todos os campos no objeto de opções sem deixar exceções escaparem.</summary>
    private void StoreAll()
    {
        foreach (var e in _editors)
        {
            try
            {
                e.Store(_options, out _);
            }
            catch (Exception ex)
            {
                Log.Error($"Campo \"{e.Opt.Label}\" ({e.Prop.Name})", ex);
            }
        }
    }

    /// <summary>
    /// Executa código de evento da interface com proteção: uma exceção dentro de um evento WPF
    /// não tratada pode fechar o Revit, então registramos no log e mostramos no próprio diálogo.
    /// </summary>
    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error("Erro no diálogo de opções", ex);
            try
            {
                _errors.Text = Loc.T("Erro interno: ", "Internal error: ") + ex.Message;
                _errors.Visibility = Visibility.Visible;
            }
            catch
            {
                // diálogo já fechado
            }
        }
    }

    private void UpdateVisibility()
    {
        foreach (var e in _editors)
        {
            var visible = EvaluateVisibleIf(e.Opt.VisibleIf);
            e.SetVisible(visible);
        }
        foreach (var (header, fields) in _groups.Values)
            header.Visibility = fields.Any(f => f.IsVisible) ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool EvaluateVisibleIf(string cond)
    {
        if (string.IsNullOrWhiteSpace(cond)) return true;
        // "A&B|C" = (A e B) ou C
        foreach (var alt in cond.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var ok = true;
            foreach (var part in alt.Split('&', StringSplitOptions.RemoveEmptyEntries))
                if (!EvaluateOne(part.Trim())) { ok = false; break; }
            if (ok) return true;
        }
        return false;
    }

    private bool EvaluateOne(string cond)
    {
        var neg = false;
        string name;
        string[]? values = null;
        if (cond.Contains("!="))
        {
            var p = cond.Split("!=");
            name = p[0].Trim();
            values = p[1].Split(',').Select(v => v.Trim()).ToArray();
            neg = true;
        }
        else if (cond.Contains('='))
        {
            var p = cond.Split('=');
            name = p[0].Trim();
            values = p[1].Split(',').Select(v => v.Trim()).ToArray();
        }
        else if (cond.StartsWith('!'))
        {
            name = cond[1..].Trim();
            neg = true;
        }
        else
        {
            name = cond;
        }
        var prop = _type.GetProperty(name);
        if (prop == null) return true;
        var v = prop.GetValue(_options);
        bool result = values == null
            ? v is bool b && b
            : values.Any(x => string.Equals(x, Convert.ToString(v, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
        return neg ? !result : result;
    }

    private bool Validate()
    {
        var errors = new List<string>();
        foreach (var e in _editors.Where(e => e.IsVisible))
        {
            try
            {
                if (!e.Store(_options, out var err) && err != null)
                    errors.Add($"{e.Opt.Label}: {err}");
            }
            catch (Exception ex)
            {
                Log.Error($"Campo \"{e.Opt.Label}\" ({e.Prop.Name})", ex);
                errors.Add($"{e.Opt.Label}: {ex.Message}");
            }
        }
        if (errors.Count == 0 && _options is IValidatableOptions v)
        {
            try
            {
                errors.AddRange(v.Validate());
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }
        _errors.Text = string.Join(Environment.NewLine, errors.Take(5));
        _errors.Visibility = errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _ok.IsEnabled = errors.Count == 0;
        return errors.Count == 0;
    }

    private void UpdatePreview()
    {
        if (_preview == null || _canvas == null) return;
        if (!Validate())
        {
            _canvas.Model = null;
            return;
        }
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var model = _preview(_options);
            _canvas.Model = model;
            var has3d = model?.Has3D == true;
            if (_view3d != null)
            {
                _view3d.Scene = has3d ? model!.Scene : null;
                _tab3d!.Visibility = _tab2d!.Visibility = has3d ? Visibility.Visible : Visibility.Collapsed;
            }
            ApplyPreviewTab(has3d);
            var info = model?.Info ?? "";
            if (model?.Scene?.Truncated == true)
                info += Loc.T(" · 3D mostra só parte dos elementos (muitos para exibir)", " · 3D shows only part of the elements (too many to display)");
            if (_previewInfo != null) _previewInfo.Text = info;
        }
        catch (Exception ex)
        {
            _canvas.Model = null;
            if (_previewInfo != null) _previewInfo.Text = ex.Message;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ApplyPreviewTab(bool? available = null)
    {
        if (_view3d == null || _canvas == null) return;
        var has3d = available ?? _tab3d?.Visibility == Visibility.Visible;
        var show3d = has3d && _show3d;
        _view3d.Visibility = show3d ? Visibility.Visible : Visibility.Collapsed;
        _canvas.Visibility = show3d ? Visibility.Collapsed : Visibility.Visible;
        if (_tab3d != null) _tab3d.IsChecked = show3d;
        if (_tab2d != null) _tab2d.IsChecked = !show3d;
    }

    private void Accept()
    {
        StoreAll();
        if (!Validate()) return;
        DialogResult = true;
    }

    // ------------------------------------------------------------------ Presets

    private void LoadPresetList(string? select)
    {
        _presetItems = Services.Presets.List(_presetKey);
        if (_doc != null)
        {
            try
            {
                _presetItems.InsertRange(0, ProjectStorage.LoadPresets(_doc).List(_presetKey));
            }
            catch (Exception ex)
            {
                Log.Warn("Presets do projeto indisponíveis: " + ex.Message);
            }
        }
        _loading = true;
        _presetCombo.Items.Clear();
        _presetCombo.Items.Add(Loc.T("(opções atuais)", "(current options)"));
        foreach (var p in _presetItems)
        {
            var tag = p.Scope switch
            {
                PresetScope.Project => Loc.T(" [projeto]", " [project]"),
                PresetScope.Library => Loc.T(" [biblioteca]", " [library]"),
                _ => "",
            };
            _presetCombo.Items.Add(p.Name + tag);
        }
        var idx = select == null ? 0 : _presetItems.FindIndex(p => p.Name == select) + 1;
        _presetCombo.SelectedIndex = Math.Max(0, idx);
        _loading = false;
    }

    private Preset? SelectedPreset => _presetCombo.SelectedIndex > 0 ? _presetItems[_presetCombo.SelectedIndex - 1] : null;

    private void ApplySelectedPreset()
    {
        if (_loading) return;
        var p = SelectedPreset;
        if (p == null) return;
        try
        {
            Json.PopulateObject(_options, p.Json);
            LoadAll();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            Msg.Warn(Loc.F("Preset inválido: {0}", "Invalid preset: {0}", ex.Message));
        }
    }

    private void SavePreset()
    {
        foreach (var e in _editors) e.Store(_options, out _);
        var owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var (name, toProject) = InputDialog.Ask(Loc.T("Salvar preset", "Save preset"), Loc.T("Nome do preset:", "Preset name:"),
            SelectedPreset?.Name ?? "", _doc != null ? Loc.T("Salvar no projeto (vai junto com o arquivo .rvt)", "Save in the project (travels with the .rvt file)") : null,
            false, owner);
        if (name == null) return;
        var preset = new Preset { Name = name, Json = Json.Serialize(_options, _type, indented: false) };
        if (toProject && _doc != null)
        {
            var pp = ProjectStorage.LoadPresets(_doc);
            pp.Save(_presetKey, preset);
            ProjectStorage.SavePresets(_doc, pp);
        }
        else
        {
            Services.Presets.Save(_presetKey, preset);
        }
        LoadPresetList(name);
    }

    private void DeletePreset()
    {
        var p = SelectedPreset;
        if (p == null) return;
        if (p.Scope == PresetScope.Library)
        {
            Msg.Info(Loc.T("Presets da biblioteca compartilhada não podem ser excluídos por aqui.", "Shared library presets cannot be deleted here."));
            return;
        }
        if (!Msg.Confirm(Loc.F("Excluir o preset \"{0}\"?", "Delete preset \"{0}\"?", p.Name))) return;
        if (p.Scope == PresetScope.Project && _doc != null)
        {
            var pp = ProjectStorage.LoadPresets(_doc);
            pp.Delete(_presetKey, p.Name);
            ProjectStorage.SavePresets(_doc, pp);
        }
        else
        {
            Services.Presets.Delete(_presetKey, p.Name);
        }
        LoadPresetList(null);
    }

    private void ImportPresets()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Presets Téra Tools (*.json)|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        var n = Services.Presets.Import(dlg.FileName, _presetKey);
        LoadPresetList(null);
        Msg.Info(Loc.F("{0} preset(s) importado(s).", "{0} preset(s) imported.", n));
    }

    private void ExportPresets()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Presets Téra Tools (*.json)|*.json",
            FileName = Core.Text.TextUtil.SafeFileName(_presetKey) + ".json",
        };
        if (dlg.ShowDialog(this) != true) return;
        var list = _presetItems.Count > 0 ? _presetItems : new List<Preset> { new() { Name = Loc.T("Atual", "Current"), Json = Json.Serialize(_options, _type, false) } };
        Services.Presets.Export(dlg.FileName, _presetKey, list);
    }

    private void ResetDefaults()
    {
        var fresh = Activator.CreateInstance(_type);
        if (fresh == null) return;
        Json.PopulateObject(_options, Json.Serialize(fresh, _type, false));
        _presetCombo.SelectedIndex = 0;
        LoadAll();
        UpdatePreview();
    }
}

// ====================================================================== Editores

internal abstract class FieldEditor
{
    public PropertyInfo Prop { get; protected init; } = null!;
    public OptAttribute Opt { get; protected init; } = null!;
    public FrameworkElement Control { get; protected set; } = null!;
    public FrameworkElement? Label { get; set; }
    public bool IsVisible { get; private set; } = true;
    public event Action? Changed;

    protected void RaiseChanged() => Changed?.Invoke();

    public abstract void Load(object target);

    /// <summary>Grava o valor do controle na propriedade. Retorna falso e uma mensagem se inválido.</summary>
    public abstract bool Store(object target, out string? error);

    public void SetVisible(bool v)
    {
        IsVisible = v;
        var vis = v ? Visibility.Visible : Visibility.Collapsed;
        Control.Visibility = vis;
        if (Label != null) Label.Visibility = vis;
    }

    public static FieldEditor Create(PropertyInfo p, OptAttribute o, Document? doc)
    {
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        var kind = o.Kind;
        if (kind == OptKind.Auto)
        {
            kind = t == typeof(bool) ? OptKind.Bool
                : t.IsEnum ? OptKind.Enum
                : t == typeof(int) || t == typeof(long) ? OptKind.Integer
                : t == typeof(double) || t == typeof(float) ? OptKind.Number
                : typeof(IList).IsAssignableFrom(t) ? OptKind.MultiLine
                : OptKind.Text;
        }
        return kind switch
        {
            OptKind.Bool => new BoolEditor(p, o),
            OptKind.Enum => new EnumEditor(p, o, t),
            OptKind.Length or OptKind.Angle or OptKind.Number or OptKind.Integer or OptKind.Percent => new NumberEditor(p, o, kind, doc),
            OptKind.Color => new ColorEditor(p, o),
            OptKind.File or OptKind.SaveFile or OptKind.Folder => new PathEditor(p, o, kind),
            OptKind.Text or OptKind.MultiLine or OptKind.Formula => new TextEditor(p, o, kind),
            _ => new ChoiceEditor(p, o, kind, doc),
        };
    }
}

internal sealed class BoolEditor : FieldEditor
{
    private readonly CheckBox _cb = new() { VerticalAlignment = VerticalAlignment.Center };

    public BoolEditor(PropertyInfo p, OptAttribute o)
    {
        Prop = p;
        Opt = o;
        Control = _cb;
        _cb.Checked += (_, _) => RaiseChanged();
        _cb.Unchecked += (_, _) => RaiseChanged();
    }

    public override void Load(object target) => _cb.IsChecked = Prop.GetValue(target) is true;

    public override bool Store(object target, out string? error)
    {
        error = null;
        Prop.SetValue(target, _cb.IsChecked == true);
        return true;
    }
}

internal sealed class EnumEditor : FieldEditor
{
    private readonly ComboBox _combo = new();
    private readonly Array _values;

    public EnumEditor(PropertyInfo p, OptAttribute o, Type enumType)
    {
        Prop = p;
        Opt = o;
        Control = _combo;
        _values = Enum.GetValues(enumType);
        foreach (var v in _values) _combo.Items.Add(EnumLabels.Label(enumType, v));
        _combo.SelectionChanged += (_, _) => RaiseChanged();
    }

    public override void Load(object target)
    {
        var v = Prop.GetValue(target);
        _combo.SelectedIndex = Array.IndexOf(_values, v);
    }

    public override bool Store(object target, out string? error)
    {
        error = null;
        if (_combo.SelectedIndex >= 0) Prop.SetValue(target, _values.GetValue(_combo.SelectedIndex));
        return true;
    }
}

internal sealed class NumberEditor : FieldEditor
{
    private readonly TextBox _box = new();
    private readonly OptKind _kind;
    private readonly Document? _doc;
    private readonly bool _isInt;

    public NumberEditor(PropertyInfo p, OptAttribute o, OptKind kind, Document? doc)
    {
        Prop = p;
        Opt = o;
        _kind = kind;
        _doc = doc;
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        _isInt = t == typeof(int) || t == typeof(long) || kind == OptKind.Integer;
        var suffix = kind switch
        {
            OptKind.Angle => "°",
            OptKind.Percent => "%",
            // Medidas sempre em milímetros (decisão do projeto), qualquer que seja a unidade do projeto.
            OptKind.Length => "mm",
            _ => "",
        };
        if (suffix.Length > 0)
        {
            var g = UiKit.Grid(UiKit.Star(), UiKit.Auto);
            g.Children.Add(_box.At(0));
            g.Children.Add(UiKit.Muted(suffix, wrap: false, margin: new Thickness(6, 0, 0, 0)).At(1));
            Control = g;
        }
        else
        {
            Control = _box;
        }
        _box.TextChanged += (_, _) => RaiseChanged();
        _box.LostKeyboardFocus += (_, _) => Reformat();
    }

    private double ReadRaw(object target) => Convert.ToDouble(Prop.GetValue(target) ?? 0, CultureInfo.InvariantCulture);

    public override void Load(object target) => _box.Text = Format(ReadRaw(target));

    private string Format(double v)
    {
        switch (_kind)
        {
            case OptKind.Length:
                return U.ToMm(v).ToString("0.##", Loc.Culture);
            case OptKind.Integer:
                return Math.Round(v).ToString("0", Loc.Culture);
            default:
                return _isInt ? Math.Round(v).ToString("0", Loc.Culture) : v.ToString("0." + new string('#', Math.Max(1, Opt.Decimals + 2)), Loc.Culture);
        }
    }

    private void Reformat()
    {
        if (TryParse(_box.Text, out var v, out _)) _box.Text = Format(v);
    }

    private bool TryParse(string text, out double value, out string? error)
    {
        error = null;
        value = 0;
        if (_kind == OptKind.Length)
        {
            // Número sozinho = mm. Aceita também "1,20 m", "120 cm", "1200 mm".
            if (U.TryParseLength(text, LengthUnit.Millimeters, out value)) return CheckRange(value, out error);
            error = Loc.T("comprimento inválido", "invalid length");
            return false;
        }
        if (!U.TryParseNumber(text, out value))
        {
            error = Loc.T("número inválido", "invalid number");
            return false;
        }
        if (_isInt) value = Math.Round(value);
        return CheckRange(value, out error);
    }

    private bool CheckRange(double v, out string? error)
    {
        error = null;
        var min = Opt.Min;
        var max = Opt.Max;
        if (!double.IsNaN(min) && v < min - 1e-12)
        {
            error = Loc.F("mínimo {0}", "minimum {0}", Format(min));
            return false;
        }
        if (!double.IsNaN(max) && v > max + 1e-12)
        {
            error = Loc.F("máximo {0}", "maximum {0}", Format(max));
            return false;
        }
        return true;
    }

    public override bool Store(object target, out string? error)
    {
        if (!TryParse(_box.Text, out var v, out error)) return false;
        // Converte para o tipo exato da propriedade (int, long, float, double...).
        Prop.SetValue(target, ValueConverter.ToNumeric(v, Prop.PropertyType));
        return true;
    }
}

internal sealed class TextEditor : FieldEditor
{
    private readonly TextBox _box = new();
    private readonly OptKind _kind;
    private readonly bool _isList;

    public TextEditor(PropertyInfo p, OptAttribute o, OptKind kind)
    {
        Prop = p;
        Opt = o;
        _kind = kind;
        _isList = typeof(IList).IsAssignableFrom(p.PropertyType);
        if (kind == OptKind.MultiLine || _isList)
        {
            _box.AcceptsReturn = true;
            _box.TextWrapping = TextWrapping.Wrap;
            _box.MinHeight = 64;
            _box.MaxHeight = 160;
            _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _box.VerticalContentAlignment = VerticalAlignment.Top;
        }
        if (kind == OptKind.Formula) _box.FontFamily = new System.Windows.Media.FontFamily("Consolas");
        Control = _box;
        _box.TextChanged += (_, _) => RaiseChanged();
    }

    public override void Load(object target)
    {
        var v = Prop.GetValue(target);
        _box.Text = v is IEnumerable<string> list ? string.Join(Environment.NewLine, list) : v?.ToString() ?? "";
    }

    public override bool Store(object target, out string? error)
    {
        error = null;
        var text = _box.Text ?? "";
        if (_kind == OptKind.Formula && !Expression.TryParse(text, out _, out error)) return false;
        if (Opt.Required && string.IsNullOrWhiteSpace(text))
        {
            error = Loc.T("obrigatório", "required");
            return false;
        }
        if (_isList)
        {
            var items = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            Prop.SetValue(target, items);
        }
        else
        {
            Prop.SetValue(target, text);
        }
        return true;
    }
}

internal sealed class ColorEditor : FieldEditor
{
    private readonly TextBox _box = new();
    private readonly Border _swatch;

    public ColorEditor(PropertyInfo p, OptAttribute o)
    {
        Prop = p;
        Opt = o;
        _swatch = UiKit.ColorSwatch("#808080", 22);
        var g = UiKit.Grid(UiKit.Auto, UiKit.Star());
        _swatch.Margin = new Thickness(0, 0, 6, 0);
        _swatch.Cursor = Cursors.Hand;
        _swatch.ToolTip = Loc.T("Clique para escolher uma cor", "Click to pick a color");
        _swatch.MouseLeftButtonUp += (_, _) => PickColor();
        g.Children.Add(_swatch.At(0));
        g.Children.Add(_box.At(1));
        Control = g;
        _box.TextChanged += (_, _) =>
        {
            if (Core.Colors.RgbColor.TryParse(_box.Text, out var c)) _swatch.Background = Theme.Brush(c.Hex);
            RaiseChanged();
        };
    }

    private void PickColor()
    {
        var current = Core.Colors.RgbColor.TryParse(_box.Text, out var c) ? c : Core.Colors.RgbColor.Gray;
        var picked = ColorPickerWindow.Pick(current);
        if (picked.HasValue) _box.Text = picked.Value.Hex;
    }

    public override void Load(object target) => _box.Text = Prop.GetValue(target)?.ToString() ?? "#808080";

    public override bool Store(object target, out string? error)
    {
        error = null;
        if (!Core.Colors.RgbColor.TryParse(_box.Text, out var c))
        {
            error = Loc.T("cor inválida (use #RRGGBB)", "invalid color (use #RRGGBB)");
            return false;
        }
        Prop.SetValue(target, c.Hex);
        return true;
    }
}

internal sealed class PathEditor : FieldEditor
{
    private readonly TextBox _box = new();

    public PathEditor(PropertyInfo p, OptAttribute o, OptKind kind)
    {
        Prop = p;
        Opt = o;
        var g = UiKit.Grid(UiKit.Star(), UiKit.Auto);
        g.Children.Add(_box.At(0));
        var b = UiKit.Button("…", () =>
        {
            if (kind == OptKind.Folder)
            {
                var d = new Microsoft.Win32.OpenFolderDialog();
                if (!string.IsNullOrWhiteSpace(_box.Text) && Directory.Exists(_box.Text)) d.InitialDirectory = _box.Text;
                if (d.ShowDialog() == true) _box.Text = d.FolderName;
            }
            else if (kind == OptKind.SaveFile)
            {
                var d = new Microsoft.Win32.SaveFileDialog { Filter = string.IsNullOrEmpty(o.Filter) ? "*.*|*.*" : o.Filter, FileName = _box.Text };
                if (d.ShowDialog() == true) _box.Text = d.FileName;
            }
            else
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = string.IsNullOrEmpty(o.Filter) ? "*.*|*.*" : o.Filter };
                if (d.ShowDialog() == true) _box.Text = d.FileName;
            }
        });
        b.Margin = new Thickness(6, 0, 0, 0);
        b.Padding = new Thickness(8, 2, 8, 2);
        g.Children.Add(b.At(1));
        Control = g;
        _box.TextChanged += (_, _) => RaiseChanged();
    }

    public override void Load(object target) => _box.Text = Prop.GetValue(target)?.ToString() ?? "";

    public override bool Store(object target, out string? error)
    {
        error = null;
        if (Opt.Required && string.IsNullOrWhiteSpace(_box.Text))
        {
            error = Loc.T("obrigatório", "required");
            return false;
        }
        Prop.SetValue(target, _box.Text?.Trim() ?? "");
        return true;
    }
}

internal sealed class ChoiceEditor : FieldEditor
{
    private readonly ComboBox _combo = new() { IsEditable = false };
    private readonly List<(string Label, string Value)> _items;
    private string? _pending;

    public ChoiceEditor(PropertyInfo p, OptAttribute o, OptKind kind, Document? doc)
    {
        Prop = p;
        Opt = o;
        _items = doc == null ? new List<(string, string)>() : SafeChoices(doc, kind, o.Filter);
        if (!o.Required) _items.Insert(0, (Loc.T("(nenhum)", "(none)"), ""));
        foreach (var it in _items) _combo.Items.Add(it.Label);
        _combo.IsTextSearchEnabled = true;
        Control = _combo;
        _combo.SelectionChanged += (_, _) => RaiseChanged();
    }

    private static List<(string, string)> SafeChoices(Document doc, OptKind kind, string filter)
    {
        try
        {
            return Lookup.Choices(doc, kind, string.IsNullOrWhiteSpace(filter) ? null : filter);
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao listar opções", ex);
            return new List<(string, string)>();
        }
    }

    public override void Load(object target)
    {
        var v = Prop.GetValue(target)?.ToString() ?? "";
        var idx = _items.FindIndex(i => i.Value == v);
        if (idx < 0 && Opt.Required && _items.Count > 0) idx = 0;
        if (idx < 0 && !string.IsNullOrEmpty(v))
        {
            // Valor de preset que não existe neste projeto: mostra como indisponível.
            _pending = v;
            _combo.Items.Add(Loc.F("{0} (não encontrado)", "{0} (not found)", v));
            idx = _combo.Items.Count - 1;
        }
        _combo.SelectedIndex = Math.Max(idx, _items.Count > 0 || _pending != null ? Math.Max(idx, 0) : -1);
    }

    public override bool Store(object target, out string? error)
    {
        error = null;
        var idx = _combo.SelectedIndex;
        string value;
        if (idx >= 0 && idx < _items.Count) value = _items[idx].Value;
        else if (idx >= _items.Count && _pending != null) value = _pending;
        else value = "";
        if (Opt.Required && string.IsNullOrEmpty(value))
        {
            error = _items.Count == 0 ? Loc.T("nenhum item disponível no projeto", "no item available in the project") : Loc.T("obrigatório", "required");
            return false;
        }
        if (idx >= _items.Count && _pending != null && Opt.Required)
        {
            error = Loc.T("item não existe neste projeto", "item does not exist in this project");
            return false;
        }
        Prop.SetValue(target, value);
        return true;
    }
}
