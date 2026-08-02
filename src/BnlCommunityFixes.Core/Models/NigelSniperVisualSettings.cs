using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class NigelSniperVisualSettings
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}
