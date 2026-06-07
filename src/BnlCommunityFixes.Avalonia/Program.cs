using System.Diagnostics;
using Avalonia;
using BnlCommunityFixes.Avalonia.ViewModels;
using BnlCommunityFixes.Core.Services;
using BnlCommunityFixes.Core.Updating;
using BnlCommunityFixes.ReplayAnalyzer;

namespace BnlCommunityFixes.Avalonia;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var paths = new AppPaths();
        paths.EnsureDirectories();
        var logger = new Logger(paths.LauncherLogPath);

        try
        {
            // CLI sub-commands (no UI needed)
            if (args.Length > 0 && string.Equals(args[0], "--analyze-replay", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(ReplayAnalyzerCli.Run(args.Skip(1).ToArray()));
                return;
            }

            if (UpdaterArgumentParser.IsApplyUpdateMode(args))
            {
                var updaterArgs = UpdaterArgumentParser.Parse(args);
                var updaterLogger = new Logger(updaterArgs.LogPath);
                updaterLogger.Info("Launcher update helper starting.");
                Environment.Exit(new UpdateInstaller(updaterLogger).RunAsync(updaterArgs).GetAwaiter().GetResult());
                return;
            }

            var runtimeOptions = LauncherRuntimeOptions.Parse(args);
            KillOtherLauncherInstances(logger);
            logger.Info("Launcher starting.");

            if (!runtimeOptions.PortableMode)
            {
                var bootstrapper = new AppBootstrapper(paths, logger);
                if (bootstrapper.EnsureInstalledAsync(args).GetAwaiter().GetResult())
                    return;
            }

            var bundledAssets = new BundledPatchingAssetService(paths, logger);
            bundledAssets.EnsureAssetsExtracted();

            var featureSettingsService = new FeatureSettingsService(paths);
            featureSettingsService.EnsureAutoCasualQueueTestDefaultEnabled();

            new LauncherDebugProfileService(paths, logger).ApplyCurrentLauncherProfile();

            var settingsService = new SettingsService(paths);
            settingsService.EnsureDefaultFile();
            var settings = settingsService.Load();
            logger.Info($"Using manifest source '{settings.ManifestUrl}'.");

            if (runtimeOptions.HeadlessSmokeTest)
            {
                if (!runtimeOptions.PortableMode)
                {
                    using var smokeHttpClient = new System.Net.Http.HttpClient();
                    var smokeUpdateCoordinator = new UpdateCoordinator(paths, logger, settings, runtimeOptions, smokeHttpClient);
                    var updateResult = smokeUpdateCoordinator.CheckAndApplyIfAcceptedAsync().GetAwaiter().GetResult();
                    logger.Info($"Headless smoke test update result: available={updateResult.UpdateAvailable}, forced={updateResult.ForcedUpdate}, exitForUpdate={updateResult.ShouldExitForUpdate}.");
                }

                logger.Info("Headless smoke test mode completed without launching UI.");
                return;
            }

            // Build the main ViewModel and pass update coordinator into the startup context.
            // The update check runs after the MainWindow opens so it has full UI available.
            var installService = new BlockNLoadInstallService();
            var installInfo = installService.Detect(settings);
            // HttpClient lifetime is managed by the app; not disposed here so it stays valid until exit
            var httpClient = new System.Net.Http.HttpClient();

            UpdateCoordinator? updateCoordinator = null;
            if (!runtimeOptions.PortableMode)
            {
                updateCoordinator = new UpdateCoordinator(paths, logger, settings, runtimeOptions, httpClient);
            }

            if (!installInfo.IsDetected)
            {
                App.Startup = new App.StartupContext
                {
                    MainVm = new MainWindowViewModel(paths, logger, settings, installInfo, null, httpClient),
                    GameSetup = new App.GameSetupArgs
                    {
                        Paths = paths,
                        Logger = logger,
                        Settings = settings,
                        SettingsService = settingsService,
                        HttpClient = httpClient,
                        UpdateCoordinator = updateCoordinator
                    }
                };
            }
            else
            {
                var launcherConfigService = new LauncherConfigService();
                var launcherConfig = launcherConfigService.LoadOrCreate(installInfo, logger);
                var mainVm = new MainWindowViewModel(paths, logger, settings, installInfo, launcherConfig, httpClient);
                App.Startup = new App.StartupContext
                {
                    MainVm = mainVm,
                    UpdateCoordinator = updateCoordinator
                };
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.Exception(ex, "Launcher startup failed");
            // Can't show Avalonia dialog before the app starts, just log
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void KillOtherLauncherInstances(Logger logger)
    {
        var currentId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("BnlCommunityFixes"))
        {
            if (process.Id == currentId) continue;
            try { process.Kill(entireProcessTree: true); logger.Info($"Killed existing launcher process {process.Id}."); }
            catch (Exception ex) { logger.Warning($"Could not kill launcher process {process.Id}: {ex.Message}"); }
        }
    }
}
