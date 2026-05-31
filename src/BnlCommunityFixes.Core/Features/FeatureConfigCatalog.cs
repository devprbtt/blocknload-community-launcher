namespace BnlCommunityFixes.Core.Features;

public static partial class FeatureConfigCatalog
{
    private static IReadOnlyList<FeatureConfigDefinition> AllDefinitions()
    {
        var features = new List<FeatureConfigDefinition>();
        features.AddRange(GetUiFeatures());
        features.AddRange(GetGameplayFeatures());
        features.AddRange(GetUtilityFeatures());
        features.AddRange(GetReplacementFeatures());
        return features;
    }

    public static IReadOnlyList<FeatureConfigDefinition> All { get; } =
        AllDefinitions();

    public static IReadOnlyList<string> ExperimentalBuildTriggerFiles { get; } =
        All.Where(static feature => feature.TriggersExperimentalBuild)
            .Select(static feature => feature.FileName)
            .ToArray();

    public static IReadOnlyList<string> ManagedProfileFiles { get; } =
        All.Where(static feature => feature.IsManagedProfile)
            .Select(static feature => feature.FileName)
            .ToArray();

    public static IReadOnlyList<FeatureConfigDefinition> ConfigTransferFeatures { get; } =
        All.Where(static feature => feature.IncludeInConfigTransfer)
            .ToArray();
}
