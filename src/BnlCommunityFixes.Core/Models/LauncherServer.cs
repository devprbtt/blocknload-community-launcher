using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class LauncherServer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("patch")]
    public string Patch { get; set; } = "default";
}
