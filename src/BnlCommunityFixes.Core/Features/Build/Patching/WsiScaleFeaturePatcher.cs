using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class WsiScaleFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "wsi-scale";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.WsiScaleRuntime")
            ?? throw new InvalidOperationException("WsiScaleRuntime not found in helper assembly.");

        var ensureInit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInit")
            ?? throw new InvalidOperationException("WsiScaleRuntime.EnsureInit not found."));

        var applyScale = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyScale")
            ?? throw new InvalidOperationException("WsiScaleRuntime.ApplyScale not found."));

        var wsiType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiWorldSpaceIndicator")
            ?? throw new InvalidOperationException("GuiWorldSpaceIndicator type not found.");

        var awakeMethod = wsiType.Methods.FirstOrDefault(static m => m.Name == "Awake" && m.HasBody)
            ?? throw new InvalidOperationException("GuiWorldSpaceIndicator.Awake not found.");

        var il = awakeMethod.Body.GetILProcessor();
        var ret = awakeMethod.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret)
            ?? throw new InvalidOperationException("GuiWorldSpaceIndicator.Awake Ret not found.");

        if (awakeMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "ApplyScale" &&
                mr.DeclaringType.Name == "WsiScaleRuntime"))
        {
            return;
        }

        // Before ret: EnsureInit() then ApplyScale(this)
        il.InsertBefore(ret, il.Create(OpCodes.Call, ensureInit));
        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, applyScale));
    }
}
