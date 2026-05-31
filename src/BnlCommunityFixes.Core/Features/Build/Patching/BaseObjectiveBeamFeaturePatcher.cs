using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class BaseObjectiveBeamFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "base-objective-beam";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.BaseObjectiveBeamRuntime")
            ?? throw new InvalidOperationException("BaseObjectiveBeamRuntime not found in helper assembly.");

        var shouldHide = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldHide")
            ?? throw new InvalidOperationException("BaseObjectiveBeamRuntime.ShouldHide not found."));

        var beamType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "BuildingBeamEffect")
            ?? throw new InvalidOperationException("BuildingBeamEffect type not found.");

        var updateMethod = beamType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody)
            ?? throw new InvalidOperationException("BuildingBeamEffect.Update not found.");

        var activeField = beamType.Fields.FirstOrDefault(static f => f.Name == "Active")
            ?? throw new InvalidOperationException("BuildingBeamEffect.Active field not found.");

        var importedActiveField = context.TargetModule.ImportReference(activeField);

        // Find first ldfld Active instruction
        var targetInstr = updateMethod.Body.Instructions.FirstOrDefault(i =>
            i.OpCode.Code == Code.Ldfld && i.Operand is FieldReference fr && fr.Name == "Active")
            ?? throw new InvalidOperationException("Active field load not found in BuildingBeamEffect.Update.");

        var il = updateMethod.Body.GetILProcessor();
        var skipHide = il.Create(OpCodes.Brfalse_S, targetInstr);

        if (updateMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "ShouldHide" &&
                mr.DeclaringType.Name == "BaseObjectiveBeamRuntime"))
        {
            return;
        }

        // if (ShouldHide()) { this.Active = false; } // then fall through to original Active read
        il.InsertBefore(targetInstr, il.Create(OpCodes.Call, shouldHide));
        il.InsertBefore(targetInstr, skipHide);
        il.InsertBefore(targetInstr, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(targetInstr, il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(targetInstr, il.Create(OpCodes.Stfld, importedActiveField));
    }
}
