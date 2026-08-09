using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class FeatureSettingsService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths paths;
    private readonly RuntimeMenuSyncService runtimeSync = new();
    private string? runtimeConfigPath;

    public FeatureSettingsService(AppPaths paths)
    {
        this.paths = paths;
    }

    public void SetRuntimeConfigPath(string path)
    {
        runtimeConfigPath = path;
    }

    public CrosshairSettings LoadCrosshairSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "crosshair-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("crosshair-config.json", new CrosshairSettings());
            settings = runtimeSync.ReadCrosshairSettings(runtimeConfigPath, settings);
            Save("crosshair-config.json", settings);
            return settings;
        }
        return Load("crosshair-config.json", new CrosshairSettings());
    }

    public void SaveCrosshairSettings(CrosshairSettings settings)
    {
        Save("crosshair-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteCrosshairSettings(runtimeConfigPath, settings);
    }

    public FovSettings LoadFovSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "fov-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("fov-config.json", new FovSettings());
            settings = runtimeSync.ReadFovSettings(runtimeConfigPath, settings);
            Save("fov-config.json", settings);
            return settings;
        }
        return Load("fov-config.json", new FovSettings());
    }

    public void SaveFovSettings(FovSettings settings)
    {
        Save("fov-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteFovSettings(runtimeConfigPath, settings);
    }

    public TeamColorSettings LoadTeamColorSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-team-color-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("experimental-team-color-config.json", new TeamColorSettings());
            settings = runtimeSync.ReadTeamColorSettings(runtimeConfigPath, settings);
            Save("experimental-team-color-config.json", settings);
            return settings;
        }
        return Load("experimental-team-color-config.json", new TeamColorSettings());
    }

    public void SaveTeamColorSettings(TeamColorSettings settings)
    {
        Save("experimental-team-color-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteTeamColorSettings(runtimeConfigPath, settings);
    }

    public DamageHealingSettings LoadDamageHealingSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "damage-healing-indicator-config.json");

        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            // Runtime config is newer — read launcher JSON first for non-runtime fields
            // (colors, enabled flag), then overlay the runtime values on top.
            var settings = Load("damage-healing-indicator-config.json", new DamageHealingSettings());
            settings = runtimeSync.ReadDamageHealingSettings(runtimeConfigPath, settings);
            // Write back to launcher JSON so both files are in sync.
            Save("damage-healing-indicator-config.json", settings);
            return settings;
        }

        return Load("damage-healing-indicator-config.json", new DamageHealingSettings());
    }

    public void SaveDamageHealingSettings(DamageHealingSettings settings)
    {
        Save("damage-healing-indicator-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteDamageHealingSettings(runtimeConfigPath, settings);
    }

    public HealAlertSettings LoadHealAlertSettings() => Load("heal-alert-indicator-config.json", new HealAlertSettings());
    public void SaveHealAlertSettings(HealAlertSettings settings) => Save("heal-alert-indicator-config.json", settings);

    public BaseObjectiveBeamSettings LoadBaseObjectiveBeamSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-base-objective-beam-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("experimental-base-objective-beam-config.json", new BaseObjectiveBeamSettings());
            settings = runtimeSync.ReadBaseObjectiveBeamSettings(runtimeConfigPath, settings);
            Save("experimental-base-objective-beam-config.json", settings);
            return settings;
        }
        return Load("experimental-base-objective-beam-config.json", new BaseObjectiveBeamSettings());
    }

    public void SaveBaseObjectiveBeamSettings(BaseObjectiveBeamSettings settings)
    {
        Save("experimental-base-objective-beam-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteBaseObjectiveBeamSettings(runtimeConfigPath, settings);
    }

    public ShieldBuffBarSettings LoadShieldBuffBarSettings() => Load("experimental-enemy-shield-buffbar-config.json", new ShieldBuffBarSettings());
    public void SaveShieldBuffBarSettings(ShieldBuffBarSettings settings) => Save("experimental-enemy-shield-buffbar-config.json", settings);

    public LocalBuildPreviewSettings LoadLocalBuildPreviewSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-local-build-preview-config.json");
        var settings = Load("experimental-local-build-preview-config.json", new LocalBuildPreviewSettings());
        // Only sync the runtime toggle when the feature is baked in (Enabled=true).
        // If it's disabled in the launcher the F8 entry doesn't exist, nothing to sync.
        if (settings.Enabled && runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            settings = runtimeSync.ReadLocalBuildPreviewSettings(runtimeConfigPath, settings);
            Save("experimental-local-build-preview-config.json", settings);
        }
        return settings;
    }

    public void SaveLocalBuildPreviewSettings(LocalBuildPreviewSettings settings)
    {
        Save("experimental-local-build-preview-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteLocalBuildPreviewSettings(runtimeConfigPath, settings);
    }

    public AimHealthbarSettings LoadAimHealthbarSettings() => Load("aim-healthbar-config.json", new AimHealthbarSettings());
    public void SaveAimHealthbarSettings(AimHealthbarSettings settings) => Save("aim-healthbar-config.json", settings);

    public DeathCamHealthbarSettings LoadDeathCamHealthbarSettings() => Load("deathcam-healthbar-config.json", new DeathCamHealthbarSettings());
    public void SaveDeathCamHealthbarSettings(DeathCamHealthbarSettings settings) => Save("deathcam-healthbar-config.json", settings);

    public AutoCasualQueueSettings LoadAutoCasualQueueSettings() => Load("experimental-auto-casual-queue-config.json", new AutoCasualQueueSettings());
    public void SaveAutoCasualQueueSettings(AutoCasualQueueSettings settings) => Save("experimental-auto-casual-queue-config.json", settings);

    public FriendlyLowHealthSettings LoadFriendlyLowHealthSettings() => Load("friendly-low-health-config.json", new FriendlyLowHealthSettings());
    public void SaveFriendlyLowHealthSettings(FriendlyLowHealthSettings settings) => Save("friendly-low-health-config.json", settings);

    public AutoCrouchSettings LoadAutoCrouchSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-auto-crouch-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("experimental-auto-crouch-config.json", new AutoCrouchSettings());
            settings = runtimeSync.ReadAutoCrouchSettings(runtimeConfigPath, settings);
            Save("experimental-auto-crouch-config.json", settings);
            return settings;
        }
        return Load("experimental-auto-crouch-config.json", new AutoCrouchSettings());
    }

    public void SaveAutoCrouchSettings(AutoCrouchSettings settings)
    {
        Save("experimental-auto-crouch-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteAutoCrouchSettings(runtimeConfigPath, settings);
    }

    public MotionBlurSettings LoadMotionBlurSettings() =>
        Load("experimental-motion-blur-config.json", new MotionBlurSettings());

    public void SaveMotionBlurSettings(MotionBlurSettings settings) =>
        Save("experimental-motion-blur-config.json", settings);

    public VisualEnhancementsSettings LoadVisualEnhancementsSettings() =>
        Load("experimental-visual-enhancements-config.json", new VisualEnhancementsSettings());

    public void SaveVisualEnhancementsSettings(VisualEnhancementsSettings settings) =>
        Save("experimental-visual-enhancements-config.json", settings);

    public NigelSniperVisualSettings LoadNigelSniperVisualSettings() =>
        Load("experimental-nigel-sniper-visual-config.json", new NigelSniperVisualSettings());

    public void SaveNigelSniperVisualSettings(NigelSniperVisualSettings settings) =>
        Save("experimental-nigel-sniper-visual-config.json", settings);

    public NinjaTurtleSkinSettings LoadNinjaTurtleSkinSettings() =>
        Load("experimental-ninja-turtle-skin-config.json", new NinjaTurtleSkinSettings());

    public void SaveNinjaTurtleSkinSettings(NinjaTurtleSkinSettings settings) =>
        Save("experimental-ninja-turtle-skin-config.json", settings);

    public VanderBlueSkinSettings LoadVanderBlueSkinSettings() =>
        Load("experimental-vander-blue-skin-config.json", new VanderBlueSkinSettings());

    public void SaveVanderBlueSkinSettings(VanderBlueSkinSettings settings) =>
        Save("experimental-vander-blue-skin-config.json", settings);

    public HinduYetiSkinSettings LoadHinduYetiSkinSettings() =>
        Load("experimental-hindu-yeti-skin-config.json", new HinduYetiSkinSettings());

    public void SaveHinduYetiSkinSettings(HinduYetiSkinSettings settings) =>
        Save("experimental-hindu-yeti-skin-config.json", settings);

    public DarklordSweetScienceSkinSettings LoadDarklordSweetScienceSkinSettings() =>
        Load("experimental-darklord-sweet-science-skin-config.json",
            new DarklordSweetScienceSkinSettings());

    public void SaveDarklordSweetScienceSkinSettings(
        DarklordSweetScienceSkinSettings settings) =>
        Save("experimental-darklord-sweet-science-skin-config.json", settings);

    public HideImpactVfxSettings LoadHideImpactVfxSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-hide-impact-vfx-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("experimental-hide-impact-vfx-config.json", new HideImpactVfxSettings());
            settings = runtimeSync.ReadHideImpactVfxSettings(runtimeConfigPath, settings);
            Save("experimental-hide-impact-vfx-config.json", settings);
            return settings;
        }
        return Load("experimental-hide-impact-vfx-config.json", new HideImpactVfxSettings());
    }

    public void SaveHideImpactVfxSettings(HideImpactVfxSettings settings)
    {
        Save("experimental-hide-impact-vfx-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteHideImpactVfxSettings(runtimeConfigPath, settings);
    }

    public UnitGuiScaleSettings LoadUnitGuiScaleSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "unit-gui-scale-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("unit-gui-scale-config.json", new UnitGuiScaleSettings());
            settings = runtimeSync.ReadUnitGuiScaleSettings(runtimeConfigPath, settings);
            Save("unit-gui-scale-config.json", settings);
            return settings;
        }
        return Load("unit-gui-scale-config.json", new UnitGuiScaleSettings());
    }

    public void SaveUnitGuiScaleSettings(UnitGuiScaleSettings settings)
    {
        Save("unit-gui-scale-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteUnitGuiScaleSettings(runtimeConfigPath, settings);
    }

    public WsiSettings LoadWsiSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "wsi-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("wsi-config.json", new WsiSettings());
            settings = runtimeSync.ReadWsiSettings(runtimeConfigPath, settings);
            Save("wsi-config.json", settings);
            return settings;
        }
        return Load("wsi-config.json", new WsiSettings());
    }

    public void SaveWsiSettings(WsiSettings settings)
    {
        Save("wsi-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteWsiSettings(runtimeConfigPath, settings);
    }

    public MapRenderOverrideSettings LoadMapRenderOverrideSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "experimental-map-render-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("experimental-map-render-config.json", new MapRenderOverrideSettings());
            settings = runtimeSync.ReadMapRenderOverrideSettings(runtimeConfigPath, settings);
            Save("experimental-map-render-config.json", settings);
            return settings;
        }
        return Load("experimental-map-render-config.json", new MapRenderOverrideSettings());
    }

    public void SaveMapRenderOverrideSettings(MapRenderOverrideSettings settings)
    {
        Save("experimental-map-render-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteMapRenderOverrideSettings(runtimeConfigPath, settings);
    }

    public TeammateHpSettings LoadTeammateHpSettings()
    {
        var launcherConfigPath = Path.Combine(paths.PatchingDir, "teammate-hp-config.json");
        if (runtimeConfigPath != null && runtimeSync.IsRuntimeNewer(runtimeConfigPath, launcherConfigPath))
        {
            var settings = Load("teammate-hp-config.json", new TeammateHpSettings());
            settings = runtimeSync.ReadTeammateHpSettings(runtimeConfigPath, settings);
            NormalizeTeammateHpSettings(settings);
            Save("teammate-hp-config.json", settings);
            return settings;
        }
        var loaded = Load("teammate-hp-config.json", new TeammateHpSettings());
        NormalizeTeammateHpSettings(loaded);
        return loaded;
    }

    public void SaveTeammateHpSettings(TeammateHpSettings settings)
    {
        NormalizeTeammateHpSettings(settings);
        Save("teammate-hp-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteTeammateHpSettings(runtimeConfigPath, settings);
    }

    private static void NormalizeTeammateHpSettings(TeammateHpSettings settings)
    {
        if (settings == null)
            return;

        if (!settings.ShowHpText && !settings.HideNameBackground && settings.Enabled)
            settings.ShowHpText = true;

        settings.Enabled = settings.ShowHpText || settings.HideNameBackground;
    }

    public SegmentedHealthbarSettings LoadSegmentedHealthbarSettings() => Load("experimental-segmented-healthbar-config.json", new SegmentedHealthbarSettings());
    public void SaveSegmentedHealthbarSettings(SegmentedHealthbarSettings settings) => Save("experimental-segmented-healthbar-config.json", settings);

    public FontOverrideSettings LoadFontOverrideSettings() => Load("experimental-font-override-config.json", new FontOverrideSettings());
    public void SaveFontOverrideSettings(FontOverrideSettings settings) => Save("experimental-font-override-config.json", settings);

    public PerformanceOptSettings LoadPerformanceOptSettings() => Load("experimental-performance-opt-config.json", new PerformanceOptSettings());
    public void SavePerformanceOptSettings(PerformanceOptSettings settings) => Save("experimental-performance-opt-config.json", settings);
    public PerformanceTelemetrySettings LoadPerformanceTelemetrySettings() => Load("experimental-performance-telemetry-config.json", new PerformanceTelemetrySettings());
    public void SavePerformanceTelemetrySettings(PerformanceTelemetrySettings settings) => Save("experimental-performance-telemetry-config.json", settings);
    public MinimapPerformanceSettings LoadMinimapPerformanceSettings() => Load("experimental-minimap-performance-config.json", new MinimapPerformanceSettings());
    public void SaveMinimapPerformanceSettings(MinimapPerformanceSettings settings) => Save("experimental-minimap-performance-config.json", settings);
    public WsiPerformanceSettings LoadWsiPerformanceSettings() => Load("experimental-wsi-performance-config.json", new WsiPerformanceSettings());
    public void SaveWsiPerformanceSettings(WsiPerformanceSettings settings) => Save("experimental-wsi-performance-config.json", settings);
    public FpsCounterSettings LoadFpsCounterSettings() => Load("experimental-fps-counter-config.json", new FpsCounterSettings());
    public void SaveFpsCounterSettings(FpsCounterSettings settings) => Save("experimental-fps-counter-config.json", settings);

    public TimeAssaultSettings LoadTimeAssaultSettings() => Load("experimental-time-assault-config.json", new TimeAssaultSettings());
    public void SaveTimeAssaultSettings(TimeAssaultSettings settings) => Save("experimental-time-assault-config.json", settings);

    public BotModeSettings LoadBotModeSettings() => Load("experimental-bot-mode-config.json", new BotModeSettings());
    public void SaveBotModeSettings(BotModeSettings settings) => Save("experimental-bot-mode-config.json", settings);

    public void PushLauncherSettingsToRuntime()
    {
        if (runtimeConfigPath == null)
        {
            return;
        }

        runtimeSync.WriteCrosshairSettings(runtimeConfigPath, Load("crosshair-config.json", new CrosshairSettings()));
        runtimeSync.WriteFovSettings(runtimeConfigPath, Load("fov-config.json", new FovSettings()));
        runtimeSync.WriteTeamColorSettings(runtimeConfigPath, Load("experimental-team-color-config.json", new TeamColorSettings()));
        runtimeSync.WriteDamageHealingSettings(runtimeConfigPath, Load("damage-healing-indicator-config.json", new DamageHealingSettings()));
        runtimeSync.WriteLocalBuildPreviewSettings(runtimeConfigPath, Load("experimental-local-build-preview-config.json", new LocalBuildPreviewSettings()));
        runtimeSync.WriteBaseObjectiveBeamSettings(runtimeConfigPath, Load("experimental-base-objective-beam-config.json", new BaseObjectiveBeamSettings()));
        runtimeSync.WriteTeammateHpSettings(runtimeConfigPath, Load("teammate-hp-config.json", new TeammateHpSettings()));
        runtimeSync.WriteAutoCrouchSettings(runtimeConfigPath, Load("experimental-auto-crouch-config.json", new AutoCrouchSettings()));
        runtimeSync.WriteHideImpactVfxSettings(runtimeConfigPath, Load("experimental-hide-impact-vfx-config.json", new HideImpactVfxSettings()));
        runtimeSync.WriteUnitGuiScaleSettings(runtimeConfigPath, Load("unit-gui-scale-config.json", new UnitGuiScaleSettings()));
        runtimeSync.WriteWsiSettings(runtimeConfigPath, Load("wsi-config.json", new WsiSettings()));
        runtimeSync.WriteMapRenderOverrideSettings(runtimeConfigPath, Load("experimental-map-render-config.json", new MapRenderOverrideSettings()));
    }

    public bool EnsureAutoCasualQueueTestDefaultEnabled()
    {
        var migrationMarkerPath = Path.Combine(paths.DataDir, "auto-casual-queue-2.3-default-enabled.migrated");
        if (File.Exists(migrationMarkerPath))
        {
            return false;
        }

        var settings = LoadAutoCasualQueueSettings();
        var changed = !settings.Enabled;
        if (changed)
        {
            settings.Enabled = true;
            SaveAutoCasualQueueSettings(settings);
        }

        Directory.CreateDirectory(paths.DataDir);
        File.WriteAllText(migrationMarkerPath, DateTimeOffset.UtcNow.ToString("O"), new UTF8Encoding(false));
        return changed;
    }

    private T Load<T>(string fileName, T fallback) where T : class
    {
        var path = Path.Combine(paths.PatchingDir, fileName);
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, ReadOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void Save<T>(string fileName, T settings)
    {
        var path = Path.Combine(paths.PatchingDir, fileName);
        var json = JsonSerializer.Serialize(settings, WriteOptions);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
