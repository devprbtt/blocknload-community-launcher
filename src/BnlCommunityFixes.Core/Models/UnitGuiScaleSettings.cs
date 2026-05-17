using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class UnitGuiScaleSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("scale_multiplier")]
    public double ScaleMultiplier { get; set; } = 1.0;
}
