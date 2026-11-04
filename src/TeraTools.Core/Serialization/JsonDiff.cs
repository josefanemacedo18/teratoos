using System.Text.Json;
using System.Text.Json.Nodes;

namespace TeraTools.Core.Serialization;

public enum DiffKind
{
    Added,
    Removed,
    Changed,
}

public sealed record DiffEntry(string Path, DiffKind Kind, string? OldValue, string? NewValue);

/// <summary>Compara duas configurações (JSON) e lista as diferenças — "comparar versões de configurações".</summary>
public static class JsonDiff
{
    public static List<DiffEntry> Compare(string? oldJson, string? newJson)
    {
        var a = string.IsNullOrWhiteSpace(oldJson) ? null : JsonNode.Parse(oldJson);
        var b = string.IsNullOrWhiteSpace(newJson) ? null : JsonNode.Parse(newJson);
        var list = new List<DiffEntry>();
        Walk("$", a, b, list);
        return list;
    }

    private static void Walk(string path, JsonNode? a, JsonNode? b, List<DiffEntry> list)
    {
        if (a == null && b == null) return;
        if (a == null) { list.Add(new DiffEntry(path, DiffKind.Added, null, Str(b))); return; }
        if (b == null) { list.Add(new DiffEntry(path, DiffKind.Removed, Str(a), null)); return; }

        if (a is JsonObject oa && b is JsonObject ob)
        {
            var keys = oa.Select(k => k.Key).Union(ob.Select(k => k.Key), StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
            foreach (var k in keys)
                Walk(path + "." + k, oa[k], ob[k], list);
            return;
        }
        if (a is JsonArray aa && b is JsonArray ab)
        {
            var n = Math.Max(aa.Count, ab.Count);
            for (var i = 0; i < n; i++)
                Walk($"{path}[{i}]", i < aa.Count ? aa[i] : null, i < ab.Count ? ab[i] : null, list);
            return;
        }
        var sa = Str(a);
        var sb = Str(b);
        if (!string.Equals(sa, sb, StringComparison.Ordinal))
            list.Add(new DiffEntry(path, DiffKind.Changed, sa, sb));
    }

    private static string? Str(JsonNode? n) =>
        n?.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
