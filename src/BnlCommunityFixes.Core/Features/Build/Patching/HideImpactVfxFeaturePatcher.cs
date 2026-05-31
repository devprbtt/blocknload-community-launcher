using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class HideImpactVfxFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "hide-impact-vfx";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.HideImpactVfxRuntime")
            ?? throw new InvalidOperationException("HideImpactVfxRuntime not found in helper assembly.");

        var ensureInit         = context.TargetModule.ImportReference(ImportMethod(runtimeType, "EnsureInit"));
        var shouldHideVfx      = context.TargetModule.ImportReference(ImportMethod(runtimeType, "ShouldHideVfx"));
        var shouldHidePlane    = context.TargetModule.ImportReference(ImportMethod(runtimeType, "ShouldHidePlane"));
        var hidePlane          = context.TargetModule.ImportReference(ImportMethod(runtimeType, "HidePlane"));
        var shouldHideFalling  = context.TargetModule.ImportReference(ImportMethod(runtimeType, "ShouldHideFallingBlocks"));
        var destroyFalling     = context.TargetModule.ImportReference(ImportMethod(runtimeType, "DestroyFallingBlock"));

        // ZoneServiceListener.Impact — inject EnsureInit at top only
        InjectEnsureInitAtStart(context, "ZoneServiceListener", "Impact", ensureInit);

        // GlobalEffects.MakeImpactEffect (all overloads) — if (ShouldHideVfx()) return;
        var globalEffectsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GlobalEffects");
        if (globalEffectsType is not null)
        {
            foreach (var m in globalEffectsType.Methods.Where(static m => m.Name == "MakeImpactEffect" && m.HasBody))
            {
                InjectEarlyReturn(m, shouldHideVfx);
            }
        }

        // MapPlane.Awake — call HidePlane(this) before ret
        var mapPlaneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MapPlane");
        var mapPlaneAwake = mapPlaneType?.Methods.FirstOrDefault(static m => m.Name == "Awake" && m.HasBody);
        if (mapPlaneAwake is not null)
        {
            var il = mapPlaneAwake.Body.GetILProcessor();
            var ret = mapPlaneAwake.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret);
            if (ret is not null)
            {
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Call, hidePlane));
            }
        }

        // Various OnGearToolFire / ShotEffect methods — if (ShouldHideVfx()) return;
        InjectEarlyReturnOnMethod(context, "RolyTankBallCannon",    "OnGearToolFire", shouldHideVfx);
        InjectEarlyReturnOnMethod(context, "RolyTankBallRocketEffect", "OnGearToolFire", shouldHideVfx);
        InjectEarlyReturnOnMethod(context, "GearModelFireEffect",   "ShotEffect",    shouldHideVfx);
        InjectEarlyReturnOnMethod(context, "GearModelShotEffect",   "ShotEffect",    shouldHideVfx);

        // BlockFalling.Create — if (ShouldHideFallingBlocks()) { DestroyFallingBlock(arg0); return; }
        var blockFallingType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "BlockFalling");
        if (blockFallingType is not null)
        {
            var createMethod = blockFallingType.Methods.FirstOrDefault(static m => m.Name == "Create" && m.HasBody);
            if (createMethod is not null)
            {
                var il = createMethod.Body.GetILProcessor();
                var first = createMethod.Body.Instructions[0];
                var cont = il.Create(OpCodes.Nop);

                il.InsertBefore(first, il.Create(OpCodes.Call, shouldHideFalling));
                il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, il.Create(OpCodes.Call, destroyFalling));
                il.InsertBefore(first, il.Create(OpCodes.Ret));
                il.InsertBefore(first, cont);
            }

            // BlockFalling.OnDestroy — if (ShouldHideFallingBlocks()) return;
            InjectEarlyReturnOnMethod(blockFallingType, "OnDestroy", shouldHideFalling);
        }
    }

    private static void InjectEnsureInitAtStart(ExperimentalPatchContext context, string typeName, string methodName, MethodReference ensureInit)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is null) return;

        if (method.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInit" &&
                mr.DeclaringType.Name == "HideImpactVfxRuntime"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        il.InsertBefore(method.Body.Instructions[0], il.Create(OpCodes.Call, ensureInit));
    }

    private static void InjectEarlyReturnOnMethod(ExperimentalPatchContext context, string typeName, string methodName, MethodReference shouldHide)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        InjectEarlyReturnOnMethod(type, methodName, shouldHide);
    }

    private static void InjectEarlyReturnOnMethod(TypeDefinition? type, string methodName, MethodReference shouldHide)
    {
        var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is not null) InjectEarlyReturn(method, shouldHide);
    }

    private static void InjectEarlyReturn(MethodDefinition method, MethodReference shouldHide)
    {
        if (method.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == shouldHide.Name &&
                mr.DeclaringType.Name == "HideImpactVfxRuntime"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var cont = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldHide));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static MethodDefinition ImportMethod(TypeDefinition type, string name) =>
        type.Methods.FirstOrDefault(m => m.Name == name)
        ?? throw new InvalidOperationException($"HideImpactVfxRuntime.{name} not found.");
}
