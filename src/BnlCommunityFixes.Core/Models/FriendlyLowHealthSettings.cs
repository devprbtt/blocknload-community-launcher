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

    [JsonPropertyName("show_direction_indicator")]
    public bool ShowDirectionIndicator { get; set; } = true;

    [JsonPropertyName("indicator_size")]
    public double IndicatorSize { get; set; } = 1.0;

    [JsonPropertyName("indicator_alpha")]
    public double IndicatorAlpha { get; set; } = 1.0;
}
