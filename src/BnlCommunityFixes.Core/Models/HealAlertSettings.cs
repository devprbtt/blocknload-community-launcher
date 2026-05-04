using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class HealAlertSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("damage_indicator_color")]
    public string DamageIndicatorColor { get; set; } = "__DEFAULT__";

    [JsonPropertyName("heal_indicator_color")]
    public string HealIndicatorColor { get; set; } = "#00FF88";

    [JsonPropertyName("damage_indicator_size_multiplier")]
    public double DamageIndicatorSizeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("heal_indicator_size_multiplier")]
    public double HealIndicatorSizeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("alpha")]
    public double Alpha { get; set; } = 1.0;

    [JsonPropertyName("minimum_heal")]
    public double MinimumHeal { get; set; } = 0.5;

    [JsonPropertyName("show_direction_on_heal")]
    public bool ShowDirectionOnHeal { get; set; }
}
