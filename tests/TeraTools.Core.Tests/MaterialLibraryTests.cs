using TeraTools.Core.Materials;
using Xunit;

namespace TeraTools.Core.Tests;

public class MaterialLibraryTests
{
    private static string TexturesFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "assets", "texturas"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "assets", "texturas");
    }

    [Fact]
    public void BibliotecaTemNomesECodigosUnicos()
    {
        var lib = MaterialLibrary.Starter();
        Assert.True(lib.Items.Count >= 90, $"só {lib.Items.Count} materiais");
        Assert.Equal(lib.Items.Count, lib.Items.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(lib.Items.Count, lib.Items.Select(i => i.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TodaTexturaCitadaExisteNaPasta()
    {
        var folder = TexturesFolder();
        var textured = MaterialLibrary.Starter().Items.Where(i => !string.IsNullOrWhiteSpace(i.Texture)).ToList();
        Assert.True(textured.Count >= 50);
        foreach (var i in textured)
        {
            Assert.True(File.Exists(Path.Combine(folder, i.Texture!)), i.Texture);
            Assert.True(i.TextureSizeMm > 0, i.Name);
            Assert.Matches("^#[0-9A-F]{6}$", i.ColorHex);
        }
    }

    [Fact]
    public void TexturasTemLicencaDocumentada() =>
        Assert.Contains("CC0", File.ReadAllText(Path.Combine(TexturesFolder(), "LEIA-ME.txt")));
}
