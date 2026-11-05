namespace TeraTools.Core.Imaging;

/// <summary>Cálculo de tamanho para reduzir imagens mantendo a proporção.</summary>
public static class ImageFit
{
    /// <summary>Largura e altura que cabem em <paramref name="maxSide"/> (nunca aumenta a imagem).</summary>
    public static (int Width, int Height) Fit(int width, int height, int maxSide)
    {
        if (width <= 0 || height <= 0) return (0, 0);
        var s = Math.Min(1.0, (double)maxSide / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * s)), Math.Max(1, (int)Math.Round(height * s)));
    }

    /// <summary>Tamanho legível de arquivo (KB ou MB).</summary>
    public static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
