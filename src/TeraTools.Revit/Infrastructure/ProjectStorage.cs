using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using TeraTools.Core.Settings;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Guarda configurações do Téra Tools DENTRO do arquivo do projeto (Extensible Storage),
/// para que presets, regras e padrões acompanhem o .rvt.
/// </summary>
public static class ProjectStorage
{
    private static readonly Guid SettingsSchemaGuid = new("DD30229E-5706-4BD4-A001-981A87AB98ED");
    private static readonly Guid GeneratedSchemaGuid = new("7A5087DF-E053-416C-A1B7-B920DEC6DDBF");
    private const string Vendor = "TERA";
    private const string DataStorageName = "TeraTools.Configuracoes";

    private static Schema SettingsSchema()
    {
        var s = Schema.Lookup(SettingsSchemaGuid);
        if (s != null) return s;
        var b = new SchemaBuilder(SettingsSchemaGuid);
        b.SetSchemaName("TeraToolsProjectSettings");
        b.SetVendorId(Vendor);
        b.SetReadAccessLevel(AccessLevel.Public);
        b.SetWriteAccessLevel(AccessLevel.Public);
        b.SetDocumentation("Configurações do plugin Téra Tools (JSON).");
        b.AddSimpleField("Json", typeof(string));
        return b.Finish();
    }

    /// <summary>Esquema para marcar elementos criados por geradores (permite reeditar o sistema).</summary>
    public static Schema GeneratedSchema()
    {
        var s = Schema.Lookup(GeneratedSchemaGuid);
        if (s != null) return s;
        var b = new SchemaBuilder(GeneratedSchemaGuid);
        b.SetSchemaName("TeraToolsGenerated");
        b.SetVendorId(Vendor);
        b.SetReadAccessLevel(AccessLevel.Public);
        b.SetWriteAccessLevel(AccessLevel.Public);
        b.SetDocumentation("Elemento criado por um gerador do Téra Tools.");
        b.AddSimpleField("SystemId", typeof(string));
        b.AddSimpleField("ToolId", typeof(string));
        b.AddSimpleField("Index", typeof(int));
        return b.Finish();
    }

    private static DataStorage? FindStorage(Document doc)
    {
        var schema = SettingsSchema();
        return new FilteredElementCollector(doc)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .FirstOrDefault(d => d.GetEntity(schema) is { } e && e.IsValid());
    }

    public static ProjectSettings Load(Document doc)
    {
        try
        {
            var ds = FindStorage(doc);
            if (ds == null) return new ProjectSettings();
            var e = ds.GetEntity(SettingsSchema());
            return ProjectSettings.FromJson(e.Get<string>("Json"));
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao ler configurações do projeto", ex);
            return new ProjectSettings();
        }
    }

    /// <summary>Grava as configurações (abre transação própria se nenhuma estiver ativa).</summary>
    public static void Save(Document doc, ProjectSettings settings)
    {
        void Write()
        {
            var ds = FindStorage(doc);
            if (ds == null)
            {
                ds = DataStorage.Create(doc);
                ds.Name = DataStorageName;
            }
            var e = new Entity(SettingsSchema());
            e.Set("Json", settings.ToJson());
            ds.SetEntity(e);
        }

        if (doc.IsModifiable)
        {
            Write();
            return;
        }
        using var t = new Transaction(doc, "Téra Tools: configurações do projeto");
        t.Start();
        Write();
        t.Commit();
    }

    public static T GetSection<T>(Document doc, string key) where T : new() => Load(doc).GetSection<T>(key);

    public static void SetSection<T>(Document doc, string key, T value)
    {
        var s = Load(doc);
        s.SetSection(key, value);
        Save(doc, s);
    }

    public static ProjectPresets LoadPresets(Document doc) =>
        Load(doc).GetSection<ProjectPresets>(ProjectSettings.SectionPresets);

    public static void SavePresets(Document doc, ProjectPresets presets) =>
        SetSection(doc, ProjectSettings.SectionPresets, presets);

    // ---------- Elementos gerados ----------

    public static void MarkGenerated(Element e, string systemId, string toolId, int index)
    {
        var ent = new Entity(GeneratedSchema());
        ent.Set("SystemId", systemId);
        ent.Set("ToolId", toolId);
        ent.Set("Index", index);
        e.SetEntity(ent);
    }

    public static (string SystemId, string ToolId, int Index)? ReadGenerated(Element e)
    {
        try
        {
            var schema = Schema.Lookup(GeneratedSchemaGuid);
            if (schema == null) return null;
            var ent = e.GetEntity(schema);
            if (ent == null || !ent.IsValid()) return null;
            return (ent.Get<string>("SystemId"), ent.Get<string>("ToolId"), ent.Get<int>("Index"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Todos os elementos de um sistema gerado.</summary>
    public static List<Element> FindSystem(Document doc, string systemId)
    {
        var schema = Schema.Lookup(GeneratedSchemaGuid);
        if (schema == null) return new List<Element>();
        return new FilteredElementCollector(doc)
            .WherePasses(new ExtensibleStorageFilter(GeneratedSchemaGuid))
            .Where(el => el.GetEntity(schema) is { } ent && ent.IsValid() && ent.Get<string>("SystemId") == systemId)
            .ToList();
    }

    /// <summary>Registro (no projeto) das opções usadas para gerar cada sistema.</summary>
    public sealed class GeneratedSystems
    {
        public Dictionary<string, GeneratedSystem> Systems { get; set; } = new();
    }

    public sealed class GeneratedSystem
    {
        public string ToolId { get; set; } = "";
        public string OptionsJson { get; set; } = "{}";
        public string ContextJson { get; set; } = "{}";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;
        public string? Name { get; set; }

        /// <summary>Versão do gerador que produziu o sistema (0 = anterior ao versionamento).</summary>
        public int Version { get; set; }

        /// <summary>Versão do Téra Tools que gravou o registro.</summary>
        public string? PluginVersion { get; set; }

        /// <summary>Versões anteriores das opções (a mais recente primeiro), para voltar atrás.</summary>
        public List<SystemRevision> History { get; set; } = new();
    }

    public sealed class SystemRevision
    {
        public DateTime Date { get; set; }
        public int Version { get; set; }
        public string OptionsJson { get; set; } = "{}";
        public string ContextJson { get; set; } = "{}";
    }

    /// <summary>Quantas versões anteriores cada sistema guarda.</summary>
    public const int MaxHistory = 20;

    public const string SectionSystems = "systems";

    public static void RegisterSystem(Document doc, string systemId, GeneratedSystem sys)
    {
        var s = Load(doc);
        var reg = s.GetSection<GeneratedSystems>(SectionSystems);
        // Regeneração: a versão anterior vai para o histórico (se as opções mudaram).
        if (reg.Systems.TryGetValue(systemId, out var prev))
        {
            sys.Created = prev.Created;
            sys.History = prev.History ?? new List<SystemRevision>();
            if (prev.OptionsJson != sys.OptionsJson || prev.ContextJson != sys.ContextJson)
                sys.History.Insert(0, new SystemRevision { Date = prev.Modified == default ? prev.Created : prev.Modified, Version = prev.Version, OptionsJson = prev.OptionsJson, ContextJson = prev.ContextJson });
            if (sys.History.Count > MaxHistory) sys.History.RemoveRange(MaxHistory, sys.History.Count - MaxHistory);
        }
        sys.Modified = DateTime.Now;
        reg.Systems[systemId] = sys;
        s.SetSection(SectionSystems, reg);
        Save(doc, s);
    }

    public static GeneratedSystem? GetSystem(Document doc, string systemId)
    {
        var reg = Load(doc).GetSection<GeneratedSystems>(SectionSystems);
        return reg.Systems.TryGetValue(systemId, out var sys) ? sys : null;
    }
}
