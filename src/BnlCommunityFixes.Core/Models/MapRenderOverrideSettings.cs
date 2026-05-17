using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class MapRenderOverrideSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "Default";
}
