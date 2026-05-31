using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed class AudioReplacerViewModel : ReplacerViewModel
{
    public AudioReplacerViewModel(AppPaths paths, GameInstallInfo? installInfo = null)
        : base(
            Path.Combine(paths.PatchingDir, "experimental-audio-replacer-config.json"),
            installInfo?.IsDetected == true ? installInfo.CustomAudioDirectoryPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
    { }

    public override string Title => "Audio Replacer";
    public override string InfoText => "Map Wwise game events to other events or custom .wav/.mp3 files. Changes apply on next launch.";
    public override string SourceColumnHeader => "Event Name";
    public override string TargetColumnHeader => "Replacement File or Event";
    public override string FileFilter => "Audio files (*.wav;*.mp3)|*.wav;*.mp3";

    protected override void Load()
    {
        Rows.Clear();
        if (!File.Exists(ConfigPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
            var root = doc.RootElement;

            // Primary replacements: "custom_audio" dict (event name → file/event name)
            if (root.TryGetProperty("custom_audio", out var customAudio) &&
                customAudio.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in customAudio.EnumerateObject())
                    Rows.Add(new ReplacerRow(kvp.Name, kvp.Value.GetString() ?? string.Empty));
            }
            // Fallback: legacy "replacements" key
            else if (root.TryGetProperty("replacements", out var replacements) &&
                     replacements.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in replacements.EnumerateObject())
                    Rows.Add(new ReplacerRow(kvp.Name, kvp.Value.GetString() ?? string.Empty));
            }
        }
        catch { }
    }

    protected override void SaveRows(List<ReplacerRow> rows)
    {
        // Preserve existing config fields (enabled, log_all_events, volume, volumes, ignored_events)
        var existing = new Dictionary<string, JsonElement>();
        if (File.Exists(ConfigPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    if (prop.Name != "custom_audio" && prop.Name != "replacements")
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

        // Write preserved fields first (existing is Dictionary<string,JsonElement>)
        foreach (var kvp in existing)
        {
            writer.WritePropertyName(kvp.Key);
            kvp.Value.WriteTo(writer);
        }

        // Write custom_audio
        writer.WritePropertyName("custom_audio");
        writer.WriteStartObject();
        foreach (var kvp in dict)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();
        File.WriteAllBytes(ConfigPath, ms.ToArray());
    }
}
