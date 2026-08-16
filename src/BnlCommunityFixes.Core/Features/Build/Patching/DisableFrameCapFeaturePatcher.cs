using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Removes the 60 FPS cap from the main menu and lobby.</summary>
public sealed class DisableFrameCapFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "fps-unlimiter";

    public void Apply(ExperimentalPatchContext context)
    {
        var sceneManagerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneManager")
            ?? throw new InvalidOperationException("SceneManager type not found.");

        foreach (var methodName in new[] { "ServerLoadMainMenu", "ServerLoadLobby" })
        {
            var method = sceneManagerType.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
            if (method is null) continue;

            // Find ldc.i4.s 60 immediately before the SetTargetFramerate newobj call and mutate to ldc.i4.m1 (-1)
            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count - 1; i++)
            {
                var instr = instructions[i];
                if ((instr.OpCode == OpCodes.Ldc_I4_S || instr.OpCode == OpCodes.Ldc_I4) &&
                    instr.Operand is sbyte or int &&
                    Convert.ToInt32(instr.Operand) == 60)
                {
                    var next = instructions[i + 1];
                    if (next.Operand is MethodReference mr && mr.Name == ".ctor")
                    {
                        instr.OpCode = OpCodes.Ldc_I4_M1;
                        instr.Operand = null;
                        break;
                    }
                }
            }
        }
    }
}
