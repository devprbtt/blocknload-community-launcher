using System.Diagnostics;
using System.Windows.Forms;
using BnlCommunityFixes.ReplayAnalyzer;
using BnlCommunityFixes.Core.Services;
using BnlCommunityFixes.Core.Updating;

namespace BnlCommunityFixes.Launcher;

internal static class Program
{
    private static void KillOtherLauncherInstances(Logger logger)
    {
        var currentId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("BnlCommunityFixes"))
        {
            if (process.Id == currentId)
                continue;
            try
            {
                process.Kill(entireProcessTree: true);
                logger.Info($"Killed existing launcher process {process.Id}.");
            }
            catch (Exception ex)
            {
                logger.Warning($"Could not kill launcher process {process.Id}: {ex.Message}");
            }
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var paths = new AppPaths();
        paths.EnsureDirectories();
        var logger = new Logger(paths.LauncherLogPath);

        try
        {
            if (args.Length > 0 && string.Equals(args[0], "--analyze-replay", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(ReplayAnalyzerCli.Run(args.Skip(1).ToArray()));
                return;
            }

            if (UpdaterArgumentParser.IsApplyUpdateMode(args))
            {
                var updaterArguments = UpdaterArgumentParser.Parse(args);
                var updaterLogger = new Logger(updaterArguments.LogPath);
                updaterLogger.Info("Launcher update helper starting.");
                var installer = new UpdateInstaller(updaterLogger);
                Environment.Exit(installer.RunAsync(updaterArguments).GetAwaiter().GetResult());
                return;
            }

            ApplicationConfiguration.Initialize();

            var runtimeOptions = LauncherRuntimeOptions.Parse(args);
            KillOtherLauncherInstances(logger);
            logger.Info("Launcher starting.");

            if (runtimeOptions.PortableMode)
            {
                logger.Info("Portable launcher mode enabled; skipping bootstrap install/redirect.");
            }
            else
            {
                var bootstrapper = new AppBootstrapper(paths, logger);
                if (bootstrapper.EnsureInstalledAsync(args).GetAwaiter().GetResult())
                {
                    return;
                }
            }

            var bundledAssetService = new BundledPatchingAssetService(paths, logger);
            bundledAssetService.EnsureAssetsExtracted();
            var featureSettingsService = new FeatureSettingsService(paths);
            if (featureSettingsService.EnsureAutoCasualQueueTestDefaultEnabled())
            {
                logger.Info("Enabled auto casual queue test feature config for launcher 2.3.");
            }
            var debugProfileService = new LauncherDebugProfileService(paths, logger);
            debugProfileService.ApplyCurrentLauncherProfile();

            var settingsService = new SettingsService(paths);
            settingsService.EnsureDefaultFile();
            var settings = settingsService.Load();
            logger.Info($"Using manifest source '{settings.ManifestUrl}'.");

            using var httpClient = new HttpClient();
            if (runtimeOptions.PortableMode)
            {
                logger.Info("Portable launcher mode enabled; skipping update check.");
            }
            else
            {
                var updateCoordinator = new UpdateCoordinator(paths, logger, settings, runtimeOptions, httpClient);
                var updateResult = updateCoordinator.CheckAndApplyIfAcceptedAsync().GetAwaiter().GetResult();
                if (updateResult.ShouldExitForUpdate)
                {
                    logger.Info("Exiting for update.");
                    Environment.Exit(0);
                    return;
                }
            }

            if (runtimeOptions.HeadlessSmokeTest)
            {
                logger.Info("Headless smoke test mode completed without launching UI.");
                return;
            }

            // Prepare runtime services and launch the main UI
            var installService = new BlockNLoadInstallService();
            var installInfo = installService.Detect(settings);

            if (!installInfo.IsDetected)
            {
                var setupForm = new GameDownloadForm(paths, logger, settings, settingsService, httpClient);
                if (setupForm.ShowDialog() != System.Windows.Forms.DialogResult.OK || setupForm.ResultInstallInfo == null)
                {
                    logger.Info("Game setup cancelled by user.");
                    return;
                }
                installInfo = setupForm.ResultInstallInfo;
                settings = settingsService.Load();
            }

            var launcherConfigService = new LauncherConfigService();
            var launcherConfig = launcherConfigService.LoadOrCreate(installInfo, logger);

            Application.Run(new LauncherMainForm(paths, logger, settings, installInfo, launcherConfig, httpClient));
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
