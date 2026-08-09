using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class PerformanceTelemetryFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "performance-telemetry";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtime = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.PerformanceTelemetryRuntime")
            ?? throw new InvalidOperationException("PerformanceTelemetryRuntime not found in helper assembly.");
        var ensure = context.TargetModule.ImportReference(runtime.Methods.First(static m => m.Name == "EnsureInitialized"));
        var mapWorld = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MapWorld")
            ?? throw new InvalidOperationException("MapWorld type not found.");
        var update = mapWorld.Methods.FirstOrDefault(static m => m.Name == "UpdateRender" && m.HasBody)
            ?? throw new InvalidOperationException("MapWorld.UpdateRender not found.");
        if (update.Body.Instructions.Any(static i => i.Operand is MethodReference m && m.DeclaringType.Name == "PerformanceTelemetryRuntime")) return;
        update.Body.GetILProcessor().InsertBefore(update.Body.Instructions[0], Instruction.Create(OpCodes.Call, ensure));
    }
}
