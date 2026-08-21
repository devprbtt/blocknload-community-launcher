using System.Diagnostics;
using System.Globalization;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class ReloadedClientLauncherService
{
    private readonly AppPaths paths;
    private readonly Logger logger;

    public ReloadedClientLauncherService(AppPaths paths, Logger logger)
    {
        this.paths = paths;
        this.logger = logger;
    }

    public Process Launch(LauncherServer server, GameInstallInfo installInfo)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(installInfo);
        if (string.IsNullOrWhiteSpace(server.Host))
        {
            throw new InvalidOperationException("The selected server has no host.");
        }
        if (server.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("The selected server has an invalid port.");
        }
        if (!File.Exists(paths.ReloadedExecutablePath))
        {
            throw new FileNotFoundException(
                "The BNL Reloaded client has not been downloaded yet.",
                paths.ReloadedExecutablePath);
        }

        var soundBankRoot = Path.Combine(installInfo.GameRoot, "Audio", "GeneratedSoundBanks");
        if (!File.Exists(Path.Combine(soundBankRoot, "Windows", "Init.bnk")))
        {
            throw new DirectoryNotFoundException(
                $"The vanilla Block N Load sound banks were not found: {soundBankRoot}");
        }

        var startInfo = CreateStartInfo(
            paths.ReloadedExecutablePath,
            paths.LogsDir,
            server,
            soundBankRoot,
            installInfo.SteamPath);
        logger.Info($"Starting BNL Reloaded against selected server {server.Host}:{server.Port} (no community patches applied).");
        return Process.Start(startInfo) ?? throw new InvalidOperationException("BNL Reloaded did not start.");
    }

    internal static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string logsDir,
        LauncherServer server,
        string soundBankRoot,
        string steamPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException("BNL Reloaded executable has no parent directory."),
            UseShellExecute = false,
        };
        startInfo.Environment["BNL_WWISE_BANK_ROOT"] = soundBankRoot;
        if (!string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath))
        {
            var currentPath = startInfo.Environment.TryGetValue("PATH", out var value) ? value : null;
            startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(currentPath)
                ? steamPath
                : steamPath + Path.PathSeparator + currentPath;
        }
        startInfo.ArgumentList.Add("-force-d3d11");
        startInfo.ArgumentList.Add("-bnl-local-server");
        startInfo.ArgumentList.Add("-bnl-server-host");
        startInfo.ArgumentList.Add(server.Host.Trim());
        startInfo.ArgumentList.Add("-bnl-server-port");
        startInfo.ArgumentList.Add(server.Port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add(Path.Combine(logsDir, "bnl-reloaded-player.log"));
        return startInfo;
    }
}
