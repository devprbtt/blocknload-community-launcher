using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class UnitGuiScaleFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "unit-gui-scale";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.UnitGuiScaleRuntime")
            ?? throw new InvalidOperationException("UnitGuiScaleRuntime not found in helper assembly.");

        var ensureInit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInit")
            ?? throw new InvalidOperationException("UnitGuiScaleRuntime.EnsureInit not found."));

        var getScaleMultiplier = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetScaleMultiplier")
            ?? throw new InvalidOperationException("UnitGuiScaleRuntime.GetScaleMultiplier not found."));

        var guiFollowType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiFollow")
            ?? throw new InvalidOperationException("GuiFollow type not found.");

        // Patch GuiFollow.UpdateScale — multiply return value by GetScaleMultiplier() before ret
        var updateScale = guiFollowType.Methods.FirstOrDefault(static m => m.Name == "UpdateScale" && m.HasBody)
            ?? throw new InvalidOperationException("GuiFollow.UpdateScale not found.");

        if (!updateScale.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "GetScaleMultiplier"))
        {
            var usIl = updateScale.Body.GetILProcessor();
            var usRet = updateScale.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret)
                ?? throw new InvalidOperationException("GuiFollow.UpdateScale Ret not found.");

            usIl.InsertBefore(usRet, usIl.Create(OpCodes.Call, getScaleMultiplier));
            usIl.InsertBefore(usRet, usIl.Create(OpCodes.Mul));
        }

        // Patch GuiFollow.Update — call EnsureInit() at the top
        var update = guiFollowType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (update is not null &&
            !update.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInit" &&
                mr.DeclaringType.Name == "UnitGuiScaleRuntime"))
        {
            var uIl = update.Body.GetILProcessor();
            uIl.InsertBefore(update.Body.Instructions[0], uIl.Create(OpCodes.Call, ensureInit));
        }
    }
}
