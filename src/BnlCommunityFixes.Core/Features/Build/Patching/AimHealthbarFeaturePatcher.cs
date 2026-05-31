using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class AimHealthbarFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "aim-healthbar";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AimHealthbarRuntime")
            ?? throw new InvalidOperationException("AimHealthbarRuntime not found in helper assembly.");

        var shouldShow = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldShow")
            ?? throw new InvalidOperationException("AimHealthbarRuntime.ShouldShow not found."));

        var healthbarType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbar")
            ?? throw new InvalidOperationException("GuiHealthbar type not found.");

        var unitField = context.TargetModule.ImportReference(
            healthbarType.Fields.FirstOrDefault(static f => f.Name == "unit")
            ?? throw new InvalidOperationException("GuiHealthbar.unit field not found."));

        var showTimeField = context.TargetModule.ImportReference(
            healthbarType.Fields.FirstOrDefault(static f => f.Name == "showTime")
            ?? throw new InvalidOperationException("GuiHealthbar.showTime field not found."));

        // Patch IsUnitAvailableForShow(): if (ShouldShow(unit)) return true;
        var availMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "IsUnitAvailableForShow" && m.HasBody)
            ?? throw new InvalidOperationException("GuiHealthbar.IsUnitAvailableForShow not found.");

        if (!availMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "ShouldShow" &&
                mr.DeclaringType.Name == "AimHealthbarRuntime"))
        {
            var availIl = availMethod.Body.GetILProcessor();
            var firstInstr = availMethod.Body.Instructions[0];
            var branchNotShown = availIl.Create(OpCodes.Brfalse_S, firstInstr);

            availIl.InsertBefore(firstInstr, availIl.Create(OpCodes.Ldarg_0));
            availIl.InsertBefore(firstInstr, availIl.Create(OpCodes.Ldfld, unitField));
            availIl.InsertBefore(firstInstr, availIl.Create(OpCodes.Call, shouldShow));
            availIl.InsertBefore(firstInstr, branchNotShown);
            availIl.InsertBefore(firstInstr, availIl.Create(OpCodes.Ldc_I4_1));
            availIl.InsertBefore(firstInstr, availIl.Create(OpCodes.Ret));
        }

        // Patch AlphaUpdate(): if (ShouldShow(unit)) { showTime = 1f; } then run original logic
        var alphaMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "AlphaUpdate" && m.HasBody)
            ?? throw new InvalidOperationException("GuiHealthbar.AlphaUpdate not found.");

        if (!alphaMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "ShouldShow" &&
                mr.DeclaringType.Name == "AimHealthbarRuntime"))
        {
            var alphaIl = alphaMethod.Body.GetILProcessor();
            var alphaFirst = alphaMethod.Body.Instructions[0];
            var branchOriginal = alphaIl.Create(OpCodes.Brfalse_S, alphaFirst);

            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Ldarg_0));
            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Ldfld, unitField));
            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Call, shouldShow));
            alphaIl.InsertBefore(alphaFirst, branchOriginal);
            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Ldarg_0));
            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Ldc_R4, 1f));
            alphaIl.InsertBefore(alphaFirst, alphaIl.Create(OpCodes.Stfld, showTimeField));
        }
    }
}
