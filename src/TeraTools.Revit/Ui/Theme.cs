using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TeraTools.Revit.Ui;

public partial class StylesDictionary : ResourceDictionary
{
    public StylesDictionary() => InitializeComponent();
}

/// <summary>Tema visual (claro/escuro) acompanhando o tema do Revit.</summary>
public static class Theme
{
    private static ResourceDictionary? _resources;

    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    private static readonly (string Key, string Light, string Dark)[] Palette =
    {
        ("TT.Bg", "#FFFFFF", "#2A2E35"),
        ("TT.Surface", "#F5F6F8", "#31363F"),
        ("TT.Surface2", "#ECEEF2", "#3A404B"),
        ("TT.Border", "#D3D8DF", "#4A515E"),
        ("TT.Text", "#1F2933", "#E8EAED"),
        ("TT.Muted", "#5F6B7A", "#A9B0BB"),
        ("TT.Accent", "#B5652B", "#D07A3A"),
        ("TT.AccentHover", "#9E5523", "#E08A4A"),
        ("TT.AccentText", "#FFFFFF", "#FFFFFF"),
        ("TT.Hover", "#E9EEF5", "#404754"),
        ("TT.Selected", "#F6E3D4", "#5A4535"),
        ("TT.Input", "#FFFFFF", "#252931"),
        ("TT.Error", "#C0392B", "#F07A6E"),
        ("TT.Warning", "#B7791F", "#E7B04F"),
        ("TT.Success", "#2F7D4F", "#5FC48A"),
        ("TT.Info", "#3B7DD8", "#79A8F0"),
    };

    public static void Detect()
    {
        try
        {
            IsDark = Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark;
        }
        catch
        {
            IsDark = false;
        }
    }

    public static ResourceDictionary Resources
    {
        get
        {
            if (_resources != null) return _resources;
            _resources = new ResourceDictionary();
            ApplyBrushes(_resources);
            try
            {
                _resources.MergedDictionaries.Add(new StylesDictionary());
            }
            catch (Exception ex)
            {
                Infrastructure.Log.Error("Falha ao carregar estilos", ex);
            }
            return _resources;
        }
    }

    private static void ApplyBrushes(ResourceDictionary rd)
    {
        foreach (var (key, light, dark) in Palette)
            rd[key] = Brush(IsDark ? dark : light);
    }

    public static void Refresh()
    {
        Detect();
        if (_resources != null) ApplyBrushes(_resources);
        Changed?.Invoke();
    }

    public static string Hex(string key)
    {
        foreach (var (k, light, dark) in Palette)
            if (k == key) return IsDark ? dark : light;
        return "#808080";
    }

    public static SolidColorBrush Get(string key) => Brush(Hex(key));

    public static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush(ParseColor(hex));
        b.Freeze();
        return b;
    }

    public static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Gray;
        }
    }

    /// <summary>Aplica tema, fonte e recursos a uma janela e define o Revit como dono.</summary>
    public static void Apply(Window w, IntPtr owner)
    {
        w.Resources.MergedDictionaries.Add(Resources);
        w.SetResourceReference(Window.BackgroundProperty, "TT.Bg");
        w.SetResourceReference(Window.ForegroundProperty, "TT.Text");
        w.FontFamily = new FontFamily("Segoe UI");
        w.FontSize = 12;
        w.ShowInTaskbar = false;
        w.WindowStartupLocation = owner != IntPtr.Zero ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
        if (owner != IntPtr.Zero)
        {
            try
            {
                new WindowInteropHelper(w) { Owner = owner };
            }
            catch
            {
                // sem dono: segue
            }
        }
        System.Windows.Media.TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
    }

    public static void Apply(FrameworkElement e)
    {
        e.Resources.MergedDictionaries.Add(Resources);
        if (e is System.Windows.Controls.Control c)
        {
            c.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "TT.Bg");
            c.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TT.Text");
            c.FontFamily = new FontFamily("Segoe UI");
            c.FontSize = 12;
        }
    }
}
