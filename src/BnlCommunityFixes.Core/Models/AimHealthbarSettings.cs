using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class AimHealthbarSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
