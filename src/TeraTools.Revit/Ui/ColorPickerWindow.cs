using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeraTools.Core.Colors;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Ui;

/// <summary>Seletor de cor simples (paleta + código hexadecimal).</summary>
public sealed class ColorPickerWindow : Window
{
    private readonly TextBox _hex = new();
    private readonly Border _preview;
    private RgbColor? _result;

    private ColorPickerWindow(RgbColor current)
    {
        Title = Loc.T("Escolher cor", "Pick a color");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        Theme.Apply(this, IntPtr.Zero);

        var grid = new UniformGrid12();
        // Linhas: tons de cinza + 11 matizes em 5 luminosidades.
        for (var l = 0; l < 6; l++)
        {
            var g = (byte)(255 - l * 51);
            grid.Add(Swatch(new RgbColor(g, g, g)));
        }
        foreach (var h in new[] { 0.0, 20, 35, 50, 90, 140, 175, 200, 220, 260, 300, 330 })
        {
            foreach (var l in new[] { 0.85, 0.7, 0.55, 0.42, 0.3 })
                grid.Add(Swatch(RgbColor.FromHsl(h, h == 35 ? 0.45 : 0.55, l)));
            grid.Add(Swatch(RgbColor.FromHsl(h, 0.2, 0.6)));
        }

        _preview = UiKit.ColorSwatch(current.Hex, 28);
        _hex.Text = current.Hex;
        _hex.Width = 110;
        _hex.TextChanged += (_, _) =>
        {
            if (RgbColor.TryParse(_hex.Text, out var c)) _preview.Background = Theme.Brush(c.Hex);
        };

        var ok = UiKit.Button("OK", () =>
        {
            if (RgbColor.TryParse(_hex.Text, out var c))
            {
                _result = c;
                DialogResult = true;
            }
        }, accent: true, minWidth: 80);
        ok.IsDefault = true;
        var cancel = UiKit.Button(Loc.T("Cancelar", "Cancel"), () => DialogResult = false, minWidth: 80);
        cancel.IsCancel = true;

        var bottom = UiKit.Row(8, _preview, _hex, cancel, ok);
        Content = new Border
        {
            Padding = new Thickness(14),
            Child = UiKit.Col(10, grid.Panel, bottom),
        };
    }

    private Border Swatch(RgbColor c)
    {
        var b = UiKit.ColorSwatch(c.Hex, 22);
        b.Margin = new Thickness(2);
        b.Cursor = Cursors.Hand;
        b.ToolTip = c.Hex;
        b.MouseLeftButtonUp += (_, _) => _hex.Text = c.Hex;
        return b;
    }

    public static RgbColor? Pick(RgbColor current)
    {
        var w = new ColorPickerWindow(current);
        return w.ShowDialog() == true ? w._result : null;
    }

    private sealed class UniformGrid12
    {
        public System.Windows.Controls.Primitives.UniformGrid Panel { get; } = new() { Columns = 13 };

        public void Add(UIElement e) => Panel.Children.Add(e);
    }
}
