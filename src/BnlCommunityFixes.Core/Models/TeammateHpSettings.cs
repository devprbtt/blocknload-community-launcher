using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class TeammateHpSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;
}
