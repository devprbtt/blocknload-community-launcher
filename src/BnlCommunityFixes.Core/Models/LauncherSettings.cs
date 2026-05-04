using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class LauncherSettings
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = "BnlCommunityFixes";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; set; } = "";

    [JsonPropertyName("gamePath")]
    public string? GamePath { get; set; }
}
