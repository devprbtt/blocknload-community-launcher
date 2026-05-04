using System.Text;
using System.Text.Json;

namespace BnlCommunityFixes.Core.Services;

public sealed class FeaturePresetsService
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string presetsPath;

    public FeaturePresetsService(AppPaths paths)
    {
        presetsPath = paths.PresetsFilePath;
    }

    public SortedDictionary<string, T> Load<T>(string featureKey) where T : class, new()
    {
        if (!File.Exists(presetsPath)) return [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(presetsPath));
            if (!doc.RootElement.TryGetProperty(featureKey, out var section)) return [];
            return JsonSerializer.Deserialize<SortedDictionary<string, T>>(section.GetRawText(), ReadOptions) ?? [];
        }
        catch { return []; }
    }

    public void Save<T>(string featureKey, string name, T value) where T : class
    {
        var root = ReadRaw();
        var section = root.TryGetValue(featureKey, out var raw)
            ? (JsonSerializer.Deserialize<Dictionary<string, T>>(raw, ReadOptions) ?? [])
            : [];
        section[name] = value;
        root[featureKey] = JsonSerializer.Serialize(section, WriteOptions);
        WriteRaw(root);
    }

    public void Delete(string featureKey, string name)
    {
        var root = ReadRaw();
        if (!root.TryGetValue(featureKey, out var raw)) return;
        using var doc = JsonDocument.Parse(raw);
        var all = doc.RootElement.EnumerateObject().ToList();
        var filtered = all.Where(p => p.Name != name)
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        if (filtered.Count == all.Count) return;
        root[featureKey] = SerializeElementDict(filtered);
        WriteRaw(root);
    }

    private Dictionary<string, string> ReadRaw()
    {
        if (!File.Exists(presetsPath)) return [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(presetsPath));
            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetRawText());
        }
        catch { return []; }
    }

    private void WriteRaw(Dictionary<string, string> root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(presetsPath)!);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var (key, rawJson) in root)
            {
                writer.WritePropertyName(key);
                using var inner = JsonDocument.Parse(rawJson);
                inner.RootElement.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        File.WriteAllBytes(presetsPath, ms.ToArray());
    }

    private static string SerializeElementDict(Dictionary<string, JsonElement> dict)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var (key, el) in dict)
            {
                writer.WritePropertyName(key);
                el.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
