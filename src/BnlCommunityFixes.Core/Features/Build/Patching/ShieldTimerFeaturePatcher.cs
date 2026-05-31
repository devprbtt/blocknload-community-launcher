using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class ShieldTimerFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "shield-timer";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.ShieldBuffBarRuntime")
            ?? throw new InvalidOperationException("ShieldBuffBarRuntime not found in helper assembly.");

        var attachMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "AttachShieldBuffBar")
            ?? throw new InvalidOperationException("ShieldBuffBarRuntime.AttachShieldBuffBar not found."));

        var healthbarType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbar")
            ?? throw new InvalidOperationException("GuiHealthbar type not found.");

        var startMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("GuiHealthbar.Start not found.");

        var il = startMethod.Body.GetILProcessor();
        var ret = startMethod.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret)
            ?? throw new InvalidOperationException("GuiHealthbar.Start Ret not found.");

        if (startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "AttachShieldBuffBar"))
        {
            return;
        }

        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, attachMethod));
    }
}
