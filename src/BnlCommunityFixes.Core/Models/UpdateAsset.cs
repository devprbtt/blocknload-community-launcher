using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class UpdateAsset
{
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}
