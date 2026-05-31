namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetReplacementFeatures() =>
    [
        new("audio-replacer", "Audio Replacer", "experimental-audio-replacer-config.json", IsManagedProfile: false),
        new("mesh-replacer", "Mesh Replacer", "experimental-mesh-replacer-config.json", IsManagedProfile: false),
        new("texture-replacer", "Texture Replacer", "experimental-texture-replacer-config.json", TriggersExperimentalBuild: false, IsManagedProfile: false)
    ];
}
