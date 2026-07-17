using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

return Run();

static int Run()
{
    var installRoot = @"C:\Users\Paulo\AppData\Local\BNL-CommunityFixes";
    var gameRoot = @"I:\SteamLibrary\steamapps\common\BlockNLoad";
    var managedDir = Path.Combine(gameRoot, @"Win64\BlockNLoad_Data\Managed");
    var gameDataDir = Path.Combine(gameRoot, @"Win64\BlockNLoad_Data");

    Environment.SetEnvironmentVariable("BNL_INSTALL_ROOT", installRoot);

    var paths = new AppPaths();
    var logger = new Logger(Path.Combine(installRoot, @"logs\rebuild-installed-offline-patch.log"));

    var installInfo = new GameInstallInfo
    {
        IsDetected = true,
        DetectionSource = "manual",
        GameRoot = gameRoot,
        GameExecutablePath = Path.Combine(gameRoot, @"Win64\BlockNLoad.exe"),
        Win64DirectoryPath = Path.Combine(gameRoot, "Win64"),
        GameDataDirectoryPath = gameDataDir,
        ManagedDirectoryPath = managedDir,
        ServersFilePath = Path.Combine(gameRoot, "servers.txt"),
        ReplaysDirectoryPath = Path.Combine(gameDataDir, "bnl-match-replays"),
        CustomAudioDirectoryPath = Path.Combine(gameDataDir, "CustomAudio"),
        CustomMeshesDirectoryPath = Path.Combine(gameDataDir, "CustomMeshes"),
        CustomTexturesDirectoryPath = Path.Combine(gameDataDir, "CustomTextures"),
        IsNoSteamInstall = false
    };

    var buildService = new ExperimentalAssemblyBuildService(paths);
    var gameAssemblyService = new GameAssemblyService(paths);

    Console.WriteLine("Rebuilding offline patch artifacts...");
    if (!buildService.BuildFromLocalConfig(installInfo, logger))
    {
        Console.Error.WriteLine("No enabled local feature config triggered a rebuild.");
        return 2;
    }

    gameAssemblyService.SyncCommunityFixAssembly(installInfo, logger);
    if (!gameAssemblyService.DeployExperimentalAssembly(installInfo, logger))
    {
        Console.Error.WriteLine("Experimental assembly deployment failed.");
        return 3;
    }

    Console.WriteLine("Rebuild and deployment complete.");
    return 0;
}
