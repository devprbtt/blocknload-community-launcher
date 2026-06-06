using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>
/// Eliminates the network round-trip wait in Self and Trigger hero ability coroutines by
/// calling Rpc_CastAbility._Success(true) locally as soon as the RPC is created.
/// UnitEventHelper.HandleAbilityCast (visuals/sound) already fires before the wait, so the
/// player experience is unchanged on accept. Server rejection is silent — the server-side
/// effect won't apply but the client has already shown the cast animation.
/// Only Self and Trigger application types are patched; Hitscan/Projectile/UnitProjectile
/// are left untouched as they have server-validated hit/spawn paths.
/// </summary>
public sealed class AbilityCastFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "ability-cast";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-ability-cast-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false)) return;

        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static t => t.FullName == "BnlCommunityFixes.AbilityCastRuntime");
        if (runtimeType is null) return;

        var tryInstantAccept = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "TryInstantAcceptCastAbility"));
        if (tryInstantAccept is null) return;

        PatchAbilityCastCoroutines(context, tryInstantAccept);
    }

    private static void PatchAbilityCastCoroutines(ExperimentalPatchContext context, MethodReference tryInstantAccept)
    {
        var playerActAbilityType = context.TargetModule.Types.FirstOrDefault(
            static t => t.Name == "PlayerActAbilityUse");
        if (playerActAbilityType is null) return;

        // Patch only ApplictionSelf and TriggerAbility — the safe ones with no server-spawned projectiles
        foreach (var coroutineName in new[] { "ApplictionSelf", "TriggerAbility" })
        {
            var iteratorType = playerActAbilityType.NestedTypes.FirstOrDefault(t =>
                t.Name.Contains(coroutineName) && t.Name.Contains("c__Iterator"));
            if (iteratorType is null) continue;

            var moveNext = iteratorType.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody);
            if (moveNext is null) continue;

            if (IsAlreadyPatched(moveNext)) continue;

            InjectAfterRpcStore(moveNext, iteratorType, tryInstantAccept, context);
        }
    }

    private static void InjectAfterRpcStore(MethodDefinition moveNext, TypeDefinition iteratorType,
        MethodReference tryInstantAccept, ExperimentalPatchContext context)
    {
        // Find the field that stores the Rpc_CastAbility instance
        var rpcField = iteratorType.Fields.FirstOrDefault(static f =>
            f.Name.StartsWith("<rpc>__") &&
            f.FieldType.Name == "Rpc_CastAbility");

        // Also try without the name constraint — some compiler versions use different names
        if (rpcField is null)
            rpcField = iteratorType.Fields.FirstOrDefault(static f =>
                f.FieldType.Name == "Rpc_CastAbility");

        if (rpcField is null) return;

        var rpcFieldRef = context.TargetModule.ImportReference(rpcField);

        // Find the stfld instruction that stores into that field
        var storeInstr = moveNext.Body.Instructions.FirstOrDefault(i =>
            i.OpCode == OpCodes.Stfld &&
            i.Operand is FieldReference fr &&
            fr.Name == rpcField.Name);
        if (storeInstr is null) return;

        var il = moveNext.Body.GetILProcessor();
        var next = storeInstr.Next;

        // Insert after store: ldarg_0 / ldfld <rpc>__ / call TryInstantAcceptCastAbility
        il.InsertBefore(next, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(next, il.Create(OpCodes.Ldfld, rpcFieldRef));
        il.InsertBefore(next, il.Create(OpCodes.Call, tryInstantAccept));
    }

    private static bool IsAlreadyPatched(MethodDefinition method) =>
        method.Body.Instructions.Any(static i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == "TryInstantAcceptCastAbility");
}
