using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed class TextureReplacerViewModel : ReplacerViewModel
{
    private readonly string _mappingPath;

    public TextureReplacerViewModel(AppPaths paths, GameInstallInfo? installInfo = null)
        : base(
            Path.Combine(paths.PatchingDir, "experimental-texture-replacer-config.json"),
            installInfo?.IsDetected == true ? installInfo.CustomTexturesDirectoryPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
    {
        _mappingPath = Path.Combine(paths.PatchingDir, "texture-replacements.txt");
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
                if (root.TryGetProperty("textures", out var textures) &&
                    textures.ValueKind == JsonValueKind.Object)
                {
                    foreach (var kvp in textures.EnumerateObject())
                        Rows.Add(new ReplacerRow(kvp.Name, kvp.Value.GetString() ?? string.Empty));
                    return;
                }
            }
            catch { }
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
                        Rows.Add(new ReplacerRow(line[..idx].Trim(), line[(idx + 1)..].Trim()));
                }
            }
            catch { }
        }
    }

    protected override void SaveRows(List<ReplacerRow> rows)
    {
        var dict = rows
            .Where(static r => !string.IsNullOrWhiteSpace(r.SourceKey))
            .ToDictionary(static r => r.SourceKey, static r => r.TargetFile);

        // Save JSON config
        var config = new { enabled = dict.Count > 0, textures = dict };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        // Keep texture-replacements.txt in sync (legacy format)
        var lines = dict.Select(static kvp => $"{kvp.Key}={kvp.Value}");
        File.WriteAllLines(_mappingPath, lines);
    }
}
