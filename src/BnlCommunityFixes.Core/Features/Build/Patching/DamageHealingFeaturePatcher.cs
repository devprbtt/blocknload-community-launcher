using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class DamageHealingFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "damage-healing";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.CombatNumberRuntime")
            ?? throw new InvalidOperationException("CombatNumberRuntime not found in helper assembly.");

        var shouldShowDamage = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldShowDamageNumber")
            ?? throw new InvalidOperationException("CombatNumberRuntime.ShouldShowDamageNumber not found."));

        var getDamageCollectTime = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetDamageCollectTime")
            ?? throw new InvalidOperationException("CombatNumberRuntime.GetDamageCollectTime not found."));

        var refreshDamageNumber = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "RefreshDamageNumber")
            ?? throw new InvalidOperationException("CombatNumberRuntime.RefreshDamageNumber not found."));

        var attachHealingIndicator = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "AttachHealing")
            ?? throw new InvalidOperationException("CombatNumberRuntime.AttachHealing not found."));

        var detectorType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiDamageNumberDetector")
            ?? throw new InvalidOperationException("GuiDamageNumberDetector type not found.");

        PatchOnGlobalUnitDamage(detectorType, shouldShowDamage);
        PatchUpdate(context, detectorType, getDamageCollectTime, refreshDamageNumber);
        PatchStart(detectorType, attachHealingIndicator);
    }

    private static void PatchOnGlobalUnitDamage(TypeDefinition detectorType, MethodReference shouldShow)
    {
        var method = detectorType.Methods.FirstOrDefault(static m => m.Name == "OnGlobalUnitDamage" && m.HasBody)
            ?? throw new InvalidOperationException("GuiDamageNumberDetector.OnGlobalUnitDamage not found.");

        if (HasHelperCall(method, "ShouldShowDamageNumber"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var continueNop = il.Create(OpCodes.Nop);

        // if (!ShouldShowDamageNumber(arg1)) return;
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldShow));
        il.InsertBefore(first, il.Create(OpCodes.Brtrue_S, continueNop));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueNop);
    }

    private static void PatchUpdate(
        ExperimentalPatchContext context,
        TypeDefinition detectorType,
        MethodReference getDamageCollectTime,
        MethodReference refreshDamageNumber)
    {
        var method = detectorType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody)
            ?? throw new InvalidOperationException("GuiDamageNumberDetector.Update not found.");

        if (HasHelperCall(method, "RefreshDamageNumber") || HasHelperCall(method, "GetDamageCollectTime"))
        {
            return;
        }

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();

        // Find nested types
        var collectedDamageType = detectorType.NestedTypes.FirstOrDefault(static t => t.Name == "CollectedDamage");
        var lastHitInfoType     = detectorType.NestedTypes.FirstOrDefault(static t => t.Name == "LastHitInfo");

        if (collectedDamageType is null || lastHitInfoType is null) return;

        var collectedDamageIsCrit = collectedDamageType.Fields.FirstOrDefault(static f => f.Name == "IsCrit");
        var collectedDamageUnit   = collectedDamageType.Fields.FirstOrDefault(static f => f.Name == "Unit");
        var lastHitHitField        = lastHitInfoType.Fields.FirstOrDefault(static f => f.Name == "Hit");
        var lastHitCollectingTime  = lastHitInfoType.Fields.FirstOrDefault(static f => f.Name == "CollectingTime");

        if (collectedDamageIsCrit is null || collectedDamageUnit is null ||
            lastHitHitField is null || lastHitCollectingTime is null) return;

        // Import the fields into target module
        var importedLastHitHit       = context.TargetModule.ImportReference(lastHitHitField);
        var importedCollectingTime   = context.TargetModule.ImportReference(lastHitCollectingTime);
        var importedCollectedUnit    = context.TargetModule.ImportReference(collectedDamageUnit);
        var importedCollectedIsCrit  = context.TargetModule.ImportReference(collectedDamageIsCrit);

        // Find closure type (contains "Update" and "c__AnonStorey" in name)
        var closureType = detectorType.NestedTypes.FirstOrDefault(static t =>
            t.Name.Contains("Update") && t.Name.Contains("c__AnonStorey"));
        var closureDamageField = closureType?.Fields.FirstOrDefault(static f => f.Name == "d");
        var closureLocal = method.Body.Variables.FirstOrDefault(v =>
            closureType is not null && v.VariableType.FullName == closureType.FullName);

        // Find GuiDamageNumber.get_DamageValue
        var damageNumberType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiDamageNumber");
        var getDamageValue = damageNumberType?.Methods.FirstOrDefault(static m => m.Name == "get_DamageValue");
        var importedGetDamageValue = getDamageValue is not null
            ? context.TargetModule.ImportReference(getDamageValue) : null;

        // Patch: before the stfld CollectingTime, insert a call to GetDamageCollectTime()
        // so the stack becomes: GetDamageCollectTime() instead of the original float literal
        var collectingTimeStore = instructions.FirstOrDefault(i =>
            i.OpCode.Code == Code.Stfld && i.Operand is FieldReference fr && fr.Name == "CollectingTime");

        if (collectingTimeStore is not null)
        {
            il.InsertBefore(collectingTimeStore, il.Create(OpCodes.Call, getDamageCollectTime));
        }

        // Patch: after each set_DamageValue callvirt, call RefreshDamageNumber
        if (closureLocal is null || closureDamageField is null || importedGetDamageValue is null) return;

        var importedClosureDamageField = context.TargetModule.ImportReference(closureDamageField);

        // Find lastHitInfo2 local (the Find result) by tracing back from stfld Hit
        var lastHitHitAssign = instructions.FirstOrDefault(i =>
            i.OpCode.Code == Code.Stfld && i.Operand is FieldReference fr && fr.Name == "Hit");

        VariableDefinition? lastHitLocal = null;
        if (lastHitHitAssign?.Previous?.Previous is Instruction ldloc)
        {
            lastHitLocal = ldloc.OpCode.Code switch
            {
                Code.Ldloc_0 => method.Body.Variables[0],
                Code.Ldloc_1 => method.Body.Variables[1],
                Code.Ldloc_2 => method.Body.Variables[2],
                Code.Ldloc_3 => method.Body.Variables[3],
                Code.Ldloc_S or Code.Ldloc => ldloc.Operand as VariableDefinition,
                _ => null
            };
        }

        if (lastHitLocal is null) return;

        var damageValueSets = instructions
            .Where(static i => i.OpCode.Code == Code.Callvirt &&
                               i.Operand is MethodReference mr && mr.Name == "set_DamageValue")
            .ToArray();

        foreach (var dvSet in damageValueSets)
        {
            // Insert after set_DamageValue (in correct order using InsertBefore on the following instruction):
            // lastHitInfo2.Hit = RefreshDamageNumber(this, lastHitInfo2.Hit, closure.d.Unit, lastHitInfo2.Hit.DamageValue, closure.d.IsCrit)
            var next = dvSet.Next;
            il.InsertBefore(next, MakeLdloc(il, lastHitLocal));
            il.InsertBefore(next, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(next, MakeLdloc(il, lastHitLocal));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedLastHitHit));
            il.InsertBefore(next, il.Create(OpCodes.Ldloc_S, closureLocal));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedClosureDamageField));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedCollectedUnit));
            il.InsertBefore(next, MakeLdloc(il, lastHitLocal));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedLastHitHit));
            il.InsertBefore(next, il.Create(OpCodes.Callvirt, importedGetDamageValue));
            il.InsertBefore(next, il.Create(OpCodes.Ldloc_S, closureLocal));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedClosureDamageField));
            il.InsertBefore(next, il.Create(OpCodes.Ldfld, importedCollectedIsCrit));
            il.InsertBefore(next, il.Create(OpCodes.Call, refreshDamageNumber));
            il.InsertBefore(next, il.Create(OpCodes.Stfld, importedLastHitHit));
        }
    }

    private static void PatchStart(TypeDefinition detectorType, MethodReference attachHealingIndicator)
    {
        var method = detectorType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("GuiDamageNumberDetector.Start not found.");

        if (HasHelperCall(method, "AttachHealing"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var ret = method.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret)
            ?? throw new InvalidOperationException("GuiDamageNumberDetector.Start Ret not found.");

        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, attachHealingIndicator));
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
            mr.DeclaringType.Name == "CombatNumberRuntime");
    }
}
