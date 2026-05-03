using System.Windows.Forms;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var paths = new AppPaths();
        paths.EnsureDirectories();
        var runtimeOptions = LauncherRuntimeOptions.Parse(args);

        var logger = new Logger(paths.LauncherLogPath);
        logger.Info("Launcher starting.");

        try
        {
            var bootstrapper = new AppBootstrapper(paths, logger);
            if (await bootstrapper.EnsureInstalledAsync(args))
            {
                return;
            }

            var settingsService = new SettingsService(paths);
            settingsService.EnsureDefaultFile();
            var settings = settingsService.Load();
            logger.Info($"Using manifest source '{settings.ManifestUrl}'.");

            using var httpClient = new HttpClient();
            var updateCoordinator = new UpdateCoordinator(paths, logger, settings, runtimeOptions, httpClient);
            var updateResult = await updateCoordinator.CheckAndApplyIfAcceptedAsync();
            if (updateResult.ShouldExitForUpdate)
            {
                logger.Info("Exiting for update.");
                return;
            }

            if (runtimeOptions.HeadlessSmokeTest)
            {
                logger.Info("Headless smoke test mode completed without launching UI.");
                return;
            }

            MessageBox.Show(
                "Launcher skeleton is installed and the update path compiled successfully.",
                "BNL Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Launcher startup failed");
            MessageBox.Show(
                exception.ToString(),
                "BNL Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
