using TeraTools.Core.Colors;
using TeraTools.Core.Geometry;
using TeraTools.Core.Mathx;
using TeraTools.Core.Units;

namespace TeraTools.Core.Imaging;

/// <summary>Imagem em memória (RGBA, 8 bits por canal, linha 0 = topo).</summary>
public sealed class PixelImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }

    public PixelImage(int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Imagem vazia.");
        if (rgba.Length < width * height * 4) throw new ArgumentException("Buffer de imagem incompleto.");
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    /// <summary>Cria a partir de pixels BGRA32 (formato do WPF), respeitando o "stride".</summary>
    public static PixelImage FromBgra32(int width, int height, int stride, byte[] bgra)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var s = y * stride + x * 4;
                var d = (y * width + x) * 4;
                rgba[d] = bgra[s + 2];
                rgba[d + 1] = bgra[s + 1];
                rgba[d + 2] = bgra[s];
                rgba[d + 3] = bgra[s + 3];
            }
        }
        return new PixelImage(width, height, rgba);
    }

    public (byte R, byte G, byte B, byte A) Get(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
    }

    /// <summary>Média de cor (e alfa) numa área retangular de pixels.</summary>
    public (RgbColor Color, double Alpha) Average(double x0, double y0, double x1, double y1)
    {
        var ix0 = Math.Clamp((int)Math.Floor(x0), 0, Width - 1);
        var iy0 = Math.Clamp((int)Math.Floor(y0), 0, Height - 1);
        var ix1 = Math.Clamp((int)Math.Ceiling(x1) - 1, ix0, Width - 1);
        var iy1 = Math.Clamp((int)Math.Ceiling(y1) - 1, iy0, Height - 1);
        double r = 0, g = 0, b = 0, a = 0, wsum = 0;
        for (var y = iy0; y <= iy1; y++)
        {
            for (var x = ix0; x <= ix1; x++)
            {
                var (pr, pg, pb, pa) = Get(x, y);
                var w = pa / 255.0;
                r += pr * w;
                g += pg * w;
                b += pb * w;
                a += pa;
                wsum += w;
            }
        }
        var n = (ix1 - ix0 + 1) * (iy1 - iy0 + 1);
        var alpha = a / (255.0 * n);
        if (wsum < 1e-9) return (RgbColor.White, 0);
        return (new RgbColor((byte)Math.Round(r / wsum), (byte)Math.Round(g / wsum), (byte)Math.Round(b / wsum)), alpha);
    }
}

public enum BlockColorMode
{
    /// <summary>Um único material (só relevo).</summary>
    Single,
    /// <summary>Paleta automática com N cores (k-means).</summary>
    AutoPalette,
    /// <summary>Cores definidas pelo usuário (cada cor vira um material).</summary>
    FixedPalette,
    /// <summary>Dois materiais separados por um limiar de brilho.</summary>
    Threshold,
    /// <summary>Faixas de tons de cinza (N níveis).</summary>
    GrayLevels,
}

public enum HeightSource
{
    Flat,
    Brightness,
    Darkness,
}

public sealed class ImageBlockSettings
{
    /// <summary>Número de colunas (resolução horizontal).</summary>
    public int Columns { get; set; } = 40;

    /// <summary>Largura total do painel/mosaico (pés).</summary>
    public double TotalWidth { get; set; } = U.M(2);

    /// <summary>Junta entre blocos (pés).</summary>
    public double Gap { get; set; } = U.Mm(5);

    public HeightSource HeightSource { get; set; } = HeightSource.Brightness;
    public double MinHeight { get; set; } = U.Mm(10);
    public double MaxHeight { get; set; } = U.Mm(80);

    /// <summary>Níveis de altura (0 = contínuo). Quantizar reduz tipos e facilita a fabricação.</summary>
    public int HeightLevels { get; set; } = 6;

    public double Brightness { get; set; }
    public double Contrast { get; set; }
    public double Gamma { get; set; } = 1.0;
    public bool Invert { get; set; }

    public BlockColorMode ColorMode { get; set; } = BlockColorMode.AutoPalette;
    public int PaletteSize { get; set; } = 4;
    public List<string> FixedPalette { get; set; } = new() { "#2B2B2B", "#7A7A7A", "#C8C8C8", "#F2F2F2" };
    public double Threshold { get; set; } = 0.5;

    /// <summary>Ignora células transparentes (alfa &lt; 0,5).</summary>
    public bool SkipTransparent { get; set; } = true;

    /// <summary>Ignora células com brilho acima deste valor (0..1, 1 = não ignora) — remove fundo branco.</summary>
    public double SkipBrighterThan { get; set; } = 1.0;

    /// <summary>Limite de blocos: se exceder, a resolução é reduzida automaticamente.</summary>
    public int MaxElements { get; set; } = 3000;

    public int Seed { get; set; } = 7;
}

public sealed class ImageBlock
{
    public int Column { get; set; }
    public int Row { get; set; }

    /// <summary>Centro do bloco (origem no canto inferior esquerdo do mosaico; Y para cima).</summary>
    public Vec2 Center { get; set; }

    public double Size { get; set; }
    public double Height { get; set; }
    public double Value { get; set; }
    public RgbColor Color { get; set; }
    public int PaletteIndex { get; set; }
    public int HeightLevel { get; set; }
}

public sealed class ImageBlockResult
{
    public List<ImageBlock> Blocks { get; } = new();
    public int Columns { get; set; }
    public int Rows { get; set; }
    public double CellSize { get; set; }
    public List<RgbColor> Palette { get; } = new();
    public bool ResolutionReduced { get; set; }
    public double TotalWidth => Columns * CellSize;
    public double TotalHeight => Rows * CellSize;

    public Dictionary<int, int> CountByPalette() =>
        Blocks.GroupBy(b => b.PaletteIndex).ToDictionary(g => g.Key, g => g.Count());

    public Dictionary<int, int> CountByHeightLevel() =>
        Blocks.GroupBy(b => b.HeightLevel).ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>Converte uma imagem em uma matriz de blocos (mosaicos, relevos e painéis).</summary>
public static class ImageToBlocks
{
    public static ImageBlockResult Generate(PixelImage img, ImageBlockSettings s)
    {
        var cols = Math.Max(1, s.Columns);
        var aspect = (double)img.Height / img.Width;
        var rows = Math.Max(1, (int)Math.Round(cols * aspect));
        var reduced = false;
        if (s.MaxElements > 0 && cols * rows > s.MaxElements)
        {
            var f = Math.Sqrt((double)s.MaxElements / (cols * rows));
            cols = Math.Max(1, (int)Math.Floor(cols * f));
            rows = Math.Max(1, (int)Math.Round(cols * aspect));
            while (cols * rows > s.MaxElements && cols > 1)
            {
                cols--;
                rows = Math.Max(1, (int)Math.Round(cols * aspect));
            }
            reduced = true;
        }

        var cell = s.TotalWidth / cols;
        var result = new ImageBlockResult { Columns = cols, Rows = rows, CellSize = cell, ResolutionReduced = reduced };
        var pxW = (double)img.Width / cols;
        var pxH = (double)img.Height / rows;

        var samples = new List<(int C, int R, RgbColor Col, double V)>(cols * rows);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var (col, alpha) = img.Average(c * pxW, r * pxH, (c + 1) * pxW, (r + 1) * pxH);
                if (s.SkipTransparent && alpha < 0.5) continue;
                var v = Adjust(col.Brightness, s);
                if (s.SkipBrighterThan < 1 && col.Brightness > s.SkipBrighterThan) continue;
                samples.Add((c, r, col, v));
            }
        }

        // Paleta
        switch (s.ColorMode)
        {
            case BlockColorMode.AutoPalette:
                result.Palette.AddRange(KMeans(samples.Select(x => x.Col).ToList(), Math.Clamp(s.PaletteSize, 1, 32), s.Seed));
                break;
            case BlockColorMode.FixedPalette:
                foreach (var hex in s.FixedPalette)
                    if (RgbColor.TryParse(hex, out var pc)) result.Palette.Add(pc);
                if (result.Palette.Count == 0) result.Palette.Add(RgbColor.Gray);
                break;
            case BlockColorMode.Threshold:
                result.Palette.Add(new RgbColor(40, 40, 40));
                result.Palette.Add(new RgbColor(230, 230, 230));
                break;
            case BlockColorMode.GrayLevels:
                var n = Math.Clamp(s.PaletteSize, 2, 32);
                for (var k = 0; k < n; k++)
                {
                    var g = (byte)Math.Round(255.0 * k / (n - 1));
                    result.Palette.Add(new RgbColor(g, g, g));
                }
                break;
            default:
                result.Palette.Add(RgbColor.Gray);
                break;
        }

        var levels = s.HeightLevels;
        foreach (var (c, r, col, v) in samples)
        {
            var hv = s.HeightSource switch
            {
                HeightSource.Brightness => v,
                HeightSource.Darkness => 1 - v,
                _ => 1.0,
            };
            var level = 0;
            if (levels > 1 && s.HeightSource != HeightSource.Flat)
            {
                level = (int)Math.Round(hv * (levels - 1));
                hv = (double)level / (levels - 1);
            }
            var height = s.HeightSource == HeightSource.Flat ? s.MinHeight : MathUtil.Lerp(s.MinHeight, s.MaxHeight, hv);

            int pi = s.ColorMode switch
            {
                BlockColorMode.Threshold => v < s.Threshold ? 0 : 1,
                BlockColorMode.GrayLevels => (int)Math.Round(v * (result.Palette.Count - 1)),
                BlockColorMode.Single => 0,
                _ => Nearest(result.Palette, col),
            };

            result.Blocks.Add(new ImageBlock
            {
                Column = c,
                Row = r,
                // Linha 0 da imagem é o topo; no modelo o Y cresce para cima.
                Center = new Vec2((c + 0.5) * cell, (rows - r - 0.5) * cell),
                Size = Math.Max(cell - s.Gap, cell * 0.05),
                Height = height,
                Value = v,
                Color = col,
                PaletteIndex = pi,
                HeightLevel = level,
            });
        }
        return result;
    }

    /// <summary>Aplica brilho, contraste, gama e inversão a um valor de 0..1.</summary>
    public static double Adjust(double v, ImageBlockSettings s)
    {
        v = (v - 0.5) * (1 + s.Contrast) + 0.5 + s.Brightness;
        v = MathUtil.Clamp01(v);
        if (Math.Abs(s.Gamma - 1) > 1e-9 && s.Gamma > 0.01)
            v = Math.Pow(v, 1 / s.Gamma);
        if (s.Invert) v = 1 - v;
        return MathUtil.Clamp01(v);
    }

    public static int Nearest(IReadOnlyList<RgbColor> palette, RgbColor c)
    {
        var best = 0;
        var bd = double.MaxValue;
        for (var i = 0; i < palette.Count; i++)
        {
            var d = palette[i].DistanceTo(c);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>K-means (inicialização k-means++) com semente fixa — resultados reproduzíveis.</summary>
    public static List<RgbColor> KMeans(List<RgbColor> colors, int k, int seed, int iterations = 20)
    {
        if (colors.Count == 0) return new List<RgbColor> { RgbColor.Gray };
        var distinct = colors.Distinct().ToList();
        if (distinct.Count <= k) return distinct.OrderBy(c => c.Brightness).ToList();
        var rng = new Rng(seed);
        var centers = new List<(double R, double G, double B)>();
        var first = colors[rng.Next(colors.Count)];
        centers.Add((first.R, first.G, first.B));
        var d2 = new double[colors.Count];
        while (centers.Count < k)
        {
            double sum = 0;
            for (var i = 0; i < colors.Count; i++)
            {
                d2[i] = centers.Min(c => Sq(colors[i], c));
                sum += d2[i];
            }
            var target = rng.NextDouble() * sum;
            var acc = 0.0;
            var pick = colors.Count - 1;
            for (var i = 0; i < colors.Count; i++)
            {
                acc += d2[i];
                if (acc >= target) { pick = i; break; }
            }
            var pc = colors[pick];
            centers.Add((pc.R, pc.G, pc.B));
        }
        var assign = new int[colors.Count];
        for (var it = 0; it < iterations; it++)
        {
            var changed = false;
            for (var i = 0; i < colors.Count; i++)
            {
                var best = 0;
                var bd = double.MaxValue;
                for (var c = 0; c < centers.Count; c++)
                {
                    var d = Sq(colors[i], centers[c]);
                    if (d < bd) { bd = d; best = c; }
                }
                if (assign[i] != best) { assign[i] = best; changed = true; }
            }
            var sums = new (double R, double G, double B, int N)[centers.Count];
            for (var i = 0; i < colors.Count; i++)
            {
                var a = assign[i];
                sums[a] = (sums[a].R + colors[i].R, sums[a].G + colors[i].G, sums[a].B + colors[i].B, sums[a].N + 1);
            }
            for (var c = 0; c < centers.Count; c++)
                if (sums[c].N > 0)
                    centers[c] = (sums[c].R / sums[c].N, sums[c].G / sums[c].N, sums[c].B / sums[c].N);
            if (!changed && it > 0) break;
        }
        return centers
            .Select(c => new RgbColor((byte)Math.Round(c.R), (byte)Math.Round(c.G), (byte)Math.Round(c.B)))
            .Distinct()
            .OrderBy(c => c.Brightness)
            .ToList();
    }

    private static double Sq(RgbColor a, (double R, double G, double B) c) =>
        (a.R - c.R) * (a.R - c.R) + (a.G - c.G) * (a.G - c.G) + (a.B - c.B) * (a.B - c.B);
}
