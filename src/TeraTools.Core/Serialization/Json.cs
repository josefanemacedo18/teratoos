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

    /// <summary>
    /// Copia os valores de <paramref name="json"/> sobre um objeto existente (preserva padrões não informados).
    /// Tolerante: cada propriedade é lida separadamente; valores de tipo diferente (ex.: 3.0 numa propriedade int,
    /// texto numa numérica) são convertidos quando possível e ignorados quando não — nunca derrubam a ferramenta.
    /// </summary>
    public static void PopulateObject(object target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        JsonObject? node;
        try
        {
            node = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }
        if (node == null) return;
        var type = target.GetType();
        foreach (var prop in type.GetProperties().Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            var kv = node.FirstOrDefault(k => string.Equals(k.Key, prop.Name, StringComparison.OrdinalIgnoreCase));
            if (kv.Key == null) continue;
            if (TryReadValue(kv.Value, prop.PropertyType, out var value)) prop.SetValue(target, value);
        }
    }

    /// <summary>Lê um valor JSON para o tipo pedido, com conversões tolerantes.</summary>
    public static bool TryReadValue(JsonNode? node, Type type, out object? value)
    {
        value = null;
        try
        {
            value = node == null ? null : node.Deserialize(type, Options);
            if (value == null && type.IsValueType && Nullable.GetUnderlyingType(type) == null) return false;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or FormatException)
        {
            // segue para a conversão tolerante
        }
        if (node is JsonValue jv)
        {
            object? raw = jv.TryGetValue<double>(out var d) ? d
                : jv.TryGetValue<string>(out var s) ? s
                : jv.TryGetValue<bool>(out var b) ? b
                : null;
            if (raw != null) return ValueConverter.TryConvert(raw, type, out value);
        }
        return false;
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
