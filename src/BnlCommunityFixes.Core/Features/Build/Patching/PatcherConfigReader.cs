using System.Text.Json;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

internal static class PatcherConfigReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static JsonElement Read(string patchingDir, string fileName)
    {
        var path = Path.Combine(patchingDir, fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JsonElement>(json, Options);
    }

    public static bool GetBool(JsonElement config, string key, bool defaultValue = false)
    {
        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty(key, out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
        {
            return prop.GetBoolean();
        }
        return defaultValue;
    }

    public static string GetString(JsonElement config, string key, string defaultValue = "")
    {
        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty(key, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? defaultValue;
        }
        return defaultValue;
    }

    public static int GetInt(JsonElement config, string key, int defaultValue = 0)
    {
        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty(key, out var prop) &&
            prop.ValueKind == JsonValueKind.Number &&
            prop.TryGetInt32(out var value))
        {
            return value;
        }
        return defaultValue;
    }

    public static float GetFloat(JsonElement config, string key, float defaultValue = 0f)
    {
        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty(key, out var prop) &&
            prop.ValueKind == JsonValueKind.Number &&
            prop.TryGetSingle(out var value))
        {
            return value;
        }
        return defaultValue;
    }

    public static IReadOnlyDictionary<string, string> GetStringDict(JsonElement config, string key)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(key, out var prop) ||
            prop.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in prop.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String)
            {
                result[entry.Name] = entry.Value.GetString() ?? string.Empty;
            }
        }
        return result;
    }

    public static IReadOnlyDictionary<string, float> GetFloatDict(JsonElement config, string key)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(key, out var prop) ||
            prop.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in prop.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetSingle(out var f))
            {
                result[entry.Name] = f;
            }
        }
        return result;
    }

    public static IReadOnlyList<JsonElement> GetObjectArray(JsonElement config, string key)
    {
        var result = new List<JsonElement>();
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(key, out var prop))
        {
            return result;
        }

        if (prop.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(prop.EnumerateArray());
        }
        else if (prop.ValueKind == JsonValueKind.Object)
        {
            result.Add(prop);
        }

        return result;
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> GetNestedObjectDict(JsonElement config, string key)
    {
        var result = new Dictionary<string, IReadOnlyList<JsonElement>>(StringComparer.Ordinal);
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(key, out var prop) ||
            prop.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in prop.EnumerateObject())
        {
            var items = new List<JsonElement>();
            if (entry.Value.ValueKind == JsonValueKind.Array)
            {
                items.AddRange(entry.Value.EnumerateArray());
            }
            else if (entry.Value.ValueKind == JsonValueKind.Object)
            {
                items.Add(entry.Value);
            }
            result[entry.Name] = items;
        }

        return result;
    }

    public static float[] GetFloatArray(JsonElement obj, string key, int expectedLength)
    {
        var result = new float[expectedLength];
        for (var i = 0; i < expectedLength; i++) result[i] = float.NaN;

        if (obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(key, out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var idx = 0;
        foreach (var item in prop.EnumerateArray())
        {
            if (idx >= expectedLength) break;
            if (item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var f))
            {
                result[idx] = f;
            }
            idx++;
        }
        return result;
    }

    public static IReadOnlyList<string> GetStringArray(JsonElement config, string key)
    {
        var result = new List<string>();
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty(key, out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(item.GetString() ?? string.Empty);
            }
        }
        return result;
    }
}
