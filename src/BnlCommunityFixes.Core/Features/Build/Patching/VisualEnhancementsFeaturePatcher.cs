using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class VisualEnhancementsFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "visual-enhancements";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static type => type.FullName == "BnlCommunityFixes.VisualEnhancementsRuntime")
            ?? throw new InvalidOperationException("VisualEnhancementsRuntime not found.");
        var runtimeMethod = runtimeType.Methods.FirstOrDefault(
            static method => method.Name == "EnsureCameraEffect")
            ?? throw new InvalidOperationException("VisualEnhancementsRuntime.EnsureCameraEffect not found.");
        var cameraType = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "CameraFov");
        var start = cameraType?.Methods.FirstOrDefault(
            static method => method.Name == "Start" && method.HasBody && !method.IsStatic);
        if (start is null) return;

        var importedMethod = context.TargetModule.ImportReference(runtimeMethod);
        var il = start.Body.GetILProcessor();
        foreach (var ret in start.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, importedMethod));
        }
    }
}
