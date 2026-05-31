namespace BnlCommunityFixes.Core.Features;

public sealed record FeatureConfigDefinition(
    string Key,
    string DisplayName,
    string FileName,
    string EnabledPropertyName = "enabled",
    bool TriggersExperimentalBuild = true,
    bool IsManagedProfile = true,
    bool IncludeInConfigTransfer = false);
