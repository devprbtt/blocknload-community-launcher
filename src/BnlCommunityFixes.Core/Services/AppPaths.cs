using System;
using System.IO;

namespace BnlCommunityFixes.Core.Services;

public sealed class AppPaths
{
    private const string InstallRootEnvironmentVariable = "BNL_INSTALL_ROOT";

    public string InstallRoot { get; }
    public string AppDir { get; }
    public string DataDir { get; }
    public string UpdatesDir { get; }
    public string LogsDir { get; }
    public string BackupDir { get; }

    public string LauncherPath => Path.Combine(AppDir, "BnlCommunityFixes.exe");
    public string UpdaterPath => Path.Combine(AppDir, "BnlUpdater.exe");
    public string LauncherTempPath => Path.Combine(UpdatesDir, "BnlCommunityFixes.new.exe");
    public string UpdaterTempPath => Path.Combine(UpdatesDir, "BnlUpdater.new.exe");
    public string LauncherBackupPath => Path.Combine(BackupDir, "BnlCommunityFixes.previous.exe");
    public string UpdaterPendingPath => Path.Combine(UpdatesDir, "BnlUpdater.pending.exe");
    public string LauncherLogPath => Path.Combine(LogsDir, "launcher.log");
    public string UpdaterLogPath => Path.Combine(LogsDir, "updater.log");
    public string PatchingDir => Path.Combine(AppDir, "patching");
    public string PresetsFilePath => Path.Combine(DataDir, "presets.json");

    public AppPaths()
    {
        InstallRoot = ResolveInstallRoot();

        AppDir = Path.Combine(InstallRoot, "app");
        DataDir = Path.Combine(InstallRoot, "data");
        UpdatesDir = Path.Combine(InstallRoot, "updates");
        LogsDir = Path.Combine(InstallRoot, "logs");
        BackupDir = Path.Combine(AppDir, "backup");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(UpdatesDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(BackupDir);
    }

    private static string ResolveInstallRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(InstallRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BNL-CommunityFixes");
    }
}
