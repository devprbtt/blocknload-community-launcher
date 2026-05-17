using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class WsiSettings
{
    [JsonPropertyName("show_cubes")]
    public bool ShowCubes { get; set; } = true;

    [JsonPropertyName("show_devices")]
    public bool ShowDevices { get; set; } = true;

    [JsonPropertyName("show_objectives")]
    public bool ShowObjectives { get; set; } = true;

    [JsonPropertyName("scale_enabled")]
    public bool ScaleEnabled { get; set; }

    [JsonPropertyName("scale_multiplier")]
    public double ScaleMultiplier { get; set; } = 1.0;
}
