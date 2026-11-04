using System.Reflection;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Catálogo de todas as ferramentas, descoberto por reflexão.</summary>
public static class ToolCatalog
{
    private static readonly Dictionary<string, ToolInfo> ById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, ToolInfo> ByType = new();
    private static List<ToolInfo> _all = new();

    public static IReadOnlyList<ToolInfo> All => _all;

    public static void Build(Assembly assembly)
    {
        ById.Clear();
        ByType.Clear();
        var list = new List<ToolInfo>();
        foreach (var t in SafeTypes(assembly))
        {
            if (t.IsAbstract || !typeof(ToolCommand).IsAssignableFrom(t)) continue;
            var a = t.GetCustomAttribute<ToolAttribute>();
            if (a == null) continue;
            var groupParts = (a.Group ?? "").Split('|');
            var info = new ToolInfo
            {
                Id = a.Id,
                Module = a.Module,
                Name = new Txt(a.NamePt, a.NameEn),
                Description = new Txt(a.DescPt, string.IsNullOrEmpty(a.DescEn) ? a.DescPt : a.DescEn),
                Help = new Txt(a.HelpPt, string.IsNullOrEmpty(a.HelpEn) ? a.HelpPt : a.HelpEn),
                Icon = string.IsNullOrEmpty(a.Icon) ? a.Id : a.Icon,
                Group = new Txt(groupParts[0], groupParts.Length > 1 ? groupParts[1] : groupParts[0]),
                Order = a.Order,
                Large = a.Large,
                Ribbon = a.Ribbon,
                RequiresDocument = a.RequiresDocument,
                Keywords = a.Keywords,
                Type = t,
            };
            if (ById.ContainsKey(info.Id))
            {
                Log.Warn($"Ferramenta duplicada ignorada: {info.Id} ({t.FullName})");
                continue;
            }
            ById[info.Id] = info;
            ByType[t] = info;
            list.Add(info);
        }
        _all = list
            .OrderBy(i => Modules.Get(i.Module).Order)
            .ThenBy(i => i.Order)
            .ThenBy(i => i.Name.Pt, StringComparer.CurrentCulture)
            .ToList();
        Log.Info($"Catálogo: {_all.Count} ferramentas.");
    }

    private static IEnumerable<Type> SafeTypes(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    public static ToolInfo? Get(string id) => ById.TryGetValue(id, out var t) ? t : null;

    public static ToolInfo Get(Type type) =>
        ByType.TryGetValue(type, out var t)
            ? t
            : new ToolInfo { Id = type.Name, Module = Modules.General, Name = new Txt(type.Name, type.Name), Type = type, Icon = "mod.general", RequiresDocument = true };

    public static IEnumerable<ToolInfo> InModule(string module) => _all.Where(t => t.Module == module);

    public static IEnumerable<ToolInfo> Search(string query) => _all.Where(t => t.Matches(query));
}
