using System.Diagnostics;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Updating;

namespace BnlCommunityFixes.Core.Services;

/// <summary>
/// Orchestrates update checking and downloading. UI interactions are injected
/// via callbacks so this class has no dependency on any UI framework.
/// </summary>
public sealed class UpdateCoordinator
{
    private readonly AppPaths _paths;
    private readonly Logger _logger;
    private readonly LauncherSettings _settings;
    private readonly LauncherRuntimeOptions _runtimeOptions;
    private readonly DownloadService _downloadService;
    private readonly ManifestService _manifestService;

    /// <summary>Called when an update is available. Return true to accept the update.</summary>
    public Func<UpdateManifest, bool, Task<bool>>? ConfirmUpdate { get; set; }

    /// <summary>Called during download to report progress (0-100, status message).</summary>
    public Action<int, string>? ReportProgress { get; set; }

    public UpdateCoordinator(AppPaths paths, Logger logger, LauncherSettings settings, LauncherRuntimeOptions runtimeOptions, HttpClient httpClient)
    {
        _paths = paths;
        _logger = logger;
        _settings = settings;
        _runtimeOptions = runtimeOptions;
        _downloadService = new DownloadService(httpClient);
        _manifestService = new ManifestService(httpClient);
    }

    public async Task<UpdateCheckResult> CheckAndApplyIfAcceptedAsync(CancellationToken cancellationToken = default)
    {
        var result = new UpdateCheckResult();

        try
        {
            _logger.Info($"Checking manifest at {_settings.ManifestUrl}.");
            var manifest = await _manifestService.FetchAsync(_settings.ManifestUrl, _settings.Product, cancellationToken: cancellationToken);
            var launcherAssetKey = manifest.ResolvePreferredLauncherAssetKey();
            var launcherAsset = manifest.ResolvePreferredLauncherAsset();
            var localVersion = LauncherVersion.GetCurrentVersion();

            result.RemoteVersion = manifest.Version;
            result.UpdateAvailable = VersionService.IsRemoteNewer(localVersion, manifest.Version);
            result.ForcedUpdate = VersionService.IsBelowMinimum(localVersion, manifest.MinimumSupportedVersion);

            _logger.Info($"Local version={localVersion}, remote version={manifest.Version}, forced={result.ForcedUpdate}, launcherAsset={launcherAssetKey}.");

            if (!result.UpdateAvailable && !result.ForcedUpdate)
                return result;

            bool accepted;
            if (_runtimeOptions.HeadlessSmokeTest)
            {
                accepted = true;
            }
            else if (ConfirmUpdate is not null)
            {
                accepted = await ConfirmUpdate(manifest, result.ForcedUpdate);
            }
            else
            {
                // No confirmation callback registered — skip update silently
                return result;
            }

            if (!accepted)
                return result;

            await DownloadAssetsAsync(launcherAsset, cancellationToken);
            await VerifyAssetsAsync(launcherAsset, cancellationToken);
            LaunchUpdater(manifest);

            result.ShouldExitForUpdate = true;
            return result;
        }
        catch (Exception exception)
        {
            _logger.Exception(exception, "Update check failed");
            throw;
        }
    }

    private async Task DownloadAssetsAsync(UpdateAsset launcherAsset, CancellationToken cancellationToken)
    {
        var totalBytes = launcherAsset.Size ?? 0;
        long downloadedBytes = 0;

        var progress = new Progress<long>(bytes =>
        {
            downloadedBytes += bytes;
            if (totalBytes > 0)
            {
                var percent = (int)((double)downloadedBytes / totalBytes * 100);
                ReportProgress?.Invoke(percent, $"Downloading update: {percent}%");
            }
        });

        _logger.Info($"Downloading launcher update from {launcherAsset.Url}.");
        await _downloadService.DownloadFileAsync(launcherAsset.Url, _paths.LauncherTempPath, progress, cancellationToken);
    }

    private async Task VerifyAssetsAsync(UpdateAsset launcherAsset, CancellationToken cancellationToken)
    {
        if (!await HashService.VerifySha256Async(_paths.LauncherTempPath, launcherAsset.Sha256, cancellationToken))
            throw new InvalidOperationException("Downloaded launcher hash verification failed.");
    }

    private void LaunchUpdater(UpdateManifest manifest)
    {
        _logger.Info("Launching launcher update helper.");
        // Always use the newly downloaded self-contained launcher as the
        // helper. The installed launcher may be a small apphost from a
        // multi-file deployment; copying that executable alone would leave
        // its managed/runtime dependencies behind and make the helper exit
        // before it can install the update.
        File.Copy(_paths.LauncherTempPath, _paths.LauncherUpdateHelperPath, true);
        EnsureExecutable(_paths.LauncherUpdateHelperPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _paths.LauncherUpdateHelperPath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_paths.LauncherUpdateHelperPath)!
        };

        startInfo.ArgumentList.Add(UpdaterArgumentParser.ApplyUpdateSwitch);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(_paths.LauncherPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(_paths.LauncherTempPath);
        AddExternalLauncherTarget(startInfo);
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--log");
        startInfo.ArgumentList.Add(_paths.UpdaterLogPath);
        startInfo.ArgumentList.Add("--restart");

        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add("--restart-arg");
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
    }

    private void AddExternalLauncherTarget(ProcessStartInfo startInfo)
    {
        try
        {
            if (!File.Exists(_paths.BootstrapSourcePath)) return;
            var bootstrapSource = File.ReadAllText(_paths.BootstrapSourcePath).Trim();
            if (string.IsNullOrWhiteSpace(bootstrapSource)) return;
            var normalizedSource = Path.GetFullPath(bootstrapSource);
            var pathComparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (string.Equals(normalizedSource, Path.GetFullPath(_paths.LauncherPath), pathComparison)) return;
            startInfo.ArgumentList.Add("--external-target");
            startInfo.ArgumentList.Add(normalizedSource);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not add external launcher target: {ex.Message}");
        }
    }

    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            File.GetUnixFileMode(path) |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherExecute);
    }
}
