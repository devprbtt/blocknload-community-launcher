using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class NinjaTurtleSkinFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "ninja-turtle-skin";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static type => type.FullName ==
                "BnlCommunityFixes.NinjaTurtleSkinRuntime")
            ?? throw new InvalidOperationException(
                "NinjaTurtleSkinRuntime not found.");
        var apply = runtimeType.Methods.FirstOrDefault(static method =>
            method.Name == "Apply" && method.Parameters.Count == 1)
            ?? throw new InvalidOperationException(
                "NinjaTurtleSkinRuntime.Apply not found.");
        var importedApply = context.TargetModule.ImportReference(apply);
        var applyFpsPrefab = runtimeType.Methods.FirstOrDefault(static method =>
            method.Name == "ApplyFpsPrefab" && method.Parameters.Count == 1)
            ?? throw new InvalidOperationException(
                "NinjaTurtleSkinRuntime.ApplyFpsPrefab not found.");
        var importedApplyFpsPrefab =
            context.TargetModule.ImportReference(applyFpsPrefab);

        PatchUnitModelCreation(context, importedApply);
        PatchUnitViewAssignment(context, importedApply);
        PatchGearCreation(context, importedApply);
        PatchPlayerPrefabRetrieval(context, importedApplyFpsPrefab);
        PatchRemotePrefabRetrieval(context, importedApplyFpsPrefab);
    }

    private static void PatchUnitModelCreation(
        ExperimentalPatchContext context, MethodReference apply)
    {
        var registry = context.TargetModule.Types.FirstOrDefault(
            static type => type.Name == "UnitsRegistry");
        var method = registry?.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "CreateUnitModel" && candidate.HasBody &&
            candidate.Parameters.Count == 3)
            ?? throw new InvalidOperationException(
                "UnitsRegistry.CreateUnitModel not found.");
        InsertBeforeReturns(method, apply, static (il, ret) =>
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_1));
        });
    }

    private static void PatchUnitViewAssignment(
        ExperimentalPatchContext context, MethodReference apply)
    {
        var unitView = context.TargetModule.Types.FirstOrDefault(
            static type => type.Name == "UnitView");
        var setter = unitView?.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "set_Unit" && candidate.HasBody &&
            candidate.Parameters.Count == 1)
            ?? throw new InvalidOperationException("UnitView.set_Unit not found.");
        InsertBeforeReturns(setter, apply, static (il, ret) =>
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_1));
        });
    }

    private static void PatchGearCreation(
        ExperimentalPatchContext context, MethodReference apply)
    {
        var gearModel = context.TargetModule.Types.FirstOrDefault(
            static type => type.Name == "GearModel");
        var awake = gearModel?.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "Awake" && candidate.HasBody && !candidate.IsStatic)
            ?? throw new InvalidOperationException("GearModel.Awake not found.");
        var getGameObject = ResolveGetGameObject(context)
            ?? throw new InvalidOperationException(
                "UnityEngine.Component.get_gameObject not found.");
        InsertBeforeReturns(awake, apply, (il, ret) =>
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, getGameObject));
        });
    }

    private static void PatchPlayerPrefabRetrieval(
        ExperimentalPatchContext context, MethodReference applyFpsPrefab)
    {
        var playerAsset = context.TargetModule.Types.FirstOrDefault(
            static type => type.Name == "PlayerAsset");
        var getPrefab = playerAsset?.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "GetPrefab" && candidate.HasBody &&
            candidate.Parameters.Count == 1)
            ?? throw new InvalidOperationException(
                "PlayerAsset.GetPrefab not found.");
        var il = getPrefab.Body.GetILProcessor();
        foreach (var ret in getPrefab.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            // Preserve the returned prefab while passing a duplicate to the
            // one-time preparation hook.
            il.InsertBefore(ret, il.Create(OpCodes.Dup));
            il.InsertBefore(ret, il.Create(OpCodes.Call, applyFpsPrefab));
        }
    }

    private static void PatchRemotePrefabRetrieval(
        ExperimentalPatchContext context, MethodReference applyPrefab)
    {
        var unitsAsset = context.TargetModule.Types.FirstOrDefault(
            static type => type.Name == "UnitsAsset");
        var getPrefab = unitsAsset?.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "GetPrefab" && candidate.HasBody &&
            candidate.Parameters.Count == 2)
            ?? throw new InvalidOperationException(
                "UnitsAsset.GetPrefab not found.");
        var il = getPrefab.Body.GetILProcessor();
        foreach (var ret in getPrefab.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Dup));
            il.InsertBefore(ret, il.Create(OpCodes.Call, applyPrefab));
        }
    }

    private static void InsertBeforeReturns(
        MethodDefinition method,
        MethodReference apply,
        Action<ILProcessor, Instruction> loadRoot)
    {
        var il = method.Body.GetILProcessor();
        foreach (var ret in method.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            loadRoot(il, ret);
            il.InsertBefore(ret, il.Create(OpCodes.Call, apply));
        }
    }

    private static MethodReference? ResolveGetGameObject(
        ExperimentalPatchContext context)
    {
        var reference = context.TargetModule.AssemblyReferences.FirstOrDefault(
            static assembly => assembly.Name == "UnityEngine");
        if (reference is null)
            return null;
        var assembly = context.TargetModule.AssemblyResolver.Resolve(reference);
        var component = assembly.MainModule.Types.FirstOrDefault(
            static type => type.FullName == "UnityEngine.Component");
        var getter = component?.Methods.FirstOrDefault(
            static method => method.Name == "get_gameObject");
        return getter is null
            ? null
            : context.TargetModule.ImportReference(getter);
    }
}
