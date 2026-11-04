using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TeraTools.Revit.Ui;

/// <summary>Atalhos para montar interfaces WPF em código, já com o tema aplicado.</summary>
public static class UiKit
{
    public static TextBlock Text(string text, double size = 12, bool bold = false, string colorKey = "TT.Text", bool wrap = true, Thickness? margin = null)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            Margin = margin ?? new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        t.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
        return t;
    }

    public static TextBlock Muted(string text, double size = 11.5, bool wrap = true, Thickness? margin = null) =>
        Text(text, size, false, "TT.Muted", wrap, margin);

    public static TextBlock Title(string text) => Text(text, 16, true, margin: new Thickness(0, 0, 0, 2));

    public static Button Button(string text, Action onClick, bool accent = false, string? tooltip = null, double minWidth = 0)
    {
        var b = new Button { Content = text, MinWidth = minWidth };
        if (accent) b.SetResourceReference(FrameworkElement.StyleProperty, "TT.AccentButton");
        if (!string.IsNullOrEmpty(tooltip)) b.ToolTip = tooltip;
        b.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (OperationCanceledException)
            {
                // cancelado pelo usuário
            }
            catch (Exception ex)
            {
                Infrastructure.Log.Error("Erro em botão", ex);
                Msg.Error(ex.Message);
            }
        };
        return b;
    }

    public static Button FlatButton(object content, Action onClick, string? tooltip = null)
    {
        var b = new Button { Content = content };
        b.SetResourceReference(FrameworkElement.StyleProperty, "TT.FlatButton");
        if (!string.IsNullOrEmpty(tooltip)) b.ToolTip = tooltip;
        b.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Infrastructure.Log.Error("Erro em botão", ex);
                Msg.Error(ex.Message);
            }
        };
        return b;
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] is FrameworkElement fe && i > 0)
                fe.Margin = new Thickness(spacing, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
            p.Children.Add(children[i]);
        }
        return p;
    }

    public static StackPanel Col(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] is FrameworkElement fe && i > 0)
                fe.Margin = new Thickness(fe.Margin.Left, spacing, fe.Margin.Right, fe.Margin.Bottom);
            p.Children.Add(children[i]);
        }
        return p;
    }

    public static Border Card(UIElement child, Thickness? padding = null)
    {
        var b = new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = padding ?? new Thickness(10),
        };
        b.SetResourceReference(Border.BackgroundProperty, "TT.Surface");
        b.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        return b;
    }

    public static Border Divider(Thickness? margin = null)
    {
        var b = new Border { Height = 1, Margin = margin ?? new Thickness(0, 8, 0, 8) };
        b.SetResourceReference(Border.BackgroundProperty, "TT.Border");
        return b;
    }

    public static Grid Grid(params GridLength[] columns)
    {
        var g = new Grid();
        foreach (var c in columns) g.ColumnDefinitions.Add(new ColumnDefinition { Width = c });
        return g;
    }

    public static GridLength Star(double v = 1) => new(v, GridUnitType.Star);
    public static GridLength Auto => GridLength.Auto;
    public static GridLength Px(double v) => new(v);

    public static T At<T>(this T e, int column, int row = 0) where T : UIElement
    {
        System.Windows.Controls.Grid.SetColumn(e, column);
        System.Windows.Controls.Grid.SetRow(e, row);
        return e;
    }

    public static Border ColorSwatch(string hex, double size = 14)
    {
        var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1) };
        b.Background = Theme.Brush(hex);
        b.SetResourceReference(Border.BorderBrushProperty, "TT.Border");
        return b;
    }

    public static Brush SeverityBrush(Core.Reporting.Severity s) => s switch
    {
        Core.Reporting.Severity.Error => Theme.Get("TT.Error"),
        Core.Reporting.Severity.Warning => Theme.Get("TT.Warning"),
        Core.Reporting.Severity.Success => Theme.Get("TT.Success"),
        _ => Theme.Get("TT.Info"),
    };
}
