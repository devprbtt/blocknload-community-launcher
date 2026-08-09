using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class WsiPerformanceSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("update_hz")]
    public int UpdateHz { get; set; } = 15;
}
