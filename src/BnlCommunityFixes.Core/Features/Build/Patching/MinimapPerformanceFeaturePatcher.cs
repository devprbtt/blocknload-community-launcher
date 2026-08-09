using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class MinimapPerformanceFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "minimap-performance";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtime = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.MinimapPerformanceRuntime")
            ?? throw new InvalidOperationException("MinimapPerformanceRuntime not found in helper assembly.");
        PatchUpdate(context, "MinimapCamera", context.TargetModule.ImportReference(runtime.Methods.First(static m => m.Name == "ShouldSkipCameraUpdate")));
        PatchUpdate(context, "GuiMinimap", context.TargetModule.ImportReference(runtime.Methods.First(static m => m.Name == "ShouldSkipLayoutUpdate")));
        PatchUpdate(context, "GuiMinimapObjectPopulation", context.TargetModule.ImportReference(runtime.Methods.First(static m => m.Name == "ShouldSkipPopulationUpdate")));
    }

    private static void PatchUpdate(ExperimentalPatchContext context, string typeName, MethodReference guard)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        var method = type?.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody && !m.IsStatic && m.ReturnType.FullName == "System.Void");
        if (method is null || method.Body.Instructions.Any(static i => i.Operand is MethodReference mr && mr.DeclaringType.Name == "MinimapPerformanceRuntime")) return;
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var run = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, guard));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, run));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, run);
    }
}
