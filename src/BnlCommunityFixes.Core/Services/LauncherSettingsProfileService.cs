using System.Reflection;
using BnlCommunityFixes.Core.Features;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class LauncherSettingsProfileService
{
    private const string ResourcePrefix = "Patching.";

    private readonly AppPaths paths;

    public LauncherSettingsProfileService(AppPaths paths)
    {
        this.paths = paths;
    }

    public IReadOnlyList<string> ManagedFiles => FeatureConfigCatalog.ManagedProfileFiles;

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
            ResetRecommendedSnapshotFromResources(logger);
            ApplySnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            settings.SettingsProfile = LauncherSettings.PersonalSettingsProfile;
            settings.LastLauncherVersion = currentVersion;
            return true;
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

        if (IsPersonalProfile(profile))
        {
            // Switching from recommended must not overwrite an existing personal snapshot
            // with the currently active recommended values.
            if (IsPersonalProfile(settings.SettingsProfile) || !HasPersonalSnapshot())
            {
                SaveSnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            }
            ApplySnapshot(GetPersonalSettingsDirectory(), logger, "personal");
            settings.SettingsProfile = LauncherSettings.PersonalSettingsProfile;
            return;
        }

        // Switching to recommended — never overwrite the recommended snapshot with active files.
        // The recommended snapshot is managed exclusively by ResetRecommendedSnapshotFromResources.
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
        featureSettingsService.SetRuntimeConfigPath(runtimeConfigPath);

        // Pull in-game runtime edits back into the launcher JSON first if they are newer.
        // This prevents startup sync from stomping runtime-menu changes like crosshair tweaks.
        featureSettingsService.LoadCrosshairSettings();
        featureSettingsService.LoadFovSettings();
        featureSettingsService.LoadTeamColorSettings();
        featureSettingsService.LoadDamageHealingSettings();
        featureSettingsService.LoadLocalBuildPreviewSettings();
        featureSettingsService.LoadBaseObjectiveBeamSettings();
        featureSettingsService.LoadTeammateHpSettings();
        featureSettingsService.LoadAutoCrouchSettings();
        featureSettingsService.LoadHideImpactVfxSettings();
        featureSettingsService.LoadUnitGuiScaleSettings();
        featureSettingsService.LoadWsiSettings();
        featureSettingsService.LoadMapRenderOverrideSettings();

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

        foreach (var fileName in FeatureConfigCatalog.ManagedProfileFiles)
        {
            var resourceName = ResourcePrefix + fileName;
            var resourceAssembly = GetResourceAssemblies().FirstOrDefault(assembly =>
                assembly.GetManifestResourceInfo(resourceName) is not null);
            if (resourceAssembly is null)
            {
                logger.Warning($"Recommended settings resource '{resourceName}' is missing.");
                continue;
            }

            using var resourceStream = resourceAssembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                logger.Warning($"Recommended settings resource '{resourceName}' could not be opened from '{resourceAssembly.GetName().Name}'.");
                continue;
            }

            var destinationPath = Path.Combine(GetRecommendedSettingsDirectory(), fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var output = File.Create(destinationPath);
            resourceStream.CopyTo(output);
            logger.Info($"Reset recommended settings snapshot '{fileName}' from '{resourceAssembly.GetName().Name}'.");
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

        foreach (var fileName in FeatureConfigCatalog.ManagedProfileFiles)
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
        }
        // Never save active files into the recommended snapshot — it is managed exclusively
        // by ResetRecommendedSnapshotFromResources (called on version upgrade).
    }

    private void SaveSnapshot(string snapshotDirectory, Logger logger, string profileName)
    {
        foreach (var fileName in FeatureConfigCatalog.ManagedProfileFiles)
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
        FeatureConfigCatalog.ManagedProfileFiles.Any(fileName => File.Exists(Path.Combine(snapshotDirectory, fileName)));

    private void ClearActiveManagedFiles()
    {
        foreach (var fileName in FeatureConfigCatalog.ManagedProfileFiles)
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

    private static IReadOnlyList<Assembly> GetResourceAssemblies()
    {
        var assemblies = new List<Assembly>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddAssembly(Assembly? assembly)
        {
            if (assembly is null)
            {
                return;
            }

            var fullName = assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N");
            if (seen.Add(fullName))
            {
                assemblies.Add(assembly);
            }
        }

        AddAssembly(Assembly.GetEntryAssembly());
        AddAssembly(Assembly.GetExecutingAssembly());

        return assemblies;
    }

    private string GetRecommendedSettingsDirectory() =>
        Path.Combine(paths.DataDir, "profiles", "recommended");

    private string GetPersonalSettingsDirectory() =>
        Path.Combine(paths.DataDir, "profiles", "personal");
}
