using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class HideImpactVfxSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("hide_impact_vfx")]
    public bool HideImpactVfx { get; set; }

    [JsonPropertyName("hide_lava_water_plane")]
    public bool HideLavaWaterPlane { get; set; }
}
