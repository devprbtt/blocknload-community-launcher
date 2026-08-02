using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class MotionBlurSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 1.0;

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "medium";

    [JsonPropertyName("center_focus")]
    public double CenterFocus { get; set; } = 0.35;
}
