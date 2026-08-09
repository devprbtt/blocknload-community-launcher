using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class FpsCounterSettings
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("refresh_hz")] public int RefreshHz { get; set; } = 4;
}
