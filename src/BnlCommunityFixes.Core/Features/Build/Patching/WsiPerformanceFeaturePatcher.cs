using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class WsiPerformanceFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "wsi-performance";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtime = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.WsiPerformanceRuntime")
            ?? throw new InvalidOperationException("WsiPerformanceRuntime not found in helper assembly.");
        var guard = context.TargetModule.ImportReference(runtime.Methods.First(static m => m.Name == "ShouldSkipReconciliation"));
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiWsiTeamOverlay")
            ?? throw new InvalidOperationException("GuiWsiTeamOverlay not found.");
        var update = type.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody && !m.IsStatic)
            ?? throw new InvalidOperationException("GuiWsiTeamOverlay.Update not found.");
        if (update.Body.Instructions.Any(static i => i.Operand is MethodReference mr && mr.DeclaringType.Name == "WsiPerformanceRuntime")) return;
        var il = update.Body.GetILProcessor();
        var first = update.Body.Instructions[0];
        var run = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, guard));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, run));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, run);
    }
}
