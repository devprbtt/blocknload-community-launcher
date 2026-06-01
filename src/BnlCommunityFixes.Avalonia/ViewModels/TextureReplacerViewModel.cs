using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed class TextureReplacerViewModel : ReplacerViewModel
{
    private readonly string _mappingPath;
    private readonly string _segmentedHealthbarConfigPath;
    private const string SegmentedFillEntry = "white_rect=white_rect.png";
    private const string SegmentedBgEntry = "white_rect_bg=white_rect_bg.png";

    public TextureReplacerViewModel(AppPaths paths, GameInstallInfo? installInfo = null)
        : base(
            Path.Combine(paths.PatchingDir, "experimental-texture-replacer-config.json"),
            installInfo?.IsDetected == true ? installInfo.CustomTexturesDirectoryPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
    {
        _mappingPath = Path.Combine(paths.PatchingDir, "texture-replacements.txt");
        _segmentedHealthbarConfigPath = Path.Combine(paths.PatchingDir, "experimental-segmented-healthbar-config.json");
    }

    public override string Title => "Texture Replacer";
    public override string InfoText => "Map in-game texture or sprite names to custom image files placed in the CustomTextures folder. Changes apply on next launch.";
    public override string SourceColumnHeader => "Texture / Sprite Name";
    public override string TargetColumnHeader => "Replacement Image File";
    public override string FileFilter => "Image files (*.png;*.jpg)|*.png;*.jpg";

    protected override void Load()
    {
        Rows.Clear();

        // Primary source: "textures" dict in the JSON config
        if (File.Exists(ConfigPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                var root = doc.RootElement;
                IsFeatureEnabled = ReadEnabledFlag(root, false);
                if (root.TryGetProperty("textures", out var textures) &&
                    textures.ValueKind == JsonValueKind.Object)
                {
                    foreach (var kvp in textures.EnumerateObject())
                    {
                        if (IsSegmentedHealthbarEntry(kvp.Name))
                            continue;
                        Rows.Add(new ReplacerRow(kvp.Name, kvp.Value.GetString() ?? string.Empty));
                    }
                    return;
                }
            }
            catch
            {
                IsFeatureEnabled = false;
            }
        }

        // Fallback: legacy texture-replacements.txt (tab-separated "name=file" lines)
        if (File.Exists(_mappingPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(_mappingPath))
                {
                    // Support both "name=file" and "name\tfile" formats
                    var sep = line.Contains('\t') ? '\t' : '=';
                    var idx = line.IndexOf(sep);
                    if (idx > 0)
                    {
                        var key = line[..idx].Trim();
                        if (IsSegmentedHealthbarEntry(key))
                            continue;
                        Rows.Add(new ReplacerRow(key, line[(idx + 1)..].Trim()));
                    }
                }
            }
            catch { }
        }
    }

    protected override void SaveRows(List<ReplacerRow> rows)
    {
        var existing = new Dictionary<string, JsonElement>();
        if (File.Exists(ConfigPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    if (prop.Name != "textures" && prop.Name != "enabled")
                        existing[prop.Name] = prop.Value.Clone();
            }
            catch { }
        }

        var dict = rows
            .Where(static r => !string.IsNullOrWhiteSpace(r.SourceKey))
            .ToDictionary(static r => r.SourceKey, static r => r.TargetFile);

        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", IsFeatureEnabled);
        foreach (var kvp in existing)
        {
            writer.WritePropertyName(kvp.Key);
            kvp.Value.WriteTo(writer);
        }

        writer.WritePropertyName("textures");
        writer.WriteStartObject();
        foreach (var kvp in dict)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }
        if (IsSegmentedHealthbarEnabled())
        {
            writer.WriteString("white_rect", "white_rect.png");
            writer.WriteString("white_rect_bg", "white_rect_bg.png");
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        File.WriteAllBytes(ConfigPath, ms.ToArray());

        // Keep texture-replacements.txt in sync (legacy format)
        var lines = dict.Select(static kvp => $"{kvp.Key}={kvp.Value}").ToList();
        if (IsSegmentedHealthbarEnabled())
        {
            if (!lines.Any(static line => line.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase)))
                lines.Add(SegmentedFillEntry);
            if (!lines.Any(static line => line.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase)))
                lines.Add(SegmentedBgEntry);
        }
        File.WriteAllLines(_mappingPath, lines);
    }

    private bool IsSegmentedHealthbarEnabled()
    {
        try
        {
            if (!File.Exists(_segmentedHealthbarConfigPath))
                return false;

            using var doc = JsonDocument.Parse(File.ReadAllText(_segmentedHealthbarConfigPath));
            return doc.RootElement.TryGetProperty("enabled", out var prop) &&
                   prop.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                   prop.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSegmentedHealthbarEntry(string key) =>
        string.Equals(key, "white_rect", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "white_rect_bg", StringComparison.OrdinalIgnoreCase);
}
