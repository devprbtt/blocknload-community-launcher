using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class FriendlyLowHealthSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.3;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FF4444";
}
