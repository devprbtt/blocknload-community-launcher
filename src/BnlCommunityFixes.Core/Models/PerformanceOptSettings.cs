using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class PerformanceOptSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("device_healthbar_cull_distance")]
    public double DeviceHealthbarCullDistance { get; set; } = 35.0;

    [JsonPropertyName("diagnostic_mode")]
    public string DiagnosticMode { get; set; } = "None";
}
