namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetGameplayFeatures() =>
    [
        new("base-objective-beam", "Objective Beam", "experimental-base-objective-beam-config.json", IncludeInConfigTransfer: true),
        new("shield-timer", "Shield Timer", "experimental-enemy-shield-buffbar-config.json", IncludeInConfigTransfer: true),
        new("build-preview", "Build Preview", "experimental-local-build-preview-config.json", IncludeInConfigTransfer: true),
        new("auto-queue", "Auto Queue", "experimental-auto-casual-queue-config.json", IncludeInConfigTransfer: true),
        new("auto-crouch", "Auto Crouch", "experimental-auto-crouch-config.json"),
        new("hide-impact-vfx", "Hide Impact VFX", "experimental-hide-impact-vfx-config.json"),
        new("map-render", "Map Render", "experimental-map-render-config.json", IncludeInConfigTransfer: true),
        new("performance-opt", "Device Healthbar Opt", "experimental-performance-opt-config.json"),
        new("ability-cast", "Ability Cast", "experimental-ability-cast-config.json"),
    ];
}
