namespace BnlCommunityFixes.Core.Features.Build.Patching;

public interface IExperimentalFeaturePatcher
{
    string FeatureKey { get; }

    void Apply(ExperimentalPatchContext context);
}
