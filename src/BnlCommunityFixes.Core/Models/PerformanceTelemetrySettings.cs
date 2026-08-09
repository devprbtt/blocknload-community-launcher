using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class PerformanceTelemetrySettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "baseline";

    [JsonPropertyName("warmup_seconds")]
    public double WarmupSeconds { get; set; } = 5.0;

    [JsonPropertyName("flush_interval_seconds")]
    public double FlushIntervalSeconds { get; set; } = 5.0;
}
