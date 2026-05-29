using System.Reflection;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class LauncherSettingsProfileService
{
    private const string ResourcePrefix = "Patching.";

    private static readonly string[] ManagedConfigFileNames =
    [
        "crosshair-config.json",
        "fov-config.json",
        "experimental-team-color-config.json",
        "damage-healing-indicator-config.json",
        "heal-alert-indicator-config.json",
        "experimental-base-objective-beam-config.json",
        "experimental-enemy-shield-buffbar-config.json",
        "experimental-local-build-preview-config.json",
        "aim-healthbar-config.json",
        "deathcam-healthbar-config.json",
        "experimental-auto-casual-queue-config.json",
        "friendly-low-health-config.json",
        "teammate-hp-config.json",
        "experimental-auto-crouch-config.json",
        "experimental-hide-impact-vfx-config.json",
        "unit-gui-scale-config.json",
        "wsi-config.json",
        "experimental-map-render-config.json",
        "experimental-match-replay-recorder-config.json",
        "experimental-debug-menu-config.json",
        "experimental-segmented-healthbar-config.json",
        "experimental-font-override-config.json"
    ];

    private readonly AppPaths paths;

    public LauncherSettingsProfileService(AppPaths paths)
    {
        this.paths = paths;
    }

    public IReadOnlyList<string> ManagedFiles => ManagedConfigFileNames;

    public bool ApplyUpdateDefaultsIfNeeded(LauncherSettings settings, string currentVersion, Logger logger)
    {
        Normalize(settings);
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.LastLauncherVersion))
        {
            EnsureRecommendedSnapshotInitialized(logger);
            ApplyRecommendedSnapshot(logger);
            settings.LastLauncherVersion = currentVersion;
            settings.SettingsProfile = LauncherSettings.RecommendedSettingsProfile;
            return false;
        }

        if (string.Equals(settings.LastLauncherVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsPersonalProfile(settings.SettingsProfile))
        {
            SaveActiveToSelectedProfile(settings, logger);
        }

        ResetRecommendedSnapshotFromResources(logger);
        ApplyRecommendedSnapshot(logger);
        settings.SettingsProfile = LauncherSettings.RecommendedSettingsProfile;
        settings.LastLauncherVersion = currentVersion;
        return true;
    }

    public void ApplySelectedProfile(LauncherSettings settings, string profile, Logger logger)
    {
        Normalize(settings);
        SaveActiveToSelectedProfile(settings, logger);

        if (IsPersonalProfile(profile))
        {
            if (!HasPersonalSnapshot())
            {
                SaveSnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            }

            ApplySnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            settings.SettingsProfile = LauncherSettings.PersonalSettingsProfile;
            return;
        }

        EnsureRecommendedSnapshotInitialized(logger);
        ApplyRecommendedSnapshot(logger);
        settings.SettingsProfile = LauncherSettings.RecommendedSettingsProfile;
    }

    public void SyncActiveSettingsToRuntime(GameInstallInfo? installInfo)
    {
        if (installInfo?.IsDetected != true)
        {
            return;
        }

        var runtimeSync = new RuntimeMenuSyncService();
        var featureSettingsService = new FeatureSettingsService(paths);
        var runtimeConfigPath = runtimeSync.GetRuntimeConfigPath(installInfo.ManagedDirectoryPath);
        if (File.Exists(runtimeConfigPath))
        {
            File.Delete(runtimeConfigPath);
        }

        featureSettingsService.SetRuntimeConfigPath(runtimeConfigPath);
        featureSettingsService.PushLauncherSettingsToRuntime();
    }

    public void SyncSelectedSnapshotFromActive(LauncherSettings settings, Logger logger)
    {
        Normalize(settings);
        SaveActiveToSelectedProfile(settings, logger);
    }

    public bool ShouldShowUpdateNotice(LauncherSettings settings, string currentVersion)
    {
        Normalize(settings);
        return !string.IsNullOrWhiteSpace(currentVersion) &&
               string.Equals(settings.LastLauncherVersion, currentVersion, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(settings.DismissedUpdateNoticeVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    public void DismissUpdateNotice(LauncherSettings settings, string currentVersion)
    {
        Normalize(settings);
        settings.DismissedUpdateNoticeVersion = currentVersion;
    }

    public static bool IsPersonalProfile(string? profile) =>
        string.Equals(profile, LauncherSettings.PersonalSettingsProfile, StringComparison.OrdinalIgnoreCase);

    public static void Normalize(LauncherSettings settings)
    {
        if (!IsPersonalProfile(settings.SettingsProfile))
        {
            settings.SettingsProfile = LauncherSettings.RecommendedSettingsProfile;
        }
    }

    private void ApplyRecommendedSnapshot(Logger logger)
    {
        EnsureRecommendedSnapshotInitialized(logger);
        ApplySnapshot(GetRecommendedSettingsDirectory(), logger, "recommended");
    }

    private void ResetRecommendedSnapshotFromResources(Logger logger)
    {
        ClearDirectory(GetRecommendedSettingsDirectory());

        foreach (var fileName in ManagedConfigFileNames)
        {
            var resourceName = ResourcePrefix + fileName;
            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                logger.Warning($"Recommended settings resource '{resourceName}' is missing.");
                continue;
            }

            var destinationPath = Path.Combine(GetRecommendedSettingsDirectory(), fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var output = File.Create(destinationPath);
            resourceStream.CopyTo(output);
            logger.Info($"Reset recommended settings snapshot '{fileName}'.");
        }
    }

    private void EnsureRecommendedSnapshotInitialized(Logger logger)
    {
        if (HasSnapshot(GetRecommendedSettingsDirectory()))
        {
            return;
        }

        ResetRecommendedSnapshotFromResources(logger);
    }

    private void ApplySnapshot(string snapshotDirectory, Logger logger, string profileName)
    {
        ClearActiveManagedFiles();

        foreach (var fileName in ManagedConfigFileNames)
        {
            var sourcePath = Path.Combine(snapshotDirectory, fileName);
            var destinationPath = Path.Combine(paths.PatchingDir, fileName);

            if (!File.Exists(sourcePath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, true);
            logger.Info($"Applied {profileName} settings file '{fileName}'.");
        }
    }

    private void SaveActiveToSelectedProfile(LauncherSettings settings, Logger logger)
    {
        if (IsPersonalProfile(settings.SettingsProfile))
        {
            SaveSnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            return;
        }

        EnsureRecommendedSnapshotInitialized(logger);
        SaveSnapshot(GetRecommendedSettingsDirectory(), logger, "recommended");
    }

    private void SaveSnapshot(string snapshotDirectory, Logger logger, string profileName)
    {
        foreach (var fileName in ManagedConfigFileNames)
        {
            var sourcePath = Path.Combine(paths.PatchingDir, fileName);
            var destinationPath = Path.Combine(snapshotDirectory, fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (!File.Exists(sourcePath))
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                continue;
            }

            File.Copy(sourcePath, destinationPath, true);
            logger.Info($"Saved {profileName} settings file '{fileName}'.");
        }
    }

    private bool HasPersonalSnapshot() =>
        HasSnapshot(GetPersonalSettingsDirectory());

    private bool HasSnapshot(string snapshotDirectory) =>
        ManagedConfigFileNames.Any(fileName => File.Exists(Path.Combine(snapshotDirectory, fileName)));

    private void ClearActiveManagedFiles()
    {
        foreach (var fileName in ManagedConfigFileNames)
        {
            var destinationPath = Path.Combine(paths.PatchingDir, fileName);
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }
    }

    private static void ClearDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.Delete(path);
        }
    }

    private string GetRecommendedSettingsDirectory() =>
        Path.Combine(paths.DataDir, "profiles", "recommended");

    private string GetPersonalSettingsDirectory() =>
        Path.Combine(paths.DataDir, "profiles", "personal");
}
