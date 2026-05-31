using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class TeammateHpFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "teammate-hp";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.TeammateHpRuntime")
            ?? throw new InvalidOperationException("TeammateHpRuntime not found in helper assembly.");

        var updateText = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "UpdateTeammateHpText")
            ?? throw new InvalidOperationException("TeammateHpRuntime.UpdateTeammateHpText not found."));

        var guiTeammateType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiTeammate")
            ?? throw new InvalidOperationException("GuiTeammate type not found.");

        var updateMethod = guiTeammateType.Methods.FirstOrDefault(static m => m.Name == "Update" && !m.IsStatic && m.HasBody)
            ?? throw new InvalidOperationException("GuiTeammate.Update not found.");

        if (updateMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "UpdateTeammateHpText" &&
                mr.DeclaringType.Name == "TeammateHpRuntime"))
        {
            return;
        }

        var il = updateMethod.Body.GetILProcessor();
        var ret = updateMethod.Body.Instructions.LastOrDefault(static i => i.OpCode.Code == Code.Ret)
            ?? throw new InvalidOperationException("GuiTeammate.Update Ret not found.");

        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, updateText));
    }
}
