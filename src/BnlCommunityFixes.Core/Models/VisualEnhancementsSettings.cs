using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class VisualEnhancementsSettings
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("sharpening")] public double Sharpening { get; set; } = 0.3;
    [JsonPropertyName("saturation")] public double Saturation { get; set; } = 1.0;
    [JsonPropertyName("contrast")] public double Contrast { get; set; } = 1.0;
    [JsonPropertyName("brightness")] public double Brightness { get; set; } = 1.0;
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
}
