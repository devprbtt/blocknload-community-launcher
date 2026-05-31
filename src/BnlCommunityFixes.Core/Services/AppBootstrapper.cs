using System.Diagnostics;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

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
        // Always converge on the canonical installed launcher path so renamed
        // download copies like "BnlCommunityFixes (1).exe" do not create a
        // parallel stale install inside the app directory.
        var normalizedTarget = Path.GetFullPath(paths.LauncherPath);
        if (string.Equals(normalizedCurrent, normalizedTarget, PathComparison))
        {
            TryRefreshBootstrapSources(normalizedCurrent);
            return Task.FromResult(false);
        }

        logger.Info($"Bootstrapping launcher from '{normalizedCurrent}' to '{normalizedTarget}'.");
        File.WriteAllText(paths.BootstrapSourcePath, normalizedCurrent);

        // Don't overwrite if the installed version is the same or newer than the source.
        if (File.Exists(normalizedTarget))
        {
            var installedVersion = ExeVersionReader.GetVersion(normalizedTarget);
            var sourceVersion = ExeVersionReader.GetVersion(normalizedCurrent);
            var installedParsedVersion = VersionService.Parse(installedVersion);
            var sourceParsedVersion = VersionService.Parse(sourceVersion);
            if (installedParsedVersion > sourceParsedVersion ||
                (installedParsedVersion == sourceParsedVersion && FilesAreEqual(normalizedTarget, normalizedCurrent)))
            {
                logger.Info($"Skipping bootstrapper copy: installed version '{installedVersion}' is same or newer than source '{sourceVersion}'.");
                Process.Start(CreateRestartInfo(normalizedTarget, args));
                return Task.FromResult(true);
            }
        }

        paths.EnsureDirectories();
        File.Copy(normalizedCurrent, normalizedTarget, true);
        EnsureExecutable(normalizedTarget);

        Process.Start(CreateRestartInfo(normalizedTarget, args));
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
                string.Equals(sourcePath, installedPath, PathComparison) ||
                IsInsideDirectory(sourcePath, paths.AppDir))
            {
                return;
            }

            var installedVersion = ExeVersionReader.GetVersion(installedPath);
            var sourceVersion = ExeVersionReader.GetVersion(sourcePath);
            var installedParsedVersion = VersionService.Parse(installedVersion);
            var sourceParsedVersion = VersionService.Parse(sourceVersion);
            if (sourceParsedVersion < installedParsedVersion ||
                (sourceParsedVersion == installedParsedVersion && FilesAreEqual(sourcePath, installedPath)))
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
        var seen = new HashSet<string>(PathComparer);
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
        return fullPath.StartsWith(fullDirectory, PathComparison);
    }

    // On Linux, file paths are case-sensitive; on Windows/macOS they are not.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static ProcessStartInfo CreateRestartInfo(string exePath, string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = OperatingSystem.IsWindows(),
            WorkingDirectory = Path.GetDirectoryName(exePath)!
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Mark the binary as executable on Linux/macOS
            File.SetUnixFileMode(path,
                File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
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

    private static bool FilesAreEqual(string leftPath, string rightPath)
    {
        var left = new FileInfo(leftPath);
        var right = new FileInfo(rightPath);
        if (!left.Exists || !right.Exists || left.Length != right.Length)
        {
            return false;
        }

        return string.Equals(
            HashService.ComputeSha1(leftPath),
            HashService.ComputeSha1(rightPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
