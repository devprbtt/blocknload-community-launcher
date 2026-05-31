using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed class MeshReplacerViewModel : ReplacerViewModel
{
    public MeshReplacerViewModel(AppPaths paths, GameInstallInfo? installInfo = null)
        : base(
            Path.Combine(paths.PatchingDir, "experimental-mesh-replacer-config.json"),
            installInfo?.IsDetected == true ? installInfo.CustomMeshesDirectoryPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
    { }

    public override string Title => "Mesh Replacer";
    public override string InfoText => "Map in-game mesh names to custom .obj files placed in the CustomMeshes folder. Changes apply on next launch.";
    public override string SourceColumnHeader => "Mesh Name";
    public override string TargetColumnHeader => "Replacement .obj File";
    public override string FileFilter => "OBJ files (*.obj)|*.obj";

    protected override void Load()
    {
        Rows.Clear();
        if (!File.Exists(ConfigPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
            var root = doc.RootElement;

            // Format: "meshes": { "MeshName": "file.obj" }
            if (root.TryGetProperty("meshes", out var meshes) &&
                meshes.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in meshes.EnumerateObject())
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
        var dict = rows
            .Where(static r => !string.IsNullOrWhiteSpace(r.SourceKey))
            .ToDictionary(static r => r.SourceKey, static r => r.TargetFile);

        var config = new { enabled = dict.Count > 0, meshes = dict };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
}
