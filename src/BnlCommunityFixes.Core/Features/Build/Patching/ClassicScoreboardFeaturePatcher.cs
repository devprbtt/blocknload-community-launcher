using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class ClassicScoreboardFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "classic-scoreboard";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-classic-scoreboard-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false))
        {
            return;
        }

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.ClassicScoreboardRuntime")
            ?? throw new InvalidOperationException("ClassicScoreboardRuntime not found in helper assembly.");

        var applyMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyToGuiScores" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("ClassicScoreboardRuntime.ApplyToGuiScores not found."));

        var guiScoresType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiScores")
            ?? throw new InvalidOperationException("GuiScores type not found.");

        var updateMethod = guiScoresType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody)
            ?? throw new InvalidOperationException("GuiScores.Update not found.");

        if (MethodCalls(updateMethod, "ApplyToGuiScores", "ClassicScoreboardRuntime"))
        {
            return;
        }

        var il = updateMethod.Body.GetILProcessor();
        foreach (var ret in updateMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, applyMethod));
        }
    }

    private static bool MethodCalls(MethodDefinition method, string methodName, string declaringTypeName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == methodName &&
            mr.DeclaringType.Name == declaringTypeName);
    }
}
