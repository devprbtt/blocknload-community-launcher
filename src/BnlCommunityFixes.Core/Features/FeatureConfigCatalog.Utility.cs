namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetUtilityFeatures() =>
    [
        new("match-replay-recorder", "Replay Recorder", "experimental-match-replay-recorder-config.json"),
        new("debug-menu", "Debug Menu", "experimental-debug-menu-config.json", IncludeInConfigTransfer: true)
    ];
}
