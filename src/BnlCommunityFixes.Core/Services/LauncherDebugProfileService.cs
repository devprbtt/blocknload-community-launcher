using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class LauncherDebugProfileService
{
    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly DebugMenuConfigService debugMenuConfigService = new();

    public LauncherDebugProfileService(AppPaths paths, Logger logger)
    {
        this.paths = paths;
        this.logger = logger;
    }

    public static bool IsDebugLauncherPath(string? exePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(exePath ?? string.Empty);
        return fileName.Contains("debug", StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyCurrentLauncherProfile()
    {
        var currentExe = Environment.ProcessPath;
        var configPath = Path.Combine(paths.PatchingDir, "experimental-debug-menu-config.json");
        var isDebugLauncher = IsDebugLauncherPath(currentExe);
        debugMenuConfigService.ApplyLauncherProfile(configPath, isDebugLauncher);
        logger.Info(isDebugLauncher
            ? $"Applied debug launcher profile to '{configPath}'."
            : $"Normalized debug menu profile for non-debug launcher at '{configPath}'.");
    }
}
