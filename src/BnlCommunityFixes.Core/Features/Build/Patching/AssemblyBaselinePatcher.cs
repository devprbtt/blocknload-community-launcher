using Mono.Cecil;
using Mono.Cecil.Cil;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>
/// Creates Assembly-CSharp.experimental.dll from the backup by applying three
/// baseline IL patches that were previously done via raw byte writes in the PS1 script.
/// </summary>
public sealed class AssemblyBaselinePatcher
{
    private const string ExperimentalAssemblyFileName = "Assembly-CSharp.experimental.dll";

    public void CreateExperimentalAssembly(
        string backupAssemblyPath,
        string patchingDir,
        string managedDir,
        Logger logger)
    {
        var outputPath = Path.Combine(patchingDir, ExperimentalAssemblyFileName);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);
        resolver.AddSearchDirectory(patchingDir);

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        using var assembly = AssemblyDefinition.ReadAssembly(backupAssemblyPath, readerParameters);
        var module = assembly.MainModule;

        PatchEacRuntimeCheck(module, logger);
        PatchSteamLoginBranch(module, logger);
        PatchPlayerDataIsNoob(module, logger);

        assembly.Write(outputPath);
        logger.Info($"Created experimental assembly baseline: {outputPath}");
    }

    // Patch 1 (file offset 0x61F49):
    // EACToolInitializer::IsEACRuntime — replace `call EasyAntiCheat.Client.Hydra.Runtime::IsActive()`
    // with `nop; nop; nop; nop; ldc.i4.1` so the method always returns true.
    private static void PatchEacRuntimeCheck(ModuleDefinition module, Logger logger)
    {
        var type = FindType(module, "EACToolInitializer");
        if (type is null) { logger.Info("[Baseline] EACToolInitializer not found — skipping EAC patch"); return; }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "IsEACRuntime");
        if (method is null || !method.HasBody) { logger.Info("[Baseline] EACToolInitializer::IsEACRuntime not found"); return; }

        var il = method.Body.GetILProcessor();
        var callInstr = method.Body.Instructions.FirstOrDefault(static i =>
            i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference mr &&
            mr.DeclaringType.FullName.Contains("EasyAntiCheat") &&
            mr.Name == "IsActive");

        if (callInstr is null) { logger.Info("[Baseline] EACToolInitializer::IsEACRuntime::IsActive call not found — already patched?"); return; }

        // Replace the call with ldc.i4.1 (keeping same stack effect: pushes bool true)
        var ldcTrue = il.Create(OpCodes.Ldc_I4_1);
        il.Replace(callInstr, ldcTrue);
        logger.Info("[Baseline] Patched EACToolInitializer::IsEACRuntime -> always returns true");
    }

    // Patch 2 (file offset 0x15EB80):
    // SteamLogin::<Init>m__641 — invert brfalse to brtrue at IL offset 0x218.
    private static void PatchSteamLoginBranch(ModuleDefinition module, Logger logger)
    {
        var type = FindType(module, "SteamLogin");
        if (type is null) { logger.Info("[Baseline] SteamLogin not found — skipping steam login patch"); return; }

        // The lambda method name may vary; find by IL offset 0x218 containing brfalse
        MethodDefinition? target = null;
        Instruction? targetInstr = null;

        foreach (var method in type.Methods.Where(static m => m.HasBody))
        {
            var instr = method.Body.Instructions.FirstOrDefault(static i =>
                i.Offset == 0x218 && i.OpCode == OpCodes.Brfalse);
            if (instr is not null)
            {
                target = method;
                targetInstr = instr;
                break;
            }
        }

        if (target is null || targetInstr is null)
        {
            logger.Info("[Baseline] SteamLogin brfalse @ IL 0x218 not found — already patched?");
            return;
        }

        var il = target.Body.GetILProcessor();
        var brtrue = il.Create(OpCodes.Brtrue, (Instruction)targetInstr.Operand);
        il.Replace(targetInstr, brtrue);
        logger.Info($"[Baseline] Patched {target.Name}: brfalse -> brtrue @ IL 0x218");
    }

    // Patch 3 (file offset 0x15B585):
    // PlayerData::get_IsNoob — change ldc.i4.6 at IL offset 0x10 to ldc.i4.0.
    private static void PatchPlayerDataIsNoob(ModuleDefinition module, Logger logger)
    {
        var type = FindType(module, "PlayerData");
        if (type is null) { logger.Info("[Baseline] PlayerData not found — skipping IsNoob patch"); return; }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "get_IsNoob" && m.HasBody);
        if (method is null) { logger.Info("[Baseline] PlayerData::get_IsNoob not found"); return; }

        var instr = method.Body.Instructions.FirstOrDefault(static i =>
            i.Offset == 0x10 && i.OpCode == OpCodes.Ldc_I4_6);

        if (instr is null)
        {
            logger.Info("[Baseline] PlayerData::get_IsNoob ldc.i4.6 @ IL 0x10 not found — already patched?");
            return;
        }

        var il = method.Body.GetILProcessor();
        il.Replace(instr, il.Create(OpCodes.Ldc_I4_0));
        logger.Info("[Baseline] Patched PlayerData::get_IsNoob: ldc.i4.6 -> ldc.i4.0");
    }

    private static TypeDefinition? FindType(ModuleDefinition module, string name)
    {
        return module.Types.FirstOrDefault(t => t.Name == name)
            ?? module.Types.SelectMany(static t => t.NestedTypes).FirstOrDefault(t => t.Name == name);
    }
}
