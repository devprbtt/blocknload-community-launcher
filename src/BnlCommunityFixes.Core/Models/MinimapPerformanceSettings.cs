using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class MinimapPerformanceSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("update_hz")]
    public int UpdateHz { get; set; } = 30;
}
