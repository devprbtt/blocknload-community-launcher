using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Always-active DPS overlay — unconditional, no config guard needed.</summary>
public sealed class DpsOverlayPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__always__dps-overlay";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.DpsOverlayRuntime");
        if (runtimeType is null) return;

        var ensureInit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInit")
            ?? throw new InvalidOperationException("DpsOverlayRuntime.EnsureInit not found."));

        var tryRecord = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "TryRecordDamage")
            ?? throw new InvalidOperationException("DpsOverlayRuntime.TryRecordDamage not found."));

        // EnsureInit at MainMenu.Start
        InjectEnsureInitAtStart(context, "MainMenu", "Start", ensureInit);

        // EnsureInit at GuiDamageNumberDetector.Start (fallback for scene reloads)
        InjectEnsureInitAtStart(context, "GuiDamageNumberDetector", "Start", ensureInit);

        // TryRecordDamage(arg1) before last ret of OnGlobalUnitDamage
        var detectorType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiDamageNumberDetector")
            ?? throw new InvalidOperationException("GuiDamageNumberDetector not found.");
        var onDamage = detectorType.Methods.FirstOrDefault(static m => m.Name == "OnGlobalUnitDamage" && m.HasBody)
            ?? throw new InvalidOperationException("GuiDamageNumberDetector.OnGlobalUnitDamage not found.");

        if (!HasDpsRuntimeCall(onDamage, "TryRecordDamage"))
        {
            var il = onDamage.Body.GetILProcessor();
            var lastRet = onDamage.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret)
                ?? throw new InvalidOperationException("OnGlobalUnitDamage Ret not found.");

            il.InsertBefore(lastRet, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(lastRet, il.Create(OpCodes.Call, tryRecord));
        }
    }

    private static void InjectEnsureInitAtStart(ExperimentalPatchContext context, string typeName, string methodName, MethodReference ensureInit)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is null) return;
        if (HasDpsRuntimeCall(method, "EnsureInit")) return;
        var il = method.Body.GetILProcessor();
        il.InsertBefore(method.Body.Instructions[0], il.Create(OpCodes.Call, ensureInit));
    }

    private static bool HasDpsRuntimeCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "DpsOverlayRuntime");
    }
}
