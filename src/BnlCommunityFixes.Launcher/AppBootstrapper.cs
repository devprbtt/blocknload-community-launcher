using System.Diagnostics;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class AppBootstrapper
{
    private readonly AppPaths paths;
    private readonly Logger logger;

    public AppBootstrapper(AppPaths paths, Logger logger)
    {
        this.paths = paths;
        this.logger = logger;
    }

    public Task<bool> EnsureInstalledAsync(string[] args)
    {
        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve current process path.");

        var normalizedCurrent = Path.GetFullPath(currentExe);
        var normalizedTarget = Path.GetFullPath(paths.LauncherPath);
        if (string.Equals(normalizedCurrent, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        logger.Info($"Bootstrapping launcher from '{normalizedCurrent}' to '{normalizedTarget}'.");

        // Don't overwrite if the installed version is newer than the source
        if (File.Exists(normalizedTarget))
        {
            var installedVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(normalizedTarget).FileVersion;
            var sourceVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(normalizedCurrent).FileVersion;
            if (VersionService.IsRemoteNewer(sourceVersion ?? "0.0.0", installedVersion ?? "0.0.0"))
            {
                logger.Info($"Skipping bootstrapper copy: installed version '{installedVersion}' is newer than source '{sourceVersion}'.");
                var restartInfo = new ProcessStartInfo
                {
                    FileName = normalizedTarget,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(normalizedTarget)!
                };
                foreach (var arg in args) restartInfo.ArgumentList.Add(arg);
                Process.Start(restartInfo);
                return Task.FromResult(true);
            }
        }

        paths.EnsureDirectories();
        File.Copy(normalizedCurrent, normalizedTarget, true);

        var localUpdaterSource = Path.Combine(Path.GetDirectoryName(normalizedCurrent)!, "BnlUpdater.exe");
        if (File.Exists(localUpdaterSource) && !string.Equals(Path.GetFullPath(localUpdaterSource), Path.GetFullPath(paths.UpdaterPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(localUpdaterSource, paths.UpdaterPath, true);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = normalizedTarget,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(normalizedTarget)!
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
        return Task.FromResult(true);
    }
}
