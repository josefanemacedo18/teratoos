using System.Windows.Media;
using System.Windows.Media.Imaging;
using TeraTools.Core.Imaging;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Leitura de imagens (PNG, JPG, BMP, TIFF, GIF) para os geradores.</summary>
public static class ImageLoader
{
    private static readonly Dictionary<string, (DateTime Stamp, PixelImage Image)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static PixelImage Load(string path, int maxSide = 1600)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ToolException(Loc.T("Escolha um arquivo de imagem válido.", "Choose a valid image file."));
        var stamp = File.GetLastWriteTimeUtc(path);
        if (Cache.TryGetValue(path, out var c) && c.Stamp == stamp) return c.Image;

        BitmapSource src;
        using (var fs = File.OpenRead(path))
        {
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            src = dec.Frames[0];
        }
        if (Math.Max(src.PixelWidth, src.PixelHeight) > maxSide)
        {
            var s = (double)maxSide / Math.Max(src.PixelWidth, src.PixelHeight);
            src = new TransformedBitmap(src, new ScaleTransform(s, s));
        }
        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var w = conv.PixelWidth;
        var h = conv.PixelHeight;
        var stride = w * 4;
        var buf = new byte[stride * h];
        conv.CopyPixels(buf, stride, 0);
        var img = PixelImage.FromBgra32(w, h, stride, buf);
        Cache[path] = (stamp, img);
        return img;
    }

    /// <summary>Função de brilho (0..1) em coordenadas normalizadas (u para a direita, v para cima).</summary>
    public static Func<double, double, double> BrightnessSampler(string path, bool invert = false)
    {
        var img = Load(path, 600);
        return (u, v) =>
        {
            var x = Math.Clamp((int)(u * (img.Width - 1)), 0, img.Width - 1);
            var y = Math.Clamp((int)((1 - v) * (img.Height - 1)), 0, img.Height - 1);
            var (r, g, b, a) = img.Get(x, y);
            var br = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0 * (a / 255.0) + (1 - a / 255.0);
            return invert ? 1 - br : br;
        };
    }
}
