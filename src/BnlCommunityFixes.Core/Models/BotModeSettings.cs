using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class BotModeSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("bot_count")]
    public int BotCount { get; set; } = 3;

    /// <summary>"easy", "medium", or "hard"</summary>
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "medium";

    [JsonPropertyName("map")]
    public string Map { get; set; } = "default";
}
