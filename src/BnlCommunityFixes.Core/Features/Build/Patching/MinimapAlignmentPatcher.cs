using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>
/// Makes the minimap texture and its markers use the aspect-correct minimap rectangle.
/// The stock client sizes the minimap correctly, but then derives its scale from the
/// parent container, which diverges on maps whose X/Z ratio does not match that container.
/// </summary>
public sealed class MinimapAlignmentPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__minimap-alignment";

    public void Apply(ExperimentalPatchContext context)
    {
        var transformer = context.TargetModule.Types.FirstOrDefault(
                              static type => type.Name == "GuiMinimapTransformer")
                          ?? throw new InvalidOperationException("GuiMinimapTransformer type not found.");
        var minimap = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "GuiMinimap")
                      ?? throw new InvalidOperationException("GuiMinimap type not found.");
        var method = transformer.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "GetScaleFactor" && candidate.HasBody && candidate.Parameters.Count == 2)
                     ?? throw new InvalidOperationException("GuiMinimapTransformer.GetScaleFactor not found.");
        var minimapRectGetter = minimap.Properties.FirstOrDefault(static property =>
                                   property.Name == "RectTransform")?.GetMethod
                               ?? throw new InvalidOperationException("GuiMinimap.RectTransform getter not found.");

        var replacements = 0;
        var instructions = method.Body.Instructions;
        for (var index = 1; index < instructions.Count; index++)
        {
            if (instructions[index].Operand is not MethodReference called ||
                called.Name != "get_RectTransform" ||
                called.DeclaringType.Name != "GuiMinimapTransformer")
            {
                continue;
            }

            var receiver = instructions[index - 1];
            if (receiver.OpCode != OpCodes.Ldarg_0)
            {
                throw new InvalidOperationException(
                    "Unexpected GuiMinimapTransformer.GetScaleFactor IL before RectTransform access.");
            }

            // GetScaleFactor(GuiMinimap minimap, Vector2 minimapSize): use the resized
            // minimap (argument 1), not this transformer's parent/container rectangle.
            receiver.OpCode = OpCodes.Ldarg_1;
            receiver.Operand = null;
            instructions[index].Operand = context.TargetModule.ImportReference(minimapRectGetter);
            replacements++;
        }

        // The stock method reads width and height for the two candidate scales, then
        // reads height once more when deciding which scale fits the rectangle.
        if (replacements != 3)
        {
            throw new InvalidOperationException(
                $"Expected three minimap scale RectTransform accesses, found {replacements}.");
        }
    }
}
