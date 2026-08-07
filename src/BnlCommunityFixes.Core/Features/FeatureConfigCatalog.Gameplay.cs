namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetGameplayFeatures() =>
    [
        new("base-objective-beam", "Objective Beam", "experimental-base-objective-beam-config.json", IncludeInConfigTransfer: true),
        new("shield-timer", "Shield Timer", "experimental-enemy-shield-buffbar-config.json", IncludeInConfigTransfer: true),
        new("build-preview", "Build Preview", "experimental-local-build-preview-config.json", IncludeInConfigTransfer: true),
        new("auto-queue", "Auto Queue", "experimental-auto-casual-queue-config.json", IncludeInConfigTransfer: true),
        new("auto-crouch", "Auto Crouch", "experimental-auto-crouch-config.json", IncludeInConfigTransfer: true),
        new("motion-blur", "Motion Blur (Prototype)", "experimental-motion-blur-config.json", IncludeInConfigTransfer: true),
        new("visual-enhancements", "Color Grading / Sharpening", "experimental-visual-enhancements-config.json", IncludeInConfigTransfer: true),
        new("nigel-sniper-visual", "Nigel Imported Rifle (Prototype)", "experimental-nigel-sniper-visual-config.json", IncludeInConfigTransfer: true),
        new("ninja-turtle-skin", "Ninja Turtle Skin", "experimental-ninja-turtle-skin-config.json", IncludeInConfigTransfer: true),
        new("vander-blue-skin", "Vander Blue Skin", "experimental-vander-blue-skin-config.json", IncludeInConfigTransfer: true),
        new("hindu-yeti-skin", "Hindu Yeti Skin", "experimental-hindu-yeti-skin-config.json", IncludeInConfigTransfer: true),
        new("darklord-sweet-science-skin", "Darklord SS Skin", "experimental-darklord-sweet-science-skin-config.json", IncludeInConfigTransfer: true),
        new("hide-impact-vfx", "Hide Impact VFX", "experimental-hide-impact-vfx-config.json", IncludeInConfigTransfer: true),
        new("map-render", "Map Render", "experimental-map-render-config.json", IncludeInConfigTransfer: true),
        new("performance-opt", "Device Healthbar Opt", "experimental-performance-opt-config.json", IncludeInConfigTransfer: true),
        new("time-assault", "Enable Time Assault", "experimental-time-assault-config.json", IncludeInConfigTransfer: true),
        new("ability-cast", "Ability Cast", "experimental-ability-cast-config.json"),
        // Bot mode launches the embedded bnlReloaded server; no client bot patch is involved.
        new("bot-mode", "Bot / Offline Practice Mode", "experimental-bot-mode-config.json", TriggersExperimentalBuild: false, IncludeInConfigTransfer: true),
    ];
}
