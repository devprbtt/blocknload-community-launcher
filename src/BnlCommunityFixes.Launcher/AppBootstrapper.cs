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
        var targetFileName = Path.GetFileName(normalizedCurrent);
        var normalizedTarget = Path.GetFullPath(Path.Combine(paths.AppDir, targetFileName));
        if (string.Equals(normalizedCurrent, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            TryRefreshBootstrapSource(normalizedCurrent);
            return Task.FromResult(false);
        }

        logger.Info($"Bootstrapping launcher from '{normalizedCurrent}' to '{normalizedTarget}'.");
        File.WriteAllText(paths.BootstrapSourcePath, normalizedCurrent);

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

    private void TryRefreshBootstrapSource(string installedPath)
    {
        var sourcePath = ReadBootstrapSource();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        try
        {
            sourcePath = Path.GetFullPath(sourcePath);
            if (!File.Exists(sourcePath) ||
                string.Equals(sourcePath, installedPath, StringComparison.OrdinalIgnoreCase) ||
                IsInsideDirectory(sourcePath, paths.AppDir))
            {
                return;
            }

            var installedVersion = FileVersionInfo.GetVersionInfo(installedPath).FileVersion;
            var sourceVersion = FileVersionInfo.GetVersionInfo(sourcePath).FileVersion;
            if (!VersionService.IsRemoteNewer(sourceVersion ?? "0.0.0", installedVersion ?? "0.0.0"))
            {
                return;
            }

            File.Copy(installedPath, sourcePath, true);
            logger.Info($"Refreshed bootstrap source '{sourcePath}' from installed launcher version '{installedVersion}'.");
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not refresh bootstrap source '{sourcePath}': {exception.Message}");
        }
    }

    private string? ReadBootstrapSource()
    {
        try
        {
            if (File.Exists(paths.BootstrapSourcePath))
            {
                var sourcePath = File.ReadAllText(paths.BootstrapSourcePath).Trim();
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    return sourcePath;
                }
            }

            return ReadLastBootstrapSourceFromLog();
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not read bootstrap source: {exception.Message}");
            return null;
        }
    }

    private string? ReadLastBootstrapSourceFromLog()
    {
        if (!File.Exists(paths.LauncherLogPath))
        {
            return null;
        }

        var lines = File.ReadLines(paths.LauncherLogPath).Reverse();
        foreach (var line in lines)
        {
            const string prefix = "Bootstrapping launcher from '";
            var start = line.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += prefix.Length;
            var end = line.IndexOf("' to '", start, StringComparison.Ordinal);
            if (end > start)
            {
                return line[start..end];
            }
        }

        return null;
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
