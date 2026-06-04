using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class TeammateHpSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("show_hp_text")]
    public bool ShowHpText { get; set; } = false;

    [JsonPropertyName("hide_name_background")]
    public bool HideNameBackground { get; set; } = false;
}
