using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class FriendlyLowHealthFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "friendly-low-health";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.FriendlyLowHealthRuntime")
            ?? throw new InvalidOperationException("FriendlyLowHealthRuntime not found in helper assembly.");

        var isFriendlyLowHealth = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "IsFriendlyLowHealth")
            ?? throw new InvalidOperationException("FriendlyLowHealthRuntime.IsFriendlyLowHealth not found."));

        var healthbarType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbar")
            ?? throw new InvalidOperationException("GuiHealthbar type not found.");

        var showTimeField = context.TargetModule.ImportReference(
            healthbarType.Fields.FirstOrDefault(static f => f.Name == "showTime")
            ?? throw new InvalidOperationException("GuiHealthbar.showTime field not found."));

        // Patch IsUnitAvailableForShow(): if (IsFriendlyLowHealth(this)) return true;
        var availMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "IsUnitAvailableForShow" && m.HasBody);
        if (availMethod is not null &&
            !availMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "IsFriendlyLowHealth"))
        {
            var il = availMethod.Body.GetILProcessor();
            var first = availMethod.Body.Instructions[0];
            var branch = il.Create(OpCodes.Brfalse_S, first);

            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, isFriendlyLowHealth));
            il.InsertBefore(first, branch);
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(first, il.Create(OpCodes.Ret));
        }

        // Patch AlphaUpdate(): if (IsFriendlyLowHealth(this)) { showTime = 1f; }
        var alphaMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "AlphaUpdate" && m.HasBody);
        if (alphaMethod is not null &&
            !alphaMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "IsFriendlyLowHealth"))
        {
            var il = alphaMethod.Body.GetILProcessor();
            var first = alphaMethod.Body.Instructions[0];
            var branch = il.Create(OpCodes.Brfalse_S, first);

            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, isFriendlyLowHealth));
            il.InsertBefore(first, branch);
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, 1f));
            il.InsertBefore(first, il.Create(OpCodes.Stfld, showTimeField));
        }
    }
}
