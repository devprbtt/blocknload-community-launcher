using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class VanderBlueSkinSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
