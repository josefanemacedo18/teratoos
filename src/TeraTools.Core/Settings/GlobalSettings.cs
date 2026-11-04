namespace TeraTools.Core.Settings;

/// <summary>
/// Configurações globais do usuário (valem para todos os projetos).
/// Gravadas em %AppData%\TeraTools\2027\settings.json.
/// </summary>
public sealed class GlobalSettings
{
    public int Version { get; set; } = 1;

    /// <summary>"auto" (segue o Revit), "pt" ou "en". Mudança vale após reiniciar o Revit.</summary>
    public string Language { get; set; } = "auto";

    public List<string> Favorites { get; set; } = new();

    public List<string> Recent { get; set; } = new();

    public int RecentMax { get; set; } = 12;

    /// <summary>Acima desta quantidade de elementos o plugin pede confirmação.</summary>
    public int ElementWarningThreshold { get; set; } = 2000;

    /// <summary>Limite rígido de elementos por operação (proteção de desempenho).</summary>
    public int ElementHardLimit { get; set; } = 30000;

    /// <summary>Exibe o relatório ao final quando houver avisos, erros ou tabelas.</summary>
    public bool ShowReportAfterRun { get; set; } = true;

    /// <summary>Seleciona no modelo os elementos criados ao final da operação.</summary>
    public bool SelectResults { get; set; } = true;

    /// <summary>Lembrar as últimas opções usadas em cada ferramenta.</summary>
    public bool RememberLastOptions { get; set; } = true;

    /// <summary>Pasta alternativa de templates de família (.rft). Vazio = padrão do Revit.</summary>
    public string? FamilyTemplateFolder { get; set; }

    /// <summary>Pasta compartilhada de bibliotecas (equipe). Vazio = só bibliotecas pessoais.</summary>
    public string? SharedLibraryFolder { get; set; }

    public string? LastExportFolder { get; set; }

    /// <summary>"auto", ";", "," ou "tab".</summary>
    public string CsvSeparator { get; set; } = "auto";

    /// <summary>Mostra o painel lateral ao abrir o Revit.</summary>
    public bool ShowPaneOnStartup { get; set; } = true;

    /// <summary>Mostra descrições das ferramentas no painel.</summary>
    public bool PaneShowDescriptions { get; set; } = true;

    /// <summary>Registra detalhes técnicos no log (para diagnóstico).</summary>
    public bool VerboseLog { get; set; }

    public void TouchRecent(string toolId)
    {
        Recent.RemoveAll(r => string.Equals(r, toolId, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, toolId);
        if (Recent.Count > Math.Max(1, RecentMax))
            Recent.RemoveRange(RecentMax, Recent.Count - RecentMax);
    }

    public bool IsFavorite(string toolId) => Favorites.Any(f => string.Equals(f, toolId, StringComparison.OrdinalIgnoreCase));

    public bool ToggleFavorite(string toolId)
    {
        if (IsFavorite(toolId))
        {
            Favorites.RemoveAll(f => string.Equals(f, toolId, StringComparison.OrdinalIgnoreCase));
            return false;
        }
        Favorites.Add(toolId);
        return true;
    }
}
