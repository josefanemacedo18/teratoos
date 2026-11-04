using TeraTools.Core.Serialization;
using TeraTools.Core.Text;

namespace TeraTools.Core.Settings;

public enum PresetScope
{
    /// <summary>Preset pessoal (pasta do usuário).</summary>
    User,
    /// <summary>Preset salvo dentro do projeto (.rvt), compartilhado com quem abre o arquivo.</summary>
    Project,
    /// <summary>Preset de biblioteca compartilhada (pasta da equipe), somente leitura.</summary>
    Library,
}

/// <summary>Conjunto nomeado de parâmetros reutilizável de uma ferramenta (ou item de biblioteca).</summary>
public sealed class Preset
{
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public string Json { get; set; } = "{}";
    public string? Description { get; set; }
    public DateTime Modified { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore]
    public PresetScope Scope { get; set; } = PresetScope.User;

    public override string ToString() => Name;
}

public sealed class PresetFile
{
    public int Version { get; set; } = 1;
    public string Key { get; set; } = "";
    public List<Preset> Presets { get; set; } = new();
}

/// <summary>
/// Armazena presets em arquivos JSON, um por ferramenta/biblioteca: {pasta}/{chave}.json.
/// Também guarda as "últimas opções usadas" de cada ferramenta.
/// </summary>
public sealed class PresetStore
{
    public const string LastUsedName = "__ultimo__";

    private readonly string _folder;
    private readonly string? _libraryFolder;

    public PresetStore(string folder, string? sharedLibraryFolder = null)
    {
        _folder = folder;
        _libraryFolder = string.IsNullOrWhiteSpace(sharedLibraryFolder) ? null : sharedLibraryFolder;
    }

    public string Folder => _folder;

    private string PathFor(string folder, string key) => System.IO.Path.Combine(folder, TextUtil.SafeFileName(key) + ".json");

    private PresetFile Load(string folder, string key)
    {
        var f = Serialization.Json.LoadOrDefault<PresetFile>(PathFor(folder, key));
        f.Key = key;
        return f;
    }

    /// <summary>Presets do usuário + biblioteca compartilhada (sem o "último usado").</summary>
    public List<Preset> List(string key)
    {
        var result = Load(_folder, key).Presets
            .Where(p => p.Name != LastUsedName)
            .Select(p => { p.Scope = PresetScope.User; return p; })
            .ToList();
        if (_libraryFolder != null)
        {
            try
            {
                result.AddRange(Load(_libraryFolder, key).Presets
                    .Where(p => p.Name != LastUsedName)
                    .Select(p => { p.Scope = PresetScope.Library; return p; }));
            }
            catch
            {
                // pasta da equipe inacessível: segue só com presets pessoais
            }
        }
        return result.OrderBy(p => p.Name, TextUtil.NaturalComparer).ToList();
    }

    public void Save(string key, Preset preset)
    {
        var file = Load(_folder, key);
        preset.Key = key;
        preset.Modified = DateTime.Now;
        file.Presets.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        file.Presets.Add(preset);
        Serialization.Json.Save(PathFor(_folder, key), file);
    }

    public bool Delete(string key, string name)
    {
        var file = Load(_folder, key);
        var n = file.Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (n > 0) Serialization.Json.Save(PathFor(_folder, key), file);
        return n > 0;
    }

    public string? GetLastUsed(string key) =>
        Load(_folder, key).Presets.FirstOrDefault(p => p.Name == LastUsedName)?.Json;

    public void SetLastUsed(string key, string json) =>
        Save(key, new Preset { Name = LastUsedName, Json = json });

    public void Export(string path, string key, IEnumerable<Preset> presets)
    {
        var file = new PresetFile { Key = key, Presets = presets.ToList() };
        Serialization.Json.Save(path, file);
    }

    /// <summary>Importa presets de um arquivo exportado. Retorna quantos foram importados.</summary>
    public int Import(string path, string expectedKey)
    {
        var file = Serialization.Json.Deserialize<PresetFile>(Serialization.Json.ReadFile(path));
        if (file == null) return 0;
        if (!string.IsNullOrEmpty(file.Key) && !string.Equals(file.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization.Loc.F(
                "Este arquivo contém presets de outra ferramenta ({0}).",
                "This file contains presets for another tool ({0}).", file.Key));
        var n = 0;
        foreach (var p in file.Presets.Where(p => p.Name != LastUsedName))
        {
            Save(expectedKey, p);
            n++;
        }
        return n;
    }
}

/// <summary>Presets guardados no projeto (dentro das configurações do projeto).</summary>
public sealed class ProjectPresets
{
    public Dictionary<string, List<Preset>> ByKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<Preset> List(string key) =>
        ByKey.TryGetValue(key, out var l)
            ? l.Select(p => { p.Scope = PresetScope.Project; return p; }).ToList()
            : new List<Preset>();

    public void Save(string key, Preset preset)
    {
        if (!ByKey.TryGetValue(key, out var l))
            ByKey[key] = l = new List<Preset>();
        preset.Key = key;
        preset.Modified = DateTime.Now;
        l.RemoveAll(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        l.Add(preset);
    }

    public bool Delete(string key, string name) =>
        ByKey.TryGetValue(key, out var l) && l.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
}
