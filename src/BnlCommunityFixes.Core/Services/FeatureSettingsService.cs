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

    public FeatureSettingsService(AppPaths paths)
    {
        this.paths = paths;
    }

    public CrosshairSettings LoadCrosshairSettings() => Load("crosshair-config.json", new CrosshairSettings());
    public void SaveCrosshairSettings(CrosshairSettings settings) => Save("crosshair-config.json", settings);

    public FovSettings LoadFovSettings() => Load("fov-config.json", new FovSettings());
    public void SaveFovSettings(FovSettings settings) => Save("fov-config.json", settings);

    public TeamColorSettings LoadTeamColorSettings() => Load("experimental-team-color-config.json", new TeamColorSettings());
    public void SaveTeamColorSettings(TeamColorSettings settings) => Save("experimental-team-color-config.json", settings);

    public FontSettings LoadFontSettings() => Load("experimental-font-config.json", new FontSettings());
    public void SaveFontSettings(FontSettings settings) => Save("experimental-font-config.json", settings);

    public DamageHealingSettings LoadDamageHealingSettings() => Load("damage-healing-indicator-config.json", new DamageHealingSettings());
    public void SaveDamageHealingSettings(DamageHealingSettings settings) => Save("damage-healing-indicator-config.json", settings);

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
