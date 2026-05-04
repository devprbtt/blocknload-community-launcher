using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class BinaryPatchDefinition
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("offset")]
    public string Offset { get; set; } = "";

    [JsonPropertyName("before")]
    public string Before { get; set; } = "";

    [JsonPropertyName("after")]
    public string After { get; set; } = "";
}
