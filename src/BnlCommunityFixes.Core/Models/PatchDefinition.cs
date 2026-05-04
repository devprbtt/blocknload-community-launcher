using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class PatchDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("files")]
    public Dictionary<string, PatchFileDefinition> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
