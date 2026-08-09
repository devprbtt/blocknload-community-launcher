namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> GetUtilityFeatures() =>
    [
        new("match-replay-recorder", "Replay Recorder", "experimental-match-replay-recorder-config.json", IncludeInConfigTransfer: true),
        new("emote-test", "Emote Test", "experimental-emote-test-config.json")
    ];
}
