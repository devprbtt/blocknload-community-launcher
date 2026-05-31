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

        if (startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInstance" &&
                mr.DeclaringType.Name == "AutoCasualQueueRuntime"))
        {
            return;
        }

        var il = startMethod.Body.GetILProcessor();
        il.InsertBefore(startMethod.Body.Instructions[0], il.Create(OpCodes.Call, ensureInstance));
    }
}
