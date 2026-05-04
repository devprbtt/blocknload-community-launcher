using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class DeathCamHealthbarSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
