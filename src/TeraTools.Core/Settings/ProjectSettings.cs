using TeraTools.Core.Serialization;

namespace TeraTools.Core.Settings;

/// <summary>
/// Configurações gravadas DENTRO do projeto Revit (Extensible Storage).
/// Organizadas em seções JSON independentes, para que cada módulo guarde o que precisa
/// sem depender dos outros (presets, padrões de nomes, regras de verificação, orçamento...).
/// </summary>
public sealed class ProjectSettings
{
    public const string SectionPresets = "presets";
    public const string SectionNaming = "naming";
    public const string SectionRules = "rules";
    public const string SectionCosts = "costs";
    public const string SectionInfo = "info";
    public const string SectionThemes = "themes";
    public const string SectionMaterials = "materials";
    public const string SectionJoinery = "joinery";

    public int Version { get; set; } = 1;

    public DateTime Modified { get; set; } = DateTime.Now;

    public Dictionary<string, string> Sections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public T GetSection<T>(string key) where T : new()
    {
        if (Sections.TryGetValue(key, out var json))
        {
            try
            {
                return Json.Deserialize<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }
        return new T();
    }

    public void SetSection<T>(string key, T value)
    {
        Sections[key] = Json.Serialize(value, indented: false);
        Modified = DateTime.Now;
    }

    public bool HasSection(string key) => Sections.ContainsKey(key);

    public string ToJson() => Json.Serialize(this, indented: false);

    public static ProjectSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ProjectSettings();
        try
        {
            return Json.Deserialize<ProjectSettings>(json) ?? new ProjectSettings();
        }
        catch
        {
            return new ProjectSettings();
        }
    }
}

/// <summary>Dados gerais do projeto usados em carimbos, relatórios e nomes.</summary>
public sealed class ProjectInfoSection
{
    public string ProjectCode { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Phase { get; set; } = "";
    public Dictionary<string, string> Extra { get; set; } = new();
}
