using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Skips login intro screen — driven by debug-menu config's skip_intro field.</summary>
public sealed class SkipIntroFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__skip-intro";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-debug-menu-config.json");
        if (!PatcherConfigReader.GetBool(config, "skip_intro")) return;

        var introType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiLoginIntro")
            ?? throw new InvalidOperationException("GuiLoginIntro type not found.");

        var startMethod = introType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("GuiLoginIntro.Start not found.");
        var finishWarning = context.TargetModule.ImportReference(
            introType.Methods.FirstOrDefault(static m => m.Name == "FinishWarning")
            ?? throw new InvalidOperationException("GuiLoginIntro.FinishWarning not found."));
        var finishIntro = context.TargetModule.ImportReference(
            introType.Methods.FirstOrDefault(static m => m.Name == "FinishIntro")
            ?? throw new InvalidOperationException("GuiLoginIntro.FinishIntro not found."));

        var il = startMethod.Body.GetILProcessor();
        var firstRet = startMethod.Body.Instructions.FirstOrDefault(static i => i.OpCode.Code == Code.Ret)
            ?? throw new InvalidOperationException("GuiLoginIntro.Start Ret not found.");

        if (HasHelperCall(startMethod, "FinishWarning") || HasHelperCall(startMethod, "FinishIntro"))
        {
            return;
        }

        il.InsertBefore(firstRet, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstRet, il.Create(OpCodes.Call, finishWarning));
        il.InsertBefore(firstRet, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstRet, il.Create(OpCodes.Call, finishIntro));
    }

    private static bool HasHelperCall(MethodDefinition method, string methodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == methodName);
    }
}
