using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Serialization;
using TeraTools.Core.Settings;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Serviços globais do plugin (configurações, presets, histórico).</summary>
public static class Services
{
    private static GlobalSettings? _settings;
    private static PresetStore? _presets;
    private static HistoryLog? _history;

    public static GlobalSettings Settings => _settings ??= Json.LoadOrDefault<GlobalSettings>(Paths.SettingsFile);

    public static PresetStore Presets => _presets ??= new PresetStore(Paths.Presets, Settings.SharedLibraryFolder);

    public static HistoryLog History => _history ??= new HistoryLog(Paths.HistoryFile);

    /// <summary>Disparado quando favoritos/recentes/histórico mudam (o painel se atualiza).</summary>
    public static event Action? Changed;

    public static void Initialize()
    {
        _settings = Json.LoadOrDefault<GlobalSettings>(Paths.SettingsFile);
        _presets = new PresetStore(Paths.Presets, _settings.SharedLibraryFolder);
        _history = new HistoryLog(Paths.HistoryFile);
        Log.Verbose = _settings.VerboseLog;
        Log.Cleanup();
    }

    public static void SaveSettings()
    {
        try
        {
            Json.Save(Paths.SettingsFile, Settings);
            _presets = new PresetStore(Paths.Presets, Settings.SharedLibraryFolder);
            Log.Verbose = Settings.VerboseLog;
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao salvar configurações", ex);
        }
        NotifyChanged();
    }

    public static void NotifyChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao notificar mudanças", ex);
        }
    }

    /// <summary>Resolve o idioma: configuração do usuário ou idioma do Revit.</summary>
    public static Language ResolveLanguage(Autodesk.Revit.ApplicationServices.LanguageType revitLanguage)
    {
        return Settings.Language switch
        {
            "pt" => Language.Portuguese,
            "en" => Language.English,
            _ => revitLanguage == Autodesk.Revit.ApplicationServices.LanguageType.Brazilian_Portuguese
                ? Language.Portuguese
                // Português também para idiomas não suportados quando o Windows está em pt.
                : System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? Language.Portuguese
                    : Language.English,
        };
    }
}
