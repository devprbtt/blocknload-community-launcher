using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Classifies PvP feed entries by the killer's team, falling back to the victim for world deaths.</summary>
public sealed class KillFeedTeamPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "kill-feed-team";

    public void Apply(ExperimentalPatchContext context)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == "GuiActivityScroll");
        var method = type?.Methods.FirstOrDefault(m => m.Name == "AddPvp" && m.Parameters.Count == 4 && m.HasBody);
        if (method is null)
            return;

        var getTeam = method.Body.Instructions.FirstOrDefault(i =>
            i.Operand is MethodReference mr && mr.Name == "GetPlayerTeam");
        if (getTeam?.Next is not { } isMy || isMy.Next is not { } storeResult)
            return;

        var firstOriginal = method.Body.Instructions[0];
        var afterOriginal = storeResult.Next;
        if (afterOriginal is null)
            return;

        // Build references from the target's own Nullable<uint> type. Importing the
        // launcher's .NET 8 reflection methods would add System.Private.CoreLib to
        // this Unity/Mono assembly and make every kill notification fail at runtime.
        var nullableUInt = method.Parameters[0].ParameterType;
        var hasValue = new MethodReference("get_HasValue", context.TargetModule.TypeSystem.Boolean, nullableUInt)
        {
            HasThis = true
        };
        var getValue = new MethodReference("get_Value", context.TargetModule.TypeSystem.UInt32, nullableUInt)
        {
            HasThis = true
        };
        var singletonCall = getTeam.Previous?.Previous;
        if (singletonCall?.Operand is not MethodReference singletonGetter ||
            getTeam.Operand is not MethodReference getTeamMethod ||
            isMy.Operand is not MethodReference isMyMethod)
            return;

        var il = method.Body.GetILProcessor();
        var storeFriendly = storeResult.Operand is VariableDefinition variable
            ? il.Create(storeResult.OpCode, variable)
            : il.Create(storeResult.OpCode);
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarga_S, method.Parameters[0]));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Call, hasValue));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Brfalse, firstOriginal));
        il.InsertBefore(firstOriginal, il.Create(singletonCall.OpCode, singletonGetter));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarga_S, method.Parameters[0]));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Call, getValue));
        il.InsertBefore(firstOriginal, il.Create(getTeam.OpCode, getTeamMethod));
        il.InsertBefore(firstOriginal, il.Create(isMy.OpCode, isMyMethod));
        il.InsertBefore(firstOriginal, storeFriendly);
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Br, afterOriginal));
    }
}
