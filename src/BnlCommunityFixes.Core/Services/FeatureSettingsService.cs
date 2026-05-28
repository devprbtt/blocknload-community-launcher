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

    public BaseObjectiveBeamSettings LoadBaseObjectiveBeamSettings() => Load("experimental-base-objective-beam-config.json", new BaseObjectiveBeamSettings());
    public void SaveBaseObjectiveBeamSettings(BaseObjectiveBeamSettings settings) => Save("experimental-base-objective-beam-config.json", settings);

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

    public AutoCrouchSettings LoadAutoCrouchSettings() => Load("experimental-auto-crouch-config.json", new AutoCrouchSettings());
    public void SaveAutoCrouchSettings(AutoCrouchSettings settings) => Save("experimental-auto-crouch-config.json", settings);

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
            Save("teammate-hp-config.json", settings);
            return settings;
        }
        return Load("teammate-hp-config.json", new TeammateHpSettings());
    }

    public void SaveTeammateHpSettings(TeammateHpSettings settings)
    {
        Save("teammate-hp-config.json", settings);
        if (runtimeConfigPath != null)
            runtimeSync.WriteTeammateHpSettings(runtimeConfigPath, settings);
    }

    public SegmentedHealthbarSettings LoadSegmentedHealthbarSettings() => Load("experimental-segmented-healthbar-config.json", new SegmentedHealthbarSettings());
    public void SaveSegmentedHealthbarSettings(SegmentedHealthbarSettings settings) => Save("experimental-segmented-healthbar-config.json", settings);

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
