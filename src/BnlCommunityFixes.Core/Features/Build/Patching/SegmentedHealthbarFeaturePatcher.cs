using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class SegmentedHealthbarFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "segmented-healthbar";

    public void Apply(ExperimentalPatchContext context)
    {
        var helperTextureType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.TextureReplacementBootstrapper")
            ?? throw new InvalidOperationException("TextureReplacementBootstrapper not found in helper assembly.");

        var ensureInstance = context.TargetModule.ImportReference(
            helperTextureType.Methods.FirstOrDefault(static m => m.Name == "EnsureInstance" && !m.HasParameters)
            ?? throw new InvalidOperationException("TextureReplacementBootstrapper.EnsureInstance not found."));

        var mainMenuType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MainMenu")
            ?? throw new InvalidOperationException("MainMenu type not found.");

        var startMethod = mainMenuType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("MainMenu.Start not found.");

        if (startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInstance" &&
                mr.DeclaringType.Name == "TextureReplacementBootstrapper"))
        {
            return;
        }

        var il = startMethod.Body.GetILProcessor();
        il.InsertBefore(startMethod.Body.Instructions[0], il.Create(OpCodes.Call, ensureInstance));
    }
}
