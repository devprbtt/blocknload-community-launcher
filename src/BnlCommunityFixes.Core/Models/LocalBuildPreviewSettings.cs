using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class LocalBuildPreviewSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("prediction_timeout_seconds")]
    public double PredictionTimeoutSeconds { get; set; } = 2.0;
}
