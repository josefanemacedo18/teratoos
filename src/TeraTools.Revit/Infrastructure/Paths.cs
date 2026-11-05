namespace TeraTools.Revit.Infrastructure;

/// <summary>Pastas usadas pelo plugin (todas dentro de %AppData%\TeraTools).</summary>
public static class Paths
{
    public const string RevitVersion = "2027";

    public static string Root { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeraTools");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string HistoryFile => Path.Combine(Root, "historico.jsonl");
    public static string Presets => Ensure(Path.Combine(Root, "presets"));
    public static string Libraries => Ensure(Path.Combine(Root, "bibliotecas"));
    public static string Logs => Ensure(Path.Combine(Root, "logs"));
    public static string Help => Ensure(Path.Combine(Root, "ajuda"));
    public static string Families => Ensure(Path.Combine(Root, "familias", RevitVersion));
    public static string Exports => Ensure(Path.Combine(Root, "exportacoes"));
    public static string Temp => Ensure(Path.Combine(Root, "temp"));

    /// <summary>Texturas usadas pelos materiais (cópia estável: não muda quando o plugin é atualizado).</summary>
    public static string Textures => Ensure(Path.Combine(Root, "texturas"));

    /// <summary>Texturas que vêm com o plugin (pasta Texturas ao lado da DLL).</summary>
    public static string BundledTextures => Path.Combine(AddinFolder, "Texturas");

    /// <summary>Pasta onde está a DLL do plugin.</summary>
    public static string AddinFolder => Path.GetDirectoryName(typeof(Paths).Assembly.Location) ?? Root;

    public static string Ensure(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            // sem permissão: segue (operações de escrita mostrarão o erro)
        }
        return dir;
    }

    public static void UseRoot(string root) => Root = root;
}
