using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class AutoQueueFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "auto-queue";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AutoCasualQueueRuntime")
            ?? throw new InvalidOperationException("AutoCasualQueueRuntime not found in helper assembly.");

        var ensureInstance = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInstance")
            ?? throw new InvalidOperationException("AutoCasualQueueRuntime.EnsureInstance not found."));

        var mainMenuType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MainMenu")
            ?? throw new InvalidOperationException("MainMenu type not found.");

        var startMethod = mainMenuType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("MainMenu.Start not found.");

        if (!startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInstance" &&
                mr.DeclaringType.Name == "AutoCasualQueueRuntime"))
        {
            var il = startMethod.Body.GetILProcessor();
            il.InsertBefore(startMethod.Body.Instructions[0], il.Create(OpCodes.Call, ensureInstance));
        }

        PatchTimeAssaultMenuOnShow(context, runtimeType);
        PatchTimeAssaultPlayButton(context, runtimeType);
    }

    private static void PatchTimeAssaultMenuOnShow(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var notifyShown = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "NotifyTimeAssaultMenuShown" && !m.HasParameters)
            ?? throw new InvalidOperationException("AutoCasualQueueRuntime.NotifyTimeAssaultMenuShown not found."));

        var uiMenuTimeTrialType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiMenuTimeTrial");
        if (uiMenuTimeTrialType is null)
        {
            return;
        }

        var onShowMethod = uiMenuTimeTrialType.Methods.FirstOrDefault(static m => m.Name == "OnShow" && m.HasBody);
        if (onShowMethod is null)
        {
            return;
        }

        if (onShowMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "NotifyTimeAssaultMenuShown" &&
                mr.DeclaringType.Name == "AutoCasualQueueRuntime"))
        {
            return;
        }

        var il = onShowMethod.Body.GetILProcessor();
        foreach (var ret in onShowMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Call, notifyShown));
        }
    }

    private static void PatchTimeAssaultPlayButton(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var wirePlayButton = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "WireTimeAssaultPlayButton" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("AutoCasualQueueRuntime.WireTimeAssaultPlayButton not found."));

        var uiMenuTimeTrialType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiMenuTimeTrial");
        if (uiMenuTimeTrialType is null)
        {
            return;
        }

        var onFirstShowMethod = uiMenuTimeTrialType.Methods.FirstOrDefault(static m => m.Name == "OnFirstShow" && m.HasBody);
        if (onFirstShowMethod is null)
        {
            return;
        }

        if (onFirstShowMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "WireTimeAssaultPlayButton" &&
                mr.DeclaringType.Name == "AutoCasualQueueRuntime"))
        {
            return;
        }

        var il = onFirstShowMethod.Body.GetILProcessor();
        foreach (var ret in onFirstShowMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, wirePlayButton));
        }
    }
}
