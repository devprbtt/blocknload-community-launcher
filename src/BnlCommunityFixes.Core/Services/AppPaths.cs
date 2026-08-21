using System;
using System.IO;

namespace BnlCommunityFixes.Core.Services;

public sealed class AppPaths
{
    private const string InstallRootEnvironmentVariable = "BNL_INSTALL_ROOT";
    private readonly string executableExtension;

    public string InstallRoot { get; }
    public string AppDir { get; }
    public string DataDir { get; }
    public string UpdatesDir { get; }
    public string LogsDir { get; }
    public string BackupDir { get; }
    public string ReloadedDir { get; }
    public string ReloadedCurrentDir => Path.Combine(ReloadedDir, "current");
    public string ReloadedExecutablePath => Path.Combine(ReloadedCurrentDir, "BlockNLoad.exe");

    public string LauncherPath => Path.Combine(AppDir, "BnlCommunityFixes" + executableExtension);
    public string UpdaterPath => Path.Combine(AppDir, "BnlUpdater" + executableExtension);
    public string LauncherTempPath => Path.Combine(UpdatesDir, "BnlCommunityFixes.new" + executableExtension);
    public string LauncherUpdateHelperPath => Path.Combine(UpdatesDir, "BnlCommunityFixes.UpdateHelper" + executableExtension);
    public string UpdaterTempPath => Path.Combine(UpdatesDir, "BnlUpdater.new" + executableExtension);
    public string LauncherBackupPath => Path.Combine(BackupDir, "BnlCommunityFixes.previous" + executableExtension);
    public string UpdaterPendingPath => Path.Combine(UpdatesDir, "BnlUpdater.pending" + executableExtension);
    public string LauncherLogPath => Path.Combine(LogsDir, "launcher.log");
    public string UpdaterLogPath => Path.Combine(LogsDir, "updater.log");
    public string BootstrapSourcePath => Path.Combine(DataDir, "bootstrap-source.txt");
    public string PatchingDir => Path.Combine(AppDir, "patching");
    public string PresetsFilePath => Path.Combine(DataDir, "presets.json");

    public AppPaths()
    {
        InstallRoot = ResolveInstallRoot();
        executableExtension = ResolveExecutableExtension();

        AppDir = Path.Combine(InstallRoot, "app");
        DataDir = Path.Combine(InstallRoot, "data");
        UpdatesDir = Path.Combine(InstallRoot, "updates");
        LogsDir = Path.Combine(InstallRoot, "logs");
        BackupDir = Path.Combine(AppDir, "backup");
        ReloadedDir = Path.Combine(InstallRoot, "reloaded");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(UpdatesDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(ReloadedDir);
    }

    private static string ResolveInstallRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(InstallRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BNL-CommunityFixes");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return Path.Combine(Path.GetFullPath(xdgDataHome), "BNL-CommunityFixes");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home, ".local", "share", "BNL-CommunityFixes");
        }

        return Path.Combine(AppContext.BaseDirectory, "BNL-CommunityFixes");
    }

    private static string ResolveExecutableExtension()
    {
        var currentProcessPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(currentProcessPath))
        {
            var extension = Path.GetExtension(currentProcessPath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }
        }

        return OperatingSystem.IsWindows() ? ".exe" : string.Empty;
    }
}
