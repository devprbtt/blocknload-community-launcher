using System.Diagnostics;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class UpdateCoordinator
{
    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly LauncherSettings settings;
    private readonly LauncherRuntimeOptions runtimeOptions;
    private readonly DownloadService downloadService;
    private readonly ManifestService manifestService;

    public UpdateCoordinator(AppPaths paths, Logger logger, LauncherSettings settings, LauncherRuntimeOptions runtimeOptions, HttpClient httpClient)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
        this.runtimeOptions = runtimeOptions;
        downloadService = new DownloadService(httpClient);
        manifestService = new ManifestService(httpClient);
    }

    public async Task<UpdateCheckResult> CheckAndApplyIfAcceptedAsync(CancellationToken cancellationToken = default)
    {
        var result = new UpdateCheckResult();

        try
        {
            logger.Info($"Checking manifest at {settings.ManifestUrl}.");
            var manifest = await manifestService.FetchAsync(settings.ManifestUrl, settings.Product, cancellationToken);
            var localVersion = LauncherVersion.GetCurrentVersion();

            result.RemoteVersion = manifest.Version;
            result.UpdateAvailable = VersionService.IsRemoteNewer(localVersion, manifest.Version);
            result.ForcedUpdate = VersionService.IsBelowMinimum(localVersion, manifest.MinimumSupportedVersion);

            logger.Info($"Local version={localVersion}, remote version={manifest.Version}, forced={result.ForcedUpdate}.");

            if (!result.UpdateAvailable && !result.ForcedUpdate)
            {
                return result;
            }

            var promptText = result.ForcedUpdate
                ? $"Version {manifest.Version} is required to continue. Install now?"
                : $"Version {manifest.Version} is available. Install now?";

            var accepted = runtimeOptions.HeadlessSmokeTest
                ? true
                : ShowPrompt(promptText, result.ForcedUpdate);
            if (!accepted)
            {
                return result;
            }

            await DownloadAssetsAsync(manifest, cancellationToken);
            await VerifyAssetsAsync(manifest, cancellationToken);
            LaunchUpdater(manifest);

            result.ShouldExitForUpdate = true;
            return result;
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Update check failed");
            MessageBox.Show(
                $"Update check failed.{Environment.NewLine}{exception.Message}",
                "BNL Community Fixes Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return result;
        }
    }

    private static bool ShowPrompt(string promptText, bool forcedUpdate)
    {
        var promptResult = MessageBox.Show(
            promptText,
            "BNL Community Fixes Update",
            forcedUpdate ? MessageBoxButtons.OKCancel : MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return forcedUpdate ? promptResult == DialogResult.OK : promptResult == DialogResult.Yes;
    }

    private async Task DownloadAssetsAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        var launcherAsset = manifest.Assets["launcher_exe"];
        var updaterAsset = manifest.Assets["updater_exe"];

        logger.Info($"Downloading launcher update from {launcherAsset.Url}.");
        await downloadService.DownloadFileAsync(launcherAsset.Url, paths.LauncherTempPath, cancellationToken);

        logger.Info($"Downloading updater update from {updaterAsset.Url}.");
        await downloadService.DownloadFileAsync(updaterAsset.Url, paths.UpdaterTempPath, cancellationToken);
    }

    private async Task VerifyAssetsAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        var launcherAsset = manifest.Assets["launcher_exe"];
        var updaterAsset = manifest.Assets["updater_exe"];

        if (!await HashService.VerifySha256Async(paths.LauncherTempPath, launcherAsset.Sha256, cancellationToken))
        {
            throw new InvalidOperationException("Downloaded launcher hash verification failed.");
        }

        if (!await HashService.VerifySha256Async(paths.UpdaterTempPath, updaterAsset.Sha256, cancellationToken))
        {
            throw new InvalidOperationException("Downloaded updater hash verification failed.");
        }
    }

    private void LaunchUpdater(UpdateManifest manifest)
    {
        logger.Info("Launching updater.");

        var updaterPath = File.Exists(paths.UpdaterPath) ? paths.UpdaterPath : paths.UpdaterTempPath;
        var launcherProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(updaterPath)!
        };

        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(paths.LauncherPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(paths.LauncherTempPath);
        startInfo.ArgumentList.Add("--updater-target");
        startInfo.ArgumentList.Add(paths.UpdaterPath);
        startInfo.ArgumentList.Add("--updater-source");
        startInfo.ArgumentList.Add(paths.UpdaterTempPath);
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(launcherProcess.Id.ToString());
        startInfo.ArgumentList.Add("--log");
        startInfo.ArgumentList.Add(paths.UpdaterLogPath);
        startInfo.ArgumentList.Add("--restart");

        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add("--restart-arg");
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
    }
}
