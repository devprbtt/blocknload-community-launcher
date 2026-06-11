using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class TimeAssaultSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
