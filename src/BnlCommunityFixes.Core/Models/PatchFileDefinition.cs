using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class PatchFileDefinition
{
    [JsonPropertyName("hash_before")]
    public string HashBefore { get; set; } = "";

    [JsonPropertyName("hash_after")]
    public string HashAfter { get; set; } = "";

    [JsonPropertyName("patches")]
    public List<BinaryPatchDefinition> Patches { get; set; } = [];
}
