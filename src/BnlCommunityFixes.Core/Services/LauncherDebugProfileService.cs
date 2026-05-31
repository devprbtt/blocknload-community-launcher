using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class LauncherDebugProfileService
{
    private readonly AppPaths paths;
    private readonly Logger logger;

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
        if (!IsDebugLauncherPath(currentExe))
        {
            return;
        }

        var configPath = Path.Combine(paths.PatchingDir, "experimental-debug-menu-config.json");
        var config = new
        {
            enabled = true,
            debug_menu_key = "F9",
            main_menu_key = "F10",
            lobby_menu_key = "F11",
            zone_menu_key = "F12"
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(configPath, json, new UTF8Encoding(false));
        logger.Info($"Applied debug launcher profile to '{configPath}'.");
    }
}
