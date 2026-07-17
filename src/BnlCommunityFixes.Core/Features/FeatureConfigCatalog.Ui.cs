namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetUiFeatures() =>
    [
        new("crosshair", "Crosshair", "crosshair-config.json", IncludeInConfigTransfer: true),
        new("fov", "FOV / ADS", "fov-config.json", IncludeInConfigTransfer: true),
        new("team-colors", "Team Colors", "experimental-team-color-config.json", IncludeInConfigTransfer: true),
        new("damage-healing", "Damage / Healing", "damage-healing-indicator-config.json", IncludeInConfigTransfer: true),
        new("heal-alerts", "Heal Alerts", "heal-alert-indicator-config.json", IncludeInConfigTransfer: true),
        new("aim-healthbar", "Aim Healthbar", "aim-healthbar-config.json", IncludeInConfigTransfer: true),
        new("deathcam-healthbar", "Death Cam HP", "deathcam-healthbar-config.json", IncludeInConfigTransfer: true),
        new("friendly-low-health", "Low HP Alert", "friendly-low-health-config.json", IncludeInConfigTransfer: true),
        new("teammate-hp", "Teammate HP", "teammate-hp-config.json", IncludeInConfigTransfer: true),
        new("unit-gui-scale", "Unit GUI Scale", "unit-gui-scale-config.json", IncludeInConfigTransfer: true),
        new("wsi-scale", "WSI Scale", "wsi-config.json", EnabledPropertyName: "scale_enabled", IncludeInConfigTransfer: true),
        new("segmented-healthbar", "Segmented Healthbar", "experimental-segmented-healthbar-config.json", IncludeInConfigTransfer: true),
        new("font-override", "Font Override", "experimental-font-override-config.json", IncludeInConfigTransfer: true),
        new("classic-scoreboard", "Classic Scoreboard", "experimental-classic-scoreboard-config.json", IncludeInConfigTransfer: true)
    ];
}
