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

    public FontSettings LoadFontSettings() => Load("experimental-font-config.json", new FontSettings());
    public void SaveFontSettings(FontSettings settings) => Save("experimental-font-config.json", settings);

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

    public LocalBuildPreviewSettings LoadLocalBuildPreviewSettings() => Load("experimental-local-build-preview-config.json", new LocalBuildPreviewSettings());
    public void SaveLocalBuildPreviewSettings(LocalBuildPreviewSettings settings) => Save("experimental-local-build-preview-config.json", settings);

    public AimHealthbarSettings LoadAimHealthbarSettings() => Load("aim-healthbar-config.json", new AimHealthbarSettings());
    public void SaveAimHealthbarSettings(AimHealthbarSettings settings) => Save("aim-healthbar-config.json", settings);

    public DeathCamHealthbarSettings LoadDeathCamHealthbarSettings() => Load("deathcam-healthbar-config.json", new DeathCamHealthbarSettings());
    public void SaveDeathCamHealthbarSettings(DeathCamHealthbarSettings settings) => Save("deathcam-healthbar-config.json", settings);

    public AutoCasualQueueSettings LoadAutoCasualQueueSettings() => Load("experimental-auto-casual-queue-config.json", new AutoCasualQueueSettings());
    public void SaveAutoCasualQueueSettings(AutoCasualQueueSettings settings) => Save("experimental-auto-casual-queue-config.json", settings);

    public FriendlyLowHealthSettings LoadFriendlyLowHealthSettings() => Load("friendly-low-health-config.json", new FriendlyLowHealthSettings());
    public void SaveFriendlyLowHealthSettings(FriendlyLowHealthSettings settings) => Save("friendly-low-health-config.json", settings);

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
