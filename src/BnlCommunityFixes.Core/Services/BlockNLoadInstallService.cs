using System.Text.RegularExpressions;
using System.Runtime.Versioning;
using Microsoft.Win32;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class BlockNLoadInstallService
{
    private const string SteamAppId = "299360";
    private const string GameExecutableRelativePath = @"Win64\BlockNLoad.exe";

    public GameInstallInfo Detect(LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.GamePath))
        {
            return BuildFromCandidate(settings.GamePath.Trim(), "settings.gamePath");
        }

        var steamPath = TryGetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            return new GameInstallInfo
            {
                IsDetected = false,
                FailureReason = "Steam installation was not found."
            };
        }

        foreach (var libraryPath in EnumerateSteamLibraryPaths(steamPath))
        {
            var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var installDirName = TryReadInstallDirName(manifestPath);
            if (string.IsNullOrWhiteSpace(installDirName))
            {
                continue;
            }

            var candidateRoot = Path.Combine(libraryPath, "steamapps", "common", installDirName);
            var candidate = BuildFromCandidate(candidateRoot, $"steam:{manifestPath}", steamPath);
            if (candidate.IsDetected)
            {
                return candidate;
            }
        }

        return new GameInstallInfo
        {
            IsDetected = false,
            SteamPath = steamPath,
            FailureReason = $"Block N Load (AppID {SteamAppId}) was not found in Steam libraries."
        };
    }

    private static GameInstallInfo BuildFromCandidate(string candidateRoot, string source, string steamPath = "")
    {
        var fullRoot = Path.GetFullPath(candidateRoot);
        var gameExecutablePath = Path.Combine(fullRoot, GameExecutableRelativePath);
        if (!Directory.Exists(fullRoot))
        {
            return new GameInstallInfo
            {
                IsDetected = false,
                DetectionSource = source,
                SteamPath = steamPath,
                FailureReason = $"Game directory does not exist: {fullRoot}"
            };
        }

        if (!File.Exists(gameExecutablePath))
        {
            return new GameInstallInfo
            {
                IsDetected = false,
                DetectionSource = source,
                SteamPath = steamPath,
                GameRoot = fullRoot,
                FailureReason = $"Game executable was not found: {gameExecutablePath}"
            };
        }

        return new GameInstallInfo
        {
            IsDetected = true,
            DetectionSource = source,
            SteamPath = steamPath,
            GameRoot = fullRoot,
            GameExecutablePath = gameExecutablePath,
            ServersFilePath = Path.Combine(fullRoot, "servers.txt"),
            ManagedDirectoryPath = Path.Combine(fullRoot, @"Win64\BlockNLoad_Data\Managed")
        };
    }

    private static string TryGetSteamInstallPath()
    {
        foreach (var registryPath in new[]
        {
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
            @"HKEY_CURRENT_USER\Software\Valve\Steam"
        })
        {
            var value = Registry.GetValue(registryPath, "InstallPath", null) as string;
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                return Path.GetFullPath(value);
            }
        }

        foreach (var fallback in new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        })
        {
            if (Directory.Exists(Path.Combine(fallback, "steamapps")))
            {
                return fallback;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateSteamLibraryPaths(string steamPath)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = Path.GetFullPath(value);
            if (Directory.Exists(normalized))
            {
                discovered.Add(normalized);
            }
        }

        AddPath(steamPath);

        var libraryVdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryVdfPath))
        {
            return discovered;
        }

        var content = File.ReadAllText(libraryVdfPath);
        foreach (Match match in Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var value = match.Groups[1].Value.Replace(@"\\", @"\");
            AddPath(value);
        }

        return discovered;
    }

    private static string TryReadInstallDirName(string manifestPath)
    {
        var content = File.ReadAllText(manifestPath);
        var match = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
