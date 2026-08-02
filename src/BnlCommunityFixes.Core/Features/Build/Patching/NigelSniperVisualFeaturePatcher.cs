using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class NigelSniperVisualFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "nigel-sniper-visual";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static type => type.FullName == "BnlCommunityFixes.NigelSniperVisualRuntime")
            ?? throw new InvalidOperationException("NigelSniperVisualRuntime not found.");
        var apply = runtimeType.Methods.FirstOrDefault(static method =>
            method.Name == "Apply" && method.Parameters.Count == 1)
            ?? throw new InvalidOperationException("NigelSniperVisualRuntime.Apply not found.");
        var gearModel = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "GearModel");
        var awake = gearModel?.Methods.FirstOrDefault(
            static method => method.Name == "Awake" && method.HasBody && !method.IsStatic);
        if (awake is null)
            throw new InvalidOperationException("GearModel.Awake not found.");

        var importedApply = context.TargetModule.ImportReference(apply);
        var getGameObject = ResolveGetGameObject(context)
            ?? throw new InvalidOperationException("UnityEngine.Component.get_gameObject not found.");
        var il = awake.Body.GetILProcessor();
        foreach (var ret in awake.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, getGameObject));
            il.InsertBefore(ret, il.Create(OpCodes.Call, importedApply));
        }
    }

    private static MethodReference? ResolveGetGameObject(ExperimentalPatchContext context)
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
        return getter is null ? null : context.TargetModule.ImportReference(getter);
    }
}
