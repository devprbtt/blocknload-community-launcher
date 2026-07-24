using System.Diagnostics;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class SteamService
{
    private const string SteamAppId = "299360";
    private static readonly string[] LinuxUriOpenCandidates =
    [
        "xdg-open",
        "gio",
        "sensible-open",
        "gvfs-open",
        "kde-open",
        "kioclient5",
        "kioclient",
        "exo-open"
    ];

    public string? GetReadinessError()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var steamProcess = Process.GetProcessesByName("steam");
        if (steamProcess.Length == 0 && Process.GetProcessesByName("steamwebhelper").Length == 0)
        {
            return "Steam is not running. Please start Steam.";
        }

        return null;
    }

    public string? GetReadinessWarning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return ReadWindowsSteamActiveUserWarning();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? ReadWindowsSteamActiveUserWarning()
    {
        try
        {
            var activeUserValue = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", "ActiveUser", null);
            if (activeUserValue is null)
            {
                return "Steam is running but its logged-in user could not be verified.";
            }

            if (Convert.ToInt32(activeUserValue) == 0)
            {
                return "Steam is running but no logged-in Steam user was reported.";
            }
        }
        catch
        {
            return "Steam is running but its login state could not be verified.";
        }

        return null;
    }

    public void OpenSteamMain()
    {
        StartUri("steam://open/main");
    }

    public void StartSteamVerify()
    {
        StartUri($"steam://validate/{SteamAppId}");
    }

    public void EnsureSteamAppIdFile(GameInstallInfo installInfo)
    {
        var appIdPath = Path.Combine(installInfo.GameRoot, "steam_appid.txt");
        if (!File.Exists(appIdPath))
        {
            File.WriteAllText(appIdPath, SteamAppId + Environment.NewLine);
        }
    }

    public Process? LaunchGame(GameInstallInfo installInfo)
    {
        if (!OperatingSystem.IsWindows())
        {
            StartUri($"steam://rungameid/{SteamAppId}");
            return null;
        }

        return Process.Start(new ProcessStartInfo
        {
            FileName = installInfo.GameExecutablePath,
            WorkingDirectory = installInfo.GameRoot,
            UseShellExecute = true
        });
    }

    private static void StartUri(string uri)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { uri },
                UseShellExecute = false
            });
            return;
        }

        var opener = ResolveLinuxUriOpener();
        var startInfo = new ProcessStartInfo
        {
            FileName = opener,
            UseShellExecute = false
        };

        foreach (var argument in BuildLinuxUriArguments(opener, uri))
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo);
    }

    private static string ResolveLinuxUriOpener()
    {
        return PlatformCommandResolver.ResolveFromPath(LinuxUriOpenCandidates)
            ?? throw new InvalidOperationException("No supported Linux URI opener was found. Install xdg-open, gio, or an equivalent desktop opener.");
    }

    private static IEnumerable<string> BuildLinuxUriArguments(string opener, string uri)
    {
        var openerName = Path.GetFileName(opener);
        if (string.Equals(openerName, "gio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(openerName, "kioclient5", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(openerName, "kioclient", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "open", uri };
        }

        return new[] { uri };
    }
}
