using System.Text.Json.Serialization;
using System.Runtime.InteropServices;

namespace BnlCommunityFixes.Core.Models;

public sealed class UpdateManifest
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("minimum_supported_version")]
    public string MinimumSupportedVersion { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("assets")]
    public Dictionary<string, UpdateAsset> Assets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResolvePreferredLauncherAssetKey()
    {
        foreach (var key in GetPreferredLauncherAssetKeys())
        {
            if (Assets.ContainsKey(key))
            {
                return key;
            }
        }

        throw new InvalidOperationException("Manifest does not contain a compatible launcher asset for this platform.");
    }

    public UpdateAsset ResolvePreferredLauncherAsset()
    {
        return Assets[ResolvePreferredLauncherAssetKey()];
    }

    private static IEnumerable<string> GetPreferredLauncherAssetKeys()
    {
        var ridPlatform = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsMacOS() ? "osx"
            : string.Empty;

        var ridArch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(ridPlatform) && !string.IsNullOrWhiteSpace(ridArch))
        {
            yield return $"launcher_{ridPlatform}_{ridArch}";
        }

        if (!string.IsNullOrWhiteSpace(ridPlatform))
        {
            yield return $"launcher_{ridPlatform}";
        }

        yield return "launcher_exe";
    }
}
