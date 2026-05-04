using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class CrosshairSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("idle_color")]
    public string IdleColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("full_damage_color")]
    public string FullDamageColor { get; set; } = "#FF0000";

    [JsonPropertyName("below_max_color")]
    public string BelowMaxColor { get; set; } = "#FF0000";

    [JsonPropertyName("brightness_multiplier")]
    public double BrightnessMultiplier { get; set; } = 1.0;

    [JsonPropertyName("size_multiplier")]
    public double SizeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("spread_multiplier")]
    public double SpreadMultiplier { get; set; } = 1.0;

    [JsonPropertyName("alpha")]
    public double Alpha { get; set; } = 1.0;

    [JsonPropertyName("force_shape")]
    public string ForceShape { get; set; } = "__DEFAULT__";

    [JsonPropertyName("force_show_in_ads")]
    public bool ForceShowInAds { get; set; }

    [JsonPropertyName("hide_crosshair")]
    public bool HideCrosshair { get; set; }
}
