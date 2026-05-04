using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class TeamColorSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("friendly_color")]
    public string FriendlyColor { get; set; } = "#4AA3FF";

    [JsonPropertyName("enemy_color")]
    public string EnemyColor { get; set; } = "#FF5A5A";
}
