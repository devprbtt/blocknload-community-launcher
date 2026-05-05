using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class WsiSettings
{
    [JsonPropertyName("show_cubes")]
    public bool ShowCubes { get; set; } = true;

    [JsonPropertyName("show_devices")]
    public bool ShowDevices { get; set; } = true;

    [JsonPropertyName("show_objectives")]
    public bool ShowObjectives { get; set; } = true;
}
