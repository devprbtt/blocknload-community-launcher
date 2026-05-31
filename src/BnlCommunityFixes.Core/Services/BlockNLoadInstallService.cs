using System.Text.RegularExpressions;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class BlockNLoadInstallService
{
    private const string SteamAppId = "299360";
    private static readonly string GameExecutableRelativePath = Path.Combine("Win64", "BlockNLoad.exe");
    private static readonly string ManagedDirectoryRelativePath = Path.Combine("Win64", "BlockNLoad_Data", "Managed");

    public GameInstallInfo Detect(LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.GamePath))
        {
            return BuildFromCandidate(PlatformPathNormalizer.Normalize(settings.GamePath), "settings.gamePath");
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
        var fullRoot = NormalizeGameRoot(candidateRoot);
        var win64DirectoryPath = Path.Combine(fullRoot, "Win64");
        var gameDataDirectoryPath = Path.Combine(win64DirectoryPath, "BlockNLoad_Data");
        var gameExecutablePath = Path.Combine(fullRoot, GameExecutableRelativePath);
        var managedDirectoryPath = Path.Combine(gameDataDirectoryPath, "Managed");
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
            Win64DirectoryPath = win64DirectoryPath,
            GameDataDirectoryPath = gameDataDirectoryPath,
            GameExecutablePath = gameExecutablePath,
            ServersFilePath = Path.Combine(fullRoot, "servers.txt"),
            ManagedDirectoryPath = managedDirectoryPath,
            ReplaysDirectoryPath = Path.Combine(gameDataDirectoryPath, "bnl-match-replays"),
            CustomAudioDirectoryPath = Path.Combine(gameDataDirectoryPath, "CustomAudio"),
            CustomMeshesDirectoryPath = Path.Combine(gameDataDirectoryPath, "CustomMeshes"),
            CustomTexturesDirectoryPath = Path.Combine(gameDataDirectoryPath, "CustomTextures")
        };
    }

    private static string TryGetSteamInstallPath()
    {
        foreach (var candidate in EnumerateSteamInstallCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Directory.Exists(Path.Combine(candidate, "steamapps")))
            {
                return PlatformPathNormalizer.Normalize(candidate);
            }
        }

        return string.Empty;
    }

    private static string NormalizeGameRoot(string candidatePath)
    {
        var fullPath = PlatformPathNormalizer.Normalize(candidatePath);

        if (File.Exists(fullPath) &&
            string.Equals(Path.GetFileName(fullPath), "BlockNLoad.exe", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(Path.GetDirectoryName(fullPath) ?? fullPath) ?? fullPath;
        }

        if (Directory.Exists(fullPath) &&
            string.Equals(Path.GetFileName(fullPath), "Managed", StringComparison.OrdinalIgnoreCase))
        {
            var dataDir = Path.GetDirectoryName(fullPath);
            var win64Dir = dataDir is null ? null : Path.GetDirectoryName(dataDir);
            return win64Dir is null ? fullPath : Path.GetDirectoryName(win64Dir) ?? fullPath;
        }

        return fullPath;
    }

    private static IEnumerable<string> EnumerateSteamInstallCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (seen.Add(value))
            {
                candidates.Add(value);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            Add(ReadWindowsRegistrySteamPath());
            Add(@"C:\Program Files (x86)\Steam");
            Add(@"C:\Program Files\Steam");
        }

        Add(Environment.GetEnvironmentVariable("STEAM_DIR"));
        Add(Environment.GetEnvironmentVariable("STEAM_ROOT"));
        Add(Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH"));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            Add(Path.Combine(home, ".local", "share", "Steam"));
            Add(Path.Combine(home, ".steam", "steam"));
            Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }

        return candidates;
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

            var normalized = PlatformPathNormalizer.Normalize(value);
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
            // VDF on Windows escapes backslashes as \\; on Linux paths use / with no escaping
            var value = match.Groups[1].Value.Replace(@"\\", @"\");
            AddPath(value);
        }

        return discovered;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? ReadWindowsRegistrySteamPath()
    {
        foreach (var registryPath in new[]
        {
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
            @"HKEY_CURRENT_USER\Software\Valve\Steam"
        })
        {
            if (Microsoft.Win32.Registry.GetValue(registryPath, "InstallPath", null) is string value &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string TryReadInstallDirName(string manifestPath)
    {
        var content = File.ReadAllText(manifestPath);
        var match = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
