using System.Diagnostics;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class ExperimentalAssemblyBuildService
{
    private static readonly string[] TriggerConfigFileNames =
    [
        "crosshair-config.json",
        "fov-config.json",
        "experimental-team-color-config.json",
        "damage-healing-indicator-config.json",
        "experimental-font-config.json",
        "heal-alert-indicator-config.json",
        "experimental-base-objective-beam-config.json",
        "experimental-enemy-shield-buffbar-config.json",
        "experimental-match-replay-recorder-config.json",
        "aim-healthbar-config.json",
        "experimental-debug-menu-config.json",
        "experimental-auto-casual-queue-config.json",
        "experimental-local-build-preview-config.json",
        "deathcam-healthbar-config.json",
        "friendly-low-health-config.json",
        "teammate-hp-config.json"
    ];

    private readonly AppPaths paths;

    public ExperimentalAssemblyBuildService(AppPaths paths)
    {
        this.paths = paths;
    }

    public bool WillBuildFromLocalConfig()
    {
        var buildScriptPath = Path.Combine(paths.PatchingDir, "Build-ExperimentalCrosshairAssembly.ps1");
        if (!File.Exists(buildScriptPath))
        {
            return false;
        }

        return TriggerConfigFileNames
            .Select(fileName => Path.Combine(paths.PatchingDir, fileName))
            .Any(File.Exists);
    }

    public bool BuildFromLocalConfig(GameInstallInfo installInfo, Logger logger)
    {
        var buildScriptPath = Path.Combine(paths.PatchingDir, "Build-ExperimentalCrosshairAssembly.ps1");
        if (!File.Exists(buildScriptPath))
        {
            return false;
        }

        var hasAnyConfig = TriggerConfigFileNames
            .Select(fileName => Path.Combine(paths.PatchingDir, fileName))
            .Any(File.Exists);
        if (!hasAnyConfig)
        {
            return false;
        }

        logger.Info("Feature config detected. Rebuilding Assembly-CSharp DLL...");

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = paths.PatchingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(buildScriptPath);
        startInfo.ArgumentList.Add("-GameRoot");
        startInfo.ArgumentList.Add(installInfo.GameRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the feature assembly builder.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        LogProcessLines(output, logger);
        LogProcessLines(error, logger);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Feature bundle Assembly-CSharp build failed.");
        }

        return true;
    }

    private static void LogProcessLines(string content, Logger logger)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        foreach (var rawLine in content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                logger.Info(line);
            }
        }
    }
}
