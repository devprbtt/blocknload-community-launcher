using System.Diagnostics;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class AppBootstrapper
{
    private const int MaxBootstrapSourcesToRefresh = 3;

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
            TryRefreshBootstrapSources(normalizedCurrent);
            return Task.FromResult(false);
        }

        logger.Info($"Bootstrapping launcher from '{normalizedCurrent}' to '{normalizedTarget}'.");
        File.WriteAllText(paths.BootstrapSourcePath, normalizedCurrent);

        // Don't overwrite if the installed version is the same or newer than the source.
        if (File.Exists(normalizedTarget))
        {
            var installedVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(normalizedTarget).FileVersion;
            var sourceVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(normalizedCurrent).FileVersion;
            if (!VersionService.IsRemoteNewer(installedVersion ?? "0.0.0", sourceVersion ?? "0.0.0"))
            {
                logger.Info($"Skipping bootstrapper copy: installed version '{installedVersion}' is same or newer than source '{sourceVersion}'.");
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

        var localReplayAnalyzerSource = Path.Combine(Path.GetDirectoryName(normalizedCurrent)!, "BnlCommunityFixes.ReplayAnalyzer.exe");
        if (File.Exists(localReplayAnalyzerSource) && !string.Equals(Path.GetFullPath(localReplayAnalyzerSource), Path.GetFullPath(paths.ReplayAnalyzerPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(localReplayAnalyzerSource, paths.ReplayAnalyzerPath, true);
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

    private void TryRefreshBootstrapSources(string installedPath)
    {
        foreach (var sourcePath in ReadBootstrapSources())
        {
            TryRefreshBootstrapSource(installedPath, sourcePath);
        }
    }

    private void TryRefreshBootstrapSource(string installedPath, string sourcePath)
    {
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

            CopyWithRetry(installedPath, sourcePath);
            logger.Info($"Refreshed bootstrap source '{sourcePath}' from installed launcher version '{installedVersion}'.");
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not refresh bootstrap source '{sourcePath}': {exception.Message}");
        }
    }

    private IEnumerable<string> ReadBootstrapSources()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var returned = 0;

        foreach (var sourcePath in ReadBootstrapSourcesFromLog().Concat(ReadBootstrapSourceFile()))
        {
            if (!string.IsNullOrWhiteSpace(sourcePath) && seen.Add(sourcePath))
            {
                yield return sourcePath;
                returned++;
                if (returned >= MaxBootstrapSourcesToRefresh)
                {
                    yield break;
                }
            }
        }
    }

    private IEnumerable<string> ReadBootstrapSourceFile()
    {
        if (!File.Exists(paths.BootstrapSourcePath))
        {
            yield break;
        }

        string sourcePath;
        try
        {
            sourcePath = File.ReadAllText(paths.BootstrapSourcePath).Trim();
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not read bootstrap source file: {exception.Message}");
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            yield return sourcePath;
        }
    }

    private IEnumerable<string> ReadBootstrapSourcesFromLog()
    {
        if (!File.Exists(paths.LauncherLogPath))
        {
            yield break;
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
                yield return line[start..end];
            }
        }
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyWithRetry(string sourcePath, string destinationPath)
    {
        const int attempts = 10;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts)
            {
                Thread.Sleep(500);
            }
        }
    }
}
