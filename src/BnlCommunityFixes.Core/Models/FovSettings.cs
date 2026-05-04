using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class FovSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("fov")]
    public double Fov { get; set; } = 100.0;

    [JsonPropertyName("ads_sensitivity_multiplier")]
    public double AdsSensitivityMultiplier { get; set; } = 1.0;

    [JsonPropertyName("weapon_model_fov")]
    public double WeaponModelFov { get; set; } = 20.0;
}
