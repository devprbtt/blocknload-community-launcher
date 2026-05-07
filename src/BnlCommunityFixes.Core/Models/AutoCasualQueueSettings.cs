using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class AutoCasualQueueSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
