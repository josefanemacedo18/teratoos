using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TeraTools.Core.Serialization;

/// <summary>Serialização JSON padronizada do plugin (configurações, presets, dados de geradores).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = CreateOptions(indented: true);
    public static readonly JsonSerializerOptions Compact = CreateOptions(indented: false);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public static string Serialize<T>(T value, bool indented = true) =>
        JsonSerializer.Serialize(value, indented ? Options : Compact);

    public static string Serialize(object value, Type type, bool indented = true) =>
        JsonSerializer.Serialize(value, type, indented ? Options : Compact);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static object? Deserialize(string? json, Type type)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize(json, type, Options);
    }

    public static bool TryDeserialize<T>(string? json, out T? value, out string? error)
    {
        try
        {
            value = Deserialize<T>(json);
            error = null;
            return value != null;
        }
        catch (Exception ex)
        {
            value = default;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Cópia profunda via JSON.</summary>
    public static T Clone<T>(T value) where T : class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Compact), Compact)!;

    /// <summary>Copia os valores de <paramref name="json"/> sobre um objeto existente (preserva padrões não informados).</summary>
    public static void PopulateObject(object target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var type = target.GetType();
        var fresh = JsonSerializer.Deserialize(json, type, Options);
        if (fresh == null) return;
        var node = JsonNode.Parse(json) as JsonObject;
        foreach (var prop in type.GetProperties().Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            if (node != null && !node.Any(kv => string.Equals(kv.Key, prop.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            prop.SetValue(target, prop.GetValue(fresh));
        }
    }

    public static string ReadFile(string path) => File.ReadAllText(path, Encoding.UTF8);

    /// <summary>Gravação atômica: escreve em arquivo temporário e substitui (evita arquivos corrompidos).</summary>
    public static void WriteFileAtomic(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }

    public static T LoadOrDefault<T>(string path) where T : new()
    {
        try
        {
            if (File.Exists(path))
                return Deserialize<T>(ReadFile(path)) ?? new T();
        }
        catch
        {
            // Arquivo inválido: guarda uma cópia e recomeça com padrão.
            try { File.Copy(path, path + ".corrompido", true); } catch { /* ignora */ }
        }
        return new T();
    }

    public static void Save<T>(string path, T value) => WriteFileAtomic(path, Serialize(value));
}
