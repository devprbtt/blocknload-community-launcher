using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class UpdateManifest
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("minimum_supported_version")]
    public string MinimumSupportedVersion { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("assets")]
    public Dictionary<string, UpdateAsset> Assets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
