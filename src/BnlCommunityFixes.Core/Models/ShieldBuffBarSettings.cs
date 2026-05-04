using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class ShieldBuffBarSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("shield_buff_bar_color")]
    public string ShieldBuffBarColor { get; set; } = "#FFF04A";

    [JsonPropertyName("shield_clock_size_multiplier")]
    public double ShieldClockSizeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("shield_clock_offset_x")]
    public double ShieldClockOffsetX { get; set; }

    [JsonPropertyName("shield_clock_offset_y")]
    public double ShieldClockOffsetY { get; set; }

    [JsonPropertyName("shield_timer_display_mode")]
    public string ShieldTimerDisplayMode { get; set; } = "circle";
}
