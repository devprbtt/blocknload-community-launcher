using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class GameAssemblyService
{
    private const string AssemblyRelativePath = @"Win64/BlockNLoad_Data/Managed/Assembly-CSharp.dll";

    private readonly AppPaths paths;

    public GameAssemblyService(AppPaths paths)
    {
        this.paths = paths;
    }

    public void SyncCommunityFixAssembly(GameInstallInfo installInfo, Logger logger)
    {
        var localDll = Path.Combine(paths.PatchingDir, "BnlCommunityFixes.dll");
        var targetDll = Path.Combine(installInfo.ManagedDirectoryPath, "BnlCommunityFixes.dll");

        if (!File.Exists(localDll))
        {
            logger.Warning($"Community fix DLL not found at {localDll}");
            return;
        }

        if (!Directory.Exists(installInfo.ManagedDirectoryPath))
        {
            logger.Warning($"Managed folder not found: {installInfo.ManagedDirectoryPath}");
            return;
        }

        File.Copy(localDll, targetDll, true);
        logger.Info("Synced BnlCommunityFixes.dll to managed folder.");
    }

    public bool HasExperimentalAssembly()
    {
        return File.Exists(Path.Combine(paths.PatchingDir, "Assembly-CSharp.experimental.dll"));
    }

    public bool DeployExperimentalAssembly(GameInstallInfo installInfo, Logger logger)
    {
        var localDll = Path.Combine(paths.PatchingDir, "Assembly-CSharp.experimental.dll");
        var targetDll = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp.dll");

        if (!File.Exists(localDll))
        {
            return false;
        }

        if (!Directory.Exists(installInfo.ManagedDirectoryPath))
        {
            logger.Warning($"Managed folder not found: {installInfo.ManagedDirectoryPath}");
            return false;
        }

        File.Copy(localDll, targetDll, true);
        logger.Info("Feature Assembly-CSharp DLL deployed.");
        return true;
    }

    public void EnsureAssemblyBackup(GameInstallInfo installInfo, LauncherConfig config, string patchId, Logger logger)
    {
        var liveDllPath = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp.dll");
        var backupDllPath = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp-backup.dll");
        if (File.Exists(backupDllPath))
        {
            return;
        }

        if (!File.Exists(liveDllPath))
        {
            throw new InvalidOperationException($"Assembly-CSharp.dll not found: {liveDllPath}");
        }

        if (!config.PatchConfigurations.TryGetValue(patchId, out var patchDefinition) ||
            !patchDefinition.Files.TryGetValue(AssemblyRelativePath, out var assemblyPatch))
        {
            throw new InvalidOperationException("Assembly-CSharp patch definition was not found.");
        }

        var liveHash = HashService.ComputeSha1(liveDllPath);
        if (string.Equals(liveHash, assemblyPatch.HashBefore, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(liveDllPath, backupDllPath, true);
            logger.Info("Created Assembly-CSharp-backup.dll automatically.");
            return;
        }

        if (string.Equals(liveHash, assemblyPatch.HashAfter, StringComparison.OrdinalIgnoreCase))
        {
            var liveBytes = File.ReadAllBytes(liveDllPath);
            var cleanBytes = PatchService.RevertPatchedContent(liveBytes, assemblyPatch);
            File.WriteAllBytes(backupDllPath, cleanBytes);
            logger.Info("Reconstructed Assembly-CSharp-backup.dll from known patched state.");
            return;
        }

        throw new InvalidOperationException(
            "Assembly-CSharp-backup.dll is missing and the current Assembly-CSharp.dll is not clean. Please verify game files via Steam or restore the clean DLL first.");
    }
}
