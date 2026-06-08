using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class PerformanceOptPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "performance-opt";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-performance-opt-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false))
        {
            return;
        }

        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static t => t.FullName == "BnlCommunityFixes.PerformanceOptRuntime");
        if (runtimeType is null)
        {
            return;
        }

        var getActiveHealthbarMakers = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetActiveHealthbarMakers"));
        if (getActiveHealthbarMakers is null)
        {
            return;
        }

        var registerHealthbarMaker = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "RegisterHealthbarMaker"));
        if (registerHealthbarMaker is null)
        {
            return;
        }

        var shouldSkipUpdate = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipUpdate"));
        if (shouldSkipUpdate is null)
        {
            return;
        }

        var shouldDisableMinimapObjectPopulation = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableMinimapObjectPopulation"));
        var shouldDisableZoneNotifications = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableZoneNotifications"));
        var shouldDisableSupplyTimer = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableSupplyTimer"));
        var shouldDisableZoneMusic = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableZoneMusic"));
        var shouldDisableFactoryColors = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableFactoryColors"));
        var shouldDisableGravityTrapEffects = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableGravityTrapEffects"));
        var shouldDisableForcefieldShieldEffects = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableForcefieldShieldEffects"));
        var shouldDisableFanAnimators = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableFanAnimators"));
        var shouldDisableTeslaLogic = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableTeslaLogic"));
        var shouldDisableMinimapRoot = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableMinimapRoot"));
        var shouldDisablePortalUpdate = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisablePortalUpdate"));
        var shouldDisableTurretModelAnimations = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableTurretModelAnimations"));
        var shouldDisableMortarModelAnimations = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableMortarModelAnimations"));
        var shouldDisableWsiFactory = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldDisableWsiFactory"));

        var shouldThrottleMinimapUpdate = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldThrottleMinimapUpdate"));
        var shouldThrottleGravityTrapEffects = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldThrottleGravityTrapEffects"));
        var shouldThrottleFanAnimator = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldThrottleFanAnimator"));
        var shouldThrottleWsiTeamOverlay = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldThrottleWsiTeamOverlay"));

        PatchGuiHealthbarPopulation(context, getActiveHealthbarMakers, shouldSkipUpdate);
        PatchGuiHealthBarMakerStart(context, registerHealthbarMaker);
        PatchVoidMethodEarlyReturn(context, "GuiMinimapObjectPopulation", "Update", shouldThrottleMinimapUpdate);
        PatchVoidMethodEarlyReturn(context, "GuiMinimapObjectPopulation", "Update", shouldDisableMinimapObjectPopulation);
        PatchVoidMethodEarlyReturn(context, "GravityTrapEffects", "Update", shouldThrottleGravityTrapEffects);
        PatchVoidMethodEarlyReturn(context, "GravityTrapEffects", "Update", shouldDisableGravityTrapEffects);
        PatchVoidMethodEarlyReturn(context, "FanAnimator", "Update", shouldThrottleFanAnimator);
        PatchVoidMethodEarlyReturn(context, "FanAnimator", "Update", shouldDisableFanAnimators);
        PatchVoidMethodEarlyReturn(context, "GuiWsiTeamOverlay", "Update", shouldThrottleWsiTeamOverlay);
        PatchVoidMethodEarlyReturn(context, "ZoneNotifications", "Update", shouldDisableZoneNotifications);
        PatchVoidMethodEarlyReturn(context, "GuiSupplyTimer", "Update", shouldDisableSupplyTimer);
        PatchVoidMethodEarlyReturn(context, "ZoneMusic", "Update", shouldDisableZoneMusic);
        PatchVoidMethodEarlyReturn(context, "FactoryColors", "Update", shouldDisableFactoryColors);
        PatchVoidMethodEarlyReturn(context, "GravityTrapEffects", "Update", shouldDisableGravityTrapEffects);
        PatchVoidMethodEarlyReturn(context, "ForcefieldShieldEffect", "Update", shouldDisableForcefieldShieldEffects);
        PatchVoidMethodEarlyReturn(context, "FanAnimator", "Update", shouldDisableFanAnimators);
        PatchVoidMethodEarlyReturn(context, "TeslaLogic", "Update", shouldDisableTeslaLogic);
        PatchVoidMethodEarlyReturn(context, "GuiMinimap", "Update", shouldDisableMinimapRoot);
        PatchVoidMethodEarlyReturn(context, "KiraPortal", "Update", shouldDisablePortalUpdate);
        PatchVoidMethodEarlyReturn(context, "TurretModelAnimations", "LateUpdate", shouldDisableTurretModelAnimations);
        PatchVoidMethodEarlyReturn(context, "MortarModelAnimations", "Update", shouldDisableMortarModelAnimations);
        PatchVoidMethodEarlyReturn(context, "GuiWorldSpaceIndicatorFactory", "Update", shouldDisableWsiFactory);
    }

    private static void PatchGuiHealthbarPopulation(ExperimentalPatchContext context, MethodReference getActiveHealthbarMakers, MethodReference shouldSkipUpdate)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbarPopulation");
        if (type is null)
        {
            return;
        }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "Update" && !m.IsStatic && m.HasBody);
        if (method is null || MethodAlreadyCalls(method, "GetActiveHealthbarMakers"))
        {
            return;
        }

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();

        // Inject early-exit guard at the top:
        //   if (ShouldSkipUpdate()) return;
        var firstInstruction = instructions.First();
        var retInstruction = il.Create(OpCodes.Ret);
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, shouldSkipUpdate));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Brfalse, firstInstruction));
        il.InsertBefore(firstInstruction, retInstruction);

        var targets = instructions.Where(static i =>
            i.OpCode == OpCodes.Ldsfld &&
            i.Operand is FieldReference fr &&
            fr.Name == "Units" &&
            fr.DeclaringType.Name == "GuiHealthbarPopulation").ToList();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            il.InsertAfter(target, il.Create(OpCodes.Call, getActiveHealthbarMakers));
        }
    }

    private static bool MethodAlreadyCalls(MethodDefinition method, string helperName) =>
        method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperName &&
            mr.DeclaringType.Name == "PerformanceOptRuntime");

    private static void PatchVoidMethodEarlyReturn(ExperimentalPatchContext context, string typeName, string methodName, MethodReference? guardMethod)
    {
        if (guardMethod is null)
        {
            return;
        }

        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        if (type is null)
        {
            return;
        }

        var method = type.Methods.FirstOrDefault(m =>
            m.Name == methodName &&
            !m.IsStatic &&
            m.HasBody &&
            m.ReturnType.FullName == "System.Void");
        if (method is null || MethodAlreadyCalls(method, guardMethod.Name))
        {
            return;
        }

        var first = method.Body.Instructions.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var continueInstr = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, guardMethod));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueInstr));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueInstr);
    }

    private static void PatchGuiHealthBarMakerStart(ExperimentalPatchContext context, MethodReference registerHealthbarMaker)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthBarMaker");
        if (type is null)
        {
            return;
        }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "Start" && !m.IsStatic && m.HasBody);
        if (method is null || MethodAlreadyCalls(method, "RegisterHealthbarMaker"))
        {
            return;
        }

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        var addCall = instructions.FirstOrDefault(static i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == "Add" &&
            mr.DeclaringType.Name.StartsWith("List", StringComparison.Ordinal));
        if (addCall is null || addCall.Next is null)
        {
            return;
        }

        il.InsertAfter(addCall, il.Create(OpCodes.Ldarg_0));
        il.InsertAfter(addCall.Next, il.Create(OpCodes.Call, registerHealthbarMaker));
    }
}
