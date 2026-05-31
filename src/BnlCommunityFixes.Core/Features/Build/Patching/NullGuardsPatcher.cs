using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>
/// Unconditional null-guard patches that suppress per-frame exceptions during replay.
/// Guards: TeamFieldOfView, GuiFollow.Update (Camera.main), UnitGhostHandler.Update (Nullable fields).
/// </summary>
public sealed class NullGuardsPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__always__null-guards";

    public void Apply(ExperimentalPatchContext context)
    {
        var opEq = ResolveUnityOpEquality(context);
        var opImplicit = ResolveUnityOpImplicit(context);

        PatchTeamFieldOfView(context, opEq);
        PatchGuiFollowUpdate(context, opImplicit);
        PatchUnitGhostHandlerUpdate(context);
    }

    private static void PatchTeamFieldOfView(ExperimentalPatchContext context, MethodReference? opEq)
    {
        if (opEq is null) return;
        var tfovType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "TeamFieldOfView");
        if (tfovType is null) return;

        // IsInVisionSector — guard arg1 (viewer) then arg2 (target): if (arg == null) return false;
        var isInVision = tfovType.Methods.FirstOrDefault(static m => m.Name == "IsInVisionSector" && m.HasBody);
        if (isInVision is not null)
        {
            foreach (var argOpCode in new[] { OpCodes.Ldarg_2, OpCodes.Ldarg_1 })
            {
                InjectNullGuardReturnFalse(isInVision, argOpCode, opEq);
            }
        }

        // IsVisibleTroughBlocks — guard arg2 (target)
        var isVisible = tfovType.Methods.FirstOrDefault(static m => m.Name == "IsVisibleTroughBlocks" && m.HasBody);
        if (isVisible is not null)
        {
            InjectNullGuardReturnFalse(isVisible, OpCodes.Ldarg_2, opEq);
        }
    }

    private static void InjectNullGuardReturnFalse(MethodDefinition method, OpCode ldArgOpCode, MethodReference opEq)
    {
        var il = method.Body.GetILProcessor();
        var originalFirst = method.Body.Instructions[0];
        var retFalse = il.Create(OpCodes.Ldc_I4_0);
        var ret = il.Create(OpCodes.Ret);
        var brfalse = il.Create(OpCodes.Brfalse, originalFirst);

        // Insert in reverse order before originalFirst to get correct forward order:
        // ldarg / ldnull / call op_Equality / brfalse originalFirst / ldc.i4.0 / ret
        il.InsertBefore(originalFirst, ret);
        il.InsertBefore(ret, retFalse);
        il.InsertBefore(retFalse, brfalse);
        il.InsertBefore(brfalse, il.Create(OpCodes.Call, opEq));
        il.InsertBefore(brfalse.Previous, il.Create(OpCodes.Ldnull));
        il.InsertBefore(brfalse.Previous, il.Create(ldArgOpCode));
    }

    private static void PatchGuiFollowUpdate(ExperimentalPatchContext context, MethodReference? opImplicit)
    {
        if (opImplicit is null) return;
        var guiFollowType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiFollow");
        var update = guiFollowType?.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (update is null) return;

        var getMainInstr = update.Body.Instructions.FirstOrDefault(static i =>
            i.Operand is MethodReference mr && mr.Name == "get_main");
        if (getMainInstr is null) return;

        var finalRet = update.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret);
        if (finalRet is null) return;

        var il = update.Body.GetILProcessor();
        var getMainRef = context.TargetModule.ImportReference((MethodReference)getMainInstr.Operand);

        // Insert before getMainInstr: call get_main / call op_Implicit / brfalse finalRet
        var brfalse = il.Create(OpCodes.Brfalse, finalRet);
        il.InsertBefore(getMainInstr, il.Create(OpCodes.Call, getMainRef));
        il.InsertBefore(getMainInstr, il.Create(OpCodes.Call, opImplicit));
        il.InsertBefore(getMainInstr, brfalse);
    }

    private static void PatchUnitGhostHandlerUpdate(ExperimentalPatchContext context)
    {
        var ughType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UnitGhostHandler");
        var update = ughType?.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (update is null) return;

        var il = update.Body.GetILProcessor();
        var finalRet = update.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret);
        if (finalRet is null) return;

        // Find get_HasValue reference on Nullable<Single> from existing instructions
        MethodReference? hasValueRef = null;
        foreach (var instr in update.Body.Instructions)
        {
            if (instr.Operand is MethodReference mr &&
                mr.Name == "get_HasValue" &&
                mr.DeclaringType.Name == "Nullable`1")
            {
                hasValueRef = instr.Operand as MethodReference;
                break;
            }
        }
        if (hasValueRef is null) return;

        var hasValue = context.TargetModule.ImportReference(hasValueRef);

        // Find nullable fields (Ldflda on Nullable`1 fields)
        var nullableFields = update.Body.Instructions
            .Where(static i => i.OpCode == OpCodes.Ldflda &&
                               i.Operand is FieldReference fr &&
                               fr.FieldType.Name == "Nullable`1")
            .Select(static i => (FieldReference)i.Operand)
            .Distinct()
            .ToArray();

        if (nullableFields.Length == 0) return;

        // Find insert points: ldloc that immediately follows a brfalse
        var allInstrs = update.Body.Instructions.ToArray();
        var insertPoints = new List<Instruction>();
        for (var i = 1; i < allInstrs.Length; i++)
        {
            var prev = allInstrs[i - 1];
            var curr = allInstrs[i];
            if ((prev.OpCode == OpCodes.Brfalse || prev.OpCode == OpCodes.Brfalse_S) &&
                (curr.OpCode == OpCodes.Ldloc_0 || curr.OpCode == OpCodes.Ldloc_1 ||
                 curr.OpCode == OpCodes.Ldloc_2 || curr.OpCode == OpCodes.Ldloc_3 ||
                 curr.OpCode == OpCodes.Ldloc_S || curr.OpCode == OpCodes.Ldloc))
            {
                insertPoints.Add(curr);
            }
        }

        foreach (var insertPoint in insertPoints)
        {
            // Insert guards in reverse field order so first field guard appears first in IL
            foreach (var field in Enumerable.Reverse(nullableFields))
            {
                var importedField = context.TargetModule.ImportReference(field);
                var brfalse = il.Create(OpCodes.Brfalse, finalRet);
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldflda, importedField));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Call, hasValue));
                il.InsertBefore(insertPoint, brfalse);
            }
        }
    }

    private static MethodReference? ResolveUnityOpEquality(ExperimentalPatchContext context)
    {
        foreach (var type in context.TargetModule.Types)
        {
            foreach (var method in type.Methods.Where(static m => m.HasBody))
            {
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is MethodReference mr &&
                        mr.Name == "op_Equality" &&
                        mr.DeclaringType.FullName == "UnityEngine.Object")
                    {
                        return context.TargetModule.ImportReference(mr);
                    }
                }
            }
        }
        return null;
    }

    private static MethodReference? ResolveUnityOpImplicit(ExperimentalPatchContext context)
    {
        foreach (var type in context.TargetModule.Types)
        {
            foreach (var method in type.Methods.Where(static m => m.HasBody))
            {
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is MethodReference mr &&
                        mr.Name == "op_Implicit" &&
                        mr.DeclaringType.FullName == "UnityEngine.Object")
                    {
                        return context.TargetModule.ImportReference(mr);
                    }
                }
            }
        }
        return null;
    }
}
