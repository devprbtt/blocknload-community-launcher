using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class SteamService
{
    private const string SteamActiveProcessRegistryPath = @"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess";
    private const string SteamAppId = "299360";

    public string? GetReadinessError()
    {
        var steamProcess = Process.GetProcessesByName("steam");
        if (steamProcess.Length == 0)
        {
            return "Steam is not running. Please start Steam.";
        }

        try
        {
            var activeUserValue = Registry.GetValue(SteamActiveProcessRegistryPath, "ActiveUser", null);
            if (activeUserValue is null)
            {
                return "Steam is running but no user is logged in.";
            }

            if (Convert.ToInt32(activeUserValue) == 0)
            {
                return "Steam is running but no user is logged in.";
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
        Process.Start(new ProcessStartInfo
        {
            FileName = "steam://open/main",
            UseShellExecute = true
        });
    }

    public void StartSteamVerify()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://validate/{SteamAppId}",
            UseShellExecute = true
        });
    }

    public void EnsureSteamAppIdFile(GameInstallInfo installInfo)
    {
        var appIdPath = Path.Combine(installInfo.GameRoot, "steam_appid.txt");
        if (!File.Exists(appIdPath))
        {
            File.WriteAllText(appIdPath, SteamAppId + Environment.NewLine);
        }
    }

    public void LaunchGame(GameInstallInfo installInfo)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installInfo.GameExecutablePath,
            WorkingDirectory = installInfo.GameRoot,
            UseShellExecute = true
        });
    }
}
