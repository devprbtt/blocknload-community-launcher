using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class MapRenderFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "map-render";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.MapRenderOverrideRuntime")
            ?? throw new InvalidOperationException("MapRenderOverrideRuntime not found in helper assembly.");

        var ensureInit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInit")
            ?? throw new InvalidOperationException("MapRenderOverrideRuntime.EnsureInit not found."));

        var getRenderOverride = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetRenderOverride")
            ?? throw new InvalidOperationException("MapRenderOverrideRuntime.GetRenderOverride not found."));

        var mapWorldType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MapWorld")
            ?? throw new InvalidOperationException("MapWorld type not found.");

        var updateRender = mapWorldType.Methods.FirstOrDefault(static m => m.Name == "UpdateRender" && m.HasBody)
            ?? throw new InvalidOperationException("MapWorld.UpdateRender not found.");

        if (updateRender.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.DeclaringType.Name == "MapRenderOverrideRuntime"))
        {
            return;
        }

        var il = updateRender.Body.GetILProcessor();

        // Inject EnsureInit() at start
        il.InsertBefore(updateRender.Body.Instructions[0], il.Create(OpCodes.Call, ensureInit));

        // Find the call to SetMapRender and insert GetRenderOverride before it to intercept the string arg
        var setMapRenderCall = updateRender.Body.Instructions.FirstOrDefault(static i =>
            i.OpCode.Code == Code.Call &&
            i.Operand is MethodReference mr && mr.Name == "SetMapRender")
            ?? throw new InvalidOperationException("ZoneBuild.SetMapRender call not found in MapWorld.UpdateRender.");

        il.InsertBefore(setMapRenderCall, il.Create(OpCodes.Call, getRenderOverride));
    }
}
