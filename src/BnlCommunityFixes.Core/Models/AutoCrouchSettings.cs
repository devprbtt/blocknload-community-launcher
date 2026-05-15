using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class AutoCrouchSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;
}
