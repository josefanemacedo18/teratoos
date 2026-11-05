using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Texturas com imagem nos materiais do Revit: a imagem vira o mapa de cor de uma aparência genérica
/// própria do material ("TT nome"), na escala real informada.
/// </summary>
public static class Textures
{
    /// <summary>
    /// Caminho estável da imagem. Nomes simples vêm da pasta de texturas do plugin e são copiados para
    /// %AppData%\TeraTools\texturas (o caminho gravado no material não muda quando o plugin é atualizado).
    /// </summary>
    public static string? Resolve(string? texture)
    {
        if (string.IsNullOrWhiteSpace(texture)) return null;
        try
        {
            if (Path.IsPathRooted(texture)) return File.Exists(texture) ? texture : null;
            var dst = Path.Combine(Paths.Textures, texture);
            if (File.Exists(dst)) return dst;
            var src = Path.Combine(Paths.BundledTextures, texture);
            if (!File.Exists(src)) return null;
            File.Copy(src, dst, true);
            return dst;
        }
        catch (Exception ex)
        {
            Log.Warn($"Textura {texture}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Imagens disponíveis na pasta do plugin (para listas).</summary>
    public static IEnumerable<string> Bundled() =>
        Directory.Exists(Paths.BundledTextures) ? Directory.GetFiles(Paths.BundledTextures, "*.jpg").Select(Path.GetFileName).OfType<string>() : Array.Empty<string>();

    /// <summary>Aplica a imagem como textura de cor do material, com o tamanho real (pés). Deve rodar numa transação.</summary>
    public static void Apply(Document doc, Material m, string imagePath, double sizeFeet)
    {
        var name = "TT " + m.Name;
        var elem = AppearanceAssetElement.GetAppearanceAssetElementByName(doc, name);
        if (elem == null)
        {
            elem = GenericSource(doc) switch
            {
                AppearanceAssetElement e => e.Duplicate(name),
                Asset a => AppearanceAssetElement.Create(doc, name, a),
                _ => throw new ToolException(Loc.T("não há aparência genérica no projeto para usar como base", "no generic appearance in the project")),
            };
        }
        m.AppearanceAssetId = elem.Id;
        using var scope = new AppearanceAssetEditScope(doc);
        var asset = scope.Start(elem.Id);
        var diffuse = asset.FindByName(Generic.GenericDiffuse)
                      ?? throw new ToolException(Loc.T("a aparência não é do tipo genérico", "the appearance is not generic"));
        Asset? bitmap = diffuse.NumberOfConnectedProperties > 0 ? diffuse.GetSingleConnectedAsset() : null;
        if (bitmap == null)
        {
            diffuse.AddConnectedAsset("UnifiedBitmapSchema");
            bitmap = diffuse.GetSingleConnectedAsset();
        }
        if (bitmap?.FindByName(UnifiedBitmap.UnifiedbitmapBitmap) is AssetPropertyString path) path.Value = imagePath;
        else throw new ToolException(Loc.T("não foi possível ligar a imagem à aparência", "could not attach the image"));
        foreach (var key in new[] { UnifiedBitmap.TextureRealWorldScaleX, UnifiedBitmap.TextureRealWorldScaleY })
            if (bitmap.FindByName(key) is AssetPropertyDistance d && d.IsEditable())
                d.Value = UnitUtils.ConvertFromInternalUnits(sizeFeet, d.GetUnitTypeId());
        scope.Commit(true);
    }

    /// <summary>Uma aparência genérica para duplicar: do projeto ou da biblioteca do Revit.</summary>
    private static object? GenericSource(Document doc)
    {
        foreach (var e in new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>())
        {
            try
            {
                if (e.GetRenderingAsset() is { Size: > 0 } a && a.FindByName(Generic.GenericDiffuse) != null) return e;
            }
            catch
            {
                // aparência ilegível: tenta a próxima
            }
        }
        try
        {
            foreach (var a in doc.Application.GetAssets(AssetType.Appearance))
                if (a.FindByName(Generic.GenericDiffuse) != null) return a;
        }
        catch (Exception ex)
        {
            Log.Warn("Biblioteca de aparências: " + ex.Message);
        }
        return null;
    }
}
