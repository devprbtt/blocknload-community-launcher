using System.Reflection;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class NoSteamInstallService
{
    private const string SteamAppIdForNoSteam = "480";

    private static readonly string[] FixFileNames =
    [
        "steam_api64.dll",
        "rexa64.dll",
        "cream_api.ini"
    ];

    private readonly Assembly resourceAssembly;

    public NoSteamInstallService(Assembly resourceAssembly)
    {
        this.resourceAssembly = resourceAssembly;
    }

    public void ApplyFixFiles(string gameRoot)
    {
        var win64Dir = Path.Combine(gameRoot, "Win64");
        Directory.CreateDirectory(win64Dir);

        foreach (var fileName in FixFileNames)
        {
            var resourceName = $"NoSteam.{fileName}";
            var stream = resourceAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new InvalidOperationException(
                    $"This launcher build does not contain the no-Steam fix files. " +
                    $"Please update the launcher to the latest version and try again.");
            }

            using (stream)
            {
                var destPath = Path.Combine(win64Dir, fileName);
                using var output = File.Create(destPath);
                stream.CopyTo(output);
            }
        }

        var appIdPath = Path.Combine(gameRoot, "steam_appid.txt");
        File.WriteAllText(appIdPath, SteamAppIdForNoSteam + Environment.NewLine);
    }

    public bool IsFixApplied(string gameRoot)
    {
        foreach (var fileName in FixFileNames)
        {
            if (!File.Exists(Path.Combine(gameRoot, "Win64", fileName)))
                return false;
        }
        return true;
    }
}
