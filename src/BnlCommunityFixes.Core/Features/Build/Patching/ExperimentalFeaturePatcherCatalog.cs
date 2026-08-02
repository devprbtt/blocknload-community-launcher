namespace BnlCommunityFixes.Core.Features.Build.Patching;

public static class ExperimentalFeaturePatcherCatalog
{
    /// <summary>Patchers that always run whenever any experimental build happens.</summary>
    public static IReadOnlyList<IExperimentalFeaturePatcher> AlwaysRun { get; } =
    [
        new NullGuardsPatcher(),
        new FontUiFeaturePatcher(),
        new SkipIntroFeaturePatcher(),
        new DisableFrameCapFeaturePatcher()
    ];

    /// <summary>Patchers selected by feature key.</summary>
    public static IReadOnlyList<IExperimentalFeaturePatcher> All { get; } =
    [
        new TeamColorFeaturePatcher(),
        new CrosshairFeaturePatcher(),
        new AutoQueueFeaturePatcher(),
        new AutoCrouchFeaturePatcher(),
        new MotionBlurFeaturePatcher(),
        new VisualEnhancementsFeaturePatcher(),
        new NigelSniperVisualFeaturePatcher(),
        new ShieldTimerFeaturePatcher(),
        new UnitGuiScaleFeaturePatcher(),
        new WsiScaleFeaturePatcher(),
        new SegmentedHealthbarFeaturePatcher(),
        new MapRenderFeaturePatcher(),
        new TeammateHpFeaturePatcher(),
        new BaseObjectiveBeamFeaturePatcher(),
        new AimHealthbarFeaturePatcher(),
        new FriendlyLowHealthFeaturePatcher(),
        new HealAlertsFeaturePatcher(),
        new FontOverrideFeaturePatcher(),
        new DamageHealingFeaturePatcher(),
        new DeathCamHealthbarFeaturePatcher(),
        new HideImpactVfxFeaturePatcher(),
        new BuildPreviewFeaturePatcher(),
        new DebugMenuFeaturePatcher(),
        new MatchReplayRecorderFeaturePatcher(),
        new FovFeaturePatcher(),
        new PerformanceOptPatcher(),
        new TimeAssaultFeaturePatcher()
        // Bot mode is implemented by the embedded bnlReloaded server.
    ];
}
