using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class HealAlertsFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "heal-alerts";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.HealAlertRuntime")
            ?? throw new InvalidOperationException("HealAlertRuntime not found in helper assembly.");

        var applyDamageIndicator = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyDamageIndicator")
            ?? throw new InvalidOperationException("HealAlertRuntime.ApplyDamageIndicator not found."));

        var attachBridge = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "AttachHealBridge")
            ?? throw new InvalidOperationException("HealAlertRuntime.AttachHealBridge not found."));

        var makerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHitAlertMaker")
            ?? throw new InvalidOperationException("GuiHitAlertMaker type not found.");

        // Patch OnGlobalUnitDamage(): after each local store of GuiHitAlert / GuiHitAlertOnScreenEdge, call ApplyDamageIndicator
        var onDamageMethod = makerType.Methods.FirstOrDefault(static m => m.Name == "OnGlobalUnitDamage" && m.HasBody)
            ?? throw new InvalidOperationException("GuiHitAlertMaker.OnGlobalUnitDamage not found.");

        PatchDamageMethod(context, onDamageMethod, applyDamageIndicator);

        // Patch Start(): call AttachHealBridge(this) before ret
        var startMethod = makerType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("GuiHitAlertMaker.Start not found.");

        if (!HasHelperCall(startMethod, "AttachHealBridge"))
        {
            var startIl = startMethod.Body.GetILProcessor();
            var startRet = startMethod.Body.Instructions.LastOrDefault(static i => i.OpCode.Code == Code.Ret)
                ?? throw new InvalidOperationException("GuiHitAlertMaker.Start Ret not found.");

            startIl.InsertBefore(startRet, startIl.Create(OpCodes.Ldarg_0));
            startIl.InsertBefore(startRet, startIl.Create(OpCodes.Call, attachBridge));
        }
    }

    private static void PatchDamageMethod(ExperimentalPatchContext context, MethodDefinition method, MethodReference applyDamageIndicator)
    {
        if (HasHelperCall(method, "ApplyDamageIndicator"))
        {
            return;
        }

        var hitAlertVar = method.Body.Variables.FirstOrDefault(static v => v.VariableType.Name == "GuiHitAlert");
        var hitAlertEdgeVar = method.Body.Variables.FirstOrDefault(static v => v.VariableType.Name == "GuiHitAlertOnScreenEdge");

        var instructions = method.Body.Instructions.ToArray();
        var il = method.Body.GetILProcessor();

        if (hitAlertVar is not null)
        {
            var storeInstr = FindStlocForVar(instructions, hitAlertVar);
            if (storeInstr is not null)
            {
                var next = storeInstr.Next;
                il.InsertBefore(next, MakeLdloc(il, hitAlertVar));
                il.InsertBefore(next, il.Create(OpCodes.Call, applyDamageIndicator));
            }
        }

        if (hitAlertEdgeVar is not null)
        {
            var storeInstr = FindStlocForVar(instructions, hitAlertEdgeVar);
            if (storeInstr is not null)
            {
                var next = storeInstr.Next;
                il.InsertBefore(next, MakeLdloc(il, hitAlertEdgeVar));
                il.InsertBefore(next, il.Create(OpCodes.Call, applyDamageIndicator));
            }
        }
    }

    private static Instruction? FindStlocForVar(Instruction[] instructions, VariableDefinition var)
    {
        var idx = var.Index;
        return instructions.FirstOrDefault(i =>
            (i.OpCode.Code == Code.Stloc_0 && idx == 0) ||
            (i.OpCode.Code == Code.Stloc_1 && idx == 1) ||
            (i.OpCode.Code == Code.Stloc_2 && idx == 2) ||
            (i.OpCode.Code == Code.Stloc_3 && idx == 3) ||
            ((i.OpCode.Code == Code.Stloc_S || i.OpCode.Code == Code.Stloc) && i.Operand == var));
    }

    private static Instruction MakeLdloc(ILProcessor il, VariableDefinition var)
    {
        return var.Index switch
        {
            0 => il.Create(OpCodes.Ldloc_0),
            1 => il.Create(OpCodes.Ldloc_1),
            2 => il.Create(OpCodes.Ldloc_2),
            3 => il.Create(OpCodes.Ldloc_3),
            _ => il.Create(OpCodes.Ldloc_S, var)
        };
    }

    private static bool HasHelperCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "HealAlertRuntime");
    }
}
