using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class BaseObjectiveBeamSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hide_beam")]
    public bool HideBeam { get; set; } = false;
}
