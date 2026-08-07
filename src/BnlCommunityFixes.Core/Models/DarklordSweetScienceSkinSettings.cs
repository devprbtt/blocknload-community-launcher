using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class DarklordSweetScienceSkinSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
