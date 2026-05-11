using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class DamageHealingSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("damage_number_color")]
    public string DamageNumberColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("crit_damage_number_color")]
    public string CritDamageNumberColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("heal_number_color")]
    public string HealNumberColor { get; set; } = "#91ED78";

    [JsonPropertyName("damage_number_size_multiplier")]
    public double DamageNumberSizeMultiplier { get; set; } = 2.0;

    [JsonPropertyName("heal_number_size_multiplier")]
    public double HealNumberSizeMultiplier { get; set; } = 2.0;

    [JsonPropertyName("alpha")]
    public double Alpha { get; set; } = 1.0;

    [JsonPropertyName("show_friendly_healing")]
    public bool ShowFriendlyHealing { get; set; }

    [JsonPropertyName("show_self_healing")]
    public bool ShowSelfHealing { get; set; }

    [JsonPropertyName("combine_damage_until_hidden")]
    public bool CombineDamageUntilHidden { get; set; }

    [JsonPropertyName("combine_healing_until_hidden")]
    public bool CombineHealingUntilHidden { get; set; }

    [JsonPropertyName("minimum_heal")]
    public double MinimumHeal { get; set; } = 0.5;

    [JsonPropertyName("self_heal_number_size_multiplier")]
    public double SelfHealNumberSizeMultiplier { get; set; } = 0.7;

    [JsonPropertyName("self_heal_x")]
    public double SelfHealX { get; set; }

    [JsonPropertyName("self_heal_y")]
    public double SelfHealY { get; set; }
}
