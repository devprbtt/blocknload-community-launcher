namespace BnlCommunityFixes.Core.Features.Build;

public sealed record ExperimentalFeatureBuildEntry(
    FeatureConfigDefinition Definition,
    string ConfigPath,
    bool ConfigExists,
    bool IsEnabled);
