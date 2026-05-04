using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class LauncherConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("update_sources")]
    public Dictionary<string, string> UpdateSources { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("auto_update_server_list")]
    public bool AutoUpdateServerList { get; set; }

    [JsonPropertyName("selected_server")]
    public string SelectedServer { get; set; } = "";

    [JsonPropertyName("servers")]
    public Dictionary<string, LauncherServer> Servers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("patch_configurations")]
    public Dictionary<string, PatchDefinition> PatchConfigurations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
