using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class LauncherSettings
{
    public const string RecommendedSettingsProfile = "recommended";
    public const string PersonalSettingsProfile = "personal";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "BnlCommunityFixes";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; set; } = "";

    [JsonPropertyName("gamePath")]
    public string? GamePath { get; set; }

    [JsonPropertyName("settingsProfile")]
    public string SettingsProfile { get; set; } = RecommendedSettingsProfile;

    [JsonPropertyName("lastLauncherVersion")]
    public string LastLauncherVersion { get; set; } = "";

    [JsonPropertyName("dismissedUpdateNoticeVersion")]
    public string DismissedUpdateNoticeVersion { get; set; } = "";
}
