using System.Diagnostics;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Updater;

public sealed class UpdateInstaller
{
    private readonly Logger logger;

    public UpdateInstaller(Logger logger)
    {
        this.logger = logger;
    }

    public async Task<int> RunAsync(UpdaterArguments arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            await WaitForProcessExitAsync(arguments.ProcessId, cancellationToken);
            InstallLauncher(arguments);
            InstallExternalLauncher(arguments);
            StageUpdater(arguments);

            if (arguments.Restart)
            {
                RestartLauncher(arguments.TargetPath, arguments.RestartArguments);
            }

            logger.Info("Updater completed successfully.");
            return 0;
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Updater failed");
            TryRollback(arguments);
            return 1;
        }
    }

    private async Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            logger.Info($"Waiting for process {processId} to exit.");
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
            logger.Warning($"Process {processId} was already gone.");
        }
    }

    private void InstallLauncher(UpdaterArguments arguments)
    {
        if (!File.Exists(arguments.SourcePath))
        {
            throw new FileNotFoundException("Downloaded launcher update was not found.", arguments.SourcePath);
        }

        var backupPath = GetBackupPath(arguments.TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(arguments.TargetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        if (File.Exists(arguments.TargetPath))
        {
            File.Move(arguments.TargetPath, backupPath);
        }

        File.Move(arguments.SourcePath, arguments.TargetPath, true);
        logger.Info($"Installed launcher update to {arguments.TargetPath}.");
    }

    private void InstallExternalLauncher(UpdaterArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.ExternalTargetPath))
        {
            return;
        }

        try
        {
            var externalTarget = Path.GetFullPath(arguments.ExternalTargetPath);
            var installedTarget = Path.GetFullPath(arguments.TargetPath);
            var pathComparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (string.Equals(externalTarget, installedTarget, pathComparison) ||
                !File.Exists(externalTarget) ||
                !File.Exists(installedTarget))
            {
                return;
            }

            var installedVersion = ExeVersionReader.GetVersion(installedTarget);
            var externalVersion = ExeVersionReader.GetVersion(externalTarget);
            if (!VersionService.IsRemoteNewer(externalVersion ?? "0.0.0", installedVersion ?? "0.0.0"))
            {
                return;
            }

            CopyWithRetry(installedTarget, externalTarget);
            logger.Info($"Updated external launcher copy at {externalTarget}.");
        }
        catch (Exception exception)
        {
            logger.Warning($"Could not update external launcher copy '{arguments.ExternalTargetPath}': {exception.Message}");
        }
    }

    private void StageUpdater(UpdaterArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.UpdaterTargetPath) ||
            string.IsNullOrWhiteSpace(arguments.UpdaterSourcePath) ||
            !File.Exists(arguments.UpdaterSourcePath))
        {
            return;
        }

        var pendingPath = arguments.UpdaterTargetPath + ".pending";
        File.Copy(arguments.UpdaterSourcePath, pendingPath, true);
        logger.Info($"Staged updater replacement at {pendingPath}.");
    }

    private void RestartLauncher(string targetPath, IReadOnlyList<string> restartArguments)
    {
        logger.Info($"Restarting launcher at {targetPath}.");
        EnsureExecutable(targetPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = OperatingSystem.IsWindows(),
            WorkingDirectory = Path.GetDirectoryName(targetPath)!
        };

        foreach (var argument in restartArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
    }

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path,
                File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    private void TryRollback(UpdaterArguments arguments)
    {
        try
        {
            var backupPath = GetBackupPath(arguments.TargetPath);
            if (File.Exists(backupPath))
            {
                if (File.Exists(arguments.TargetPath))
                {
                    File.Delete(arguments.TargetPath);
                }

                File.Move(backupPath, arguments.TargetPath, true);
                logger.Warning("Launcher rollback completed.");
            }
        }
        catch (Exception rollbackException)
        {
            logger.Exception(rollbackException, "Rollback failed");
        }
    }

    private static string GetBackupPath(string targetPath)
    {
        var directory = Path.Combine(Path.GetDirectoryName(targetPath)!, "backup");
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        return Path.Combine(directory, $"{fileName}.previous{extension}");
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
