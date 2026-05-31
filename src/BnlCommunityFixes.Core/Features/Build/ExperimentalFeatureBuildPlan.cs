namespace BnlCommunityFixes.Core.Features.Build;

public sealed class ExperimentalFeatureBuildPlan
{
    public ExperimentalFeatureBuildPlan(IReadOnlyList<ExperimentalFeatureBuildEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ExperimentalFeatureBuildEntry> Entries { get; }

    public IReadOnlyList<ExperimentalFeatureBuildEntry> TriggerEntries =>
        Entries.Where(static entry => entry.Definition.TriggersExperimentalBuild).ToArray();

    public IReadOnlyList<ExperimentalFeatureBuildEntry> EnabledTriggerEntries =>
        Entries.Where(static entry => entry.Definition.TriggersExperimentalBuild && entry.IsEnabled).ToArray();

    public bool HasAnyTriggerConfig =>
        TriggerEntries.Any(static entry => entry.ConfigExists);

    public bool HasEnabledTriggerFeature =>
        EnabledTriggerEntries.Count > 0;

    public string DescribeEnabledTriggerFeatures() =>
        string.Join(", ", EnabledTriggerEntries.Select(static entry => entry.Definition.Key));
}
