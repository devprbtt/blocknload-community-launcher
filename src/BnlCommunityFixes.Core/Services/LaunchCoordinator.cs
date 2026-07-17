using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class LaunchCoordinator
{
    private readonly Logger logger;
    private readonly ExperimentalAssemblyBuildService experimentalAssemblyBuildService;
    private readonly GameAssemblyService gameAssemblyService;
    private readonly LauncherConfigService launcherConfigService;
    private readonly PatchService patchService;
    private readonly SteamService steamService;
    private readonly FeatureSettingsService featureSettingsService;
    private readonly OfflineBotServerService offlineBotServerService;

    public LaunchCoordinator(AppPaths paths, Logger logger)
    {
        this.logger = logger;
        experimentalAssemblyBuildService = new ExperimentalAssemblyBuildService(paths);
        gameAssemblyService = new GameAssemblyService(paths);
        launcherConfigService = new LauncherConfigService();
        patchService = new PatchService();
        steamService = new SteamService();
        featureSettingsService = new FeatureSettingsService(paths);
        offlineBotServerService = new OfflineBotServerService(paths);
    }

    public void LaunchSelectedServer(GameInstallInfo installInfo, LauncherConfig config)
    {
        if (!installInfo.IsNoSteamInstall)
        {
            logger.Info("Checking Steam status...");
            var steamError = steamService.GetReadinessError();
            if (!string.IsNullOrWhiteSpace(steamError))
            {
                if (steamError.Contains("not running", StringComparison.OrdinalIgnoreCase))
                {
                    steamService.OpenSteamMain();
                }

                throw new InvalidOperationException(steamError);
            }

            var steamWarning = steamService.GetReadinessWarning();
            if (!string.IsNullOrWhiteSpace(steamWarning))
            {
                logger.Warning(steamWarning + " Continuing launch anyway.");
            }
        }
        else
        {
            logger.Info("No-Steam install detected; skipping Steam readiness check.");
        }

        // Bot / Offline Practice Mode: start the embedded bnlReloaded server and route
        // the game to it, regardless of which community server is selected.
        var botModeSettings = featureSettingsService.LoadBotModeSettings();
        var botModeEnabled = botModeSettings.Enabled;
        LauncherServer server;
        if (botModeEnabled)
        {
            var patchKey = "default";
            if (!string.IsNullOrWhiteSpace(config.SelectedServer)
                && config.Servers.TryGetValue(config.SelectedServer, out var selectedServer)
                && !string.IsNullOrWhiteSpace(selectedServer.Patch))
            {
                patchKey = selectedServer.Patch;
            }

            logger.Info("Bot Mode enabled — starting the embedded offline server...");
            offlineBotServerService.EnsureStarted(installInfo, logger, botModeSettings);

            server = new LauncherServer
            {
                Name = "Offline Bot Mode",
                Host = OfflineBotServerService.LocalHost,
                Port = OfflineBotServerService.MasterPort,
                Patch = patchKey,
            };

            logger.Info("Preparing to launch offline bot mode (embedded local server)...");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.SelectedServer))
            {
                throw new InvalidOperationException("No server selected.");
            }

            if (!config.Servers.TryGetValue(config.SelectedServer, out var selected))
            {
                throw new InvalidOperationException("Selected server data is missing from launcher config.");
            }

            server = selected;
            logger.Info($"Preparing to launch {config.SelectedServer}...");
            launcherConfigService.SaveSelection(installInfo, config, config.SelectedServer);
        }

        launcherConfigService.WriteServersFile(installInfo, server);
        logger.Info($"Updated servers.txt to {server.Host}:{server.Port}");

        if (!string.IsNullOrWhiteSpace(server.Patch))
        {
            var skipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var willBuildExperimental = experimentalAssemblyBuildService.WillBuildFromLocalConfig();

            if (willBuildExperimental)
            {
                gameAssemblyService.EnsureAssemblyBackup(installInfo, config, server.Patch, logger);
                logger.Info("Feature Assembly-CSharp mode enabled.");
                skipPaths.Add(@"Win64/BlockNLoad_Data/Managed/Assembly-CSharp.dll");
            }
            else
            {
                gameAssemblyService.ClearExperimentalAssemblyCache(logger);
                if (gameAssemblyService.HasAssemblyBackup(installInfo))
                {
                    gameAssemblyService.RestoreAssemblyBackup(installInfo, logger);
                }
            }

            var builtExperimentalAssembly = experimentalAssemblyBuildService.BuildFromLocalConfig(installInfo, logger);
            if (builtExperimentalAssembly)
            {
                logger.Info("Feature bundle build refreshed from local config.");
            }

            gameAssemblyService.SyncCommunityFixAssembly(installInfo, logger);
            patchService.ApplyPatchSet(installInfo.GameRoot, config, server.Patch, logger, skipPaths);

            if (builtExperimentalAssembly && !gameAssemblyService.DeployExperimentalAssembly(installInfo, logger))
            {
                throw new InvalidOperationException("Feature Assembly-CSharp mode was enabled but the DLL could not be deployed.");
            }
        }
        else
        {
            gameAssemblyService.SyncCommunityFixAssembly(installInfo, logger);
        }

        if (!installInfo.IsNoSteamInstall)
        {
            steamService.EnsureSteamAppIdFile(installInfo);
        }

        logger.Info("Starting BlockNLoad...");
        steamService.LaunchGame(installInfo);
    }

    public void VerifyGameFiles()
    {
        logger.Info("Launching Steam verification...");
        steamService.StartSteamVerify();
    }
}
