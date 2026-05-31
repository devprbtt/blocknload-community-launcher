using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class AutoCrouchFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "auto-crouch";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AutoCrouchRuntime")
            ?? throw new InvalidOperationException("AutoCrouchRuntime not found in helper assembly.");

        var ensureConfigured = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureConfigured")
            ?? throw new InvalidOperationException("AutoCrouchRuntime.EnsureConfigured not found."));

        var isPossibleToStayWrapper = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "IsPossibleToStayForAutoCrouch")
            ?? throw new InvalidOperationException("AutoCrouchRuntime.IsPossibleToStayForAutoCrouch not found."));

        // 1. Inject EnsureConfigured() at the top of MainMenu.Start
        var mainMenuType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MainMenu")
            ?? throw new InvalidOperationException("MainMenu type not found.");

        var startMethod = mainMenuType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("MainMenu.Start not found.");

        var startIl = startMethod.Body.GetILProcessor();
        startIl.InsertBefore(startMethod.Body.Instructions[0], startIl.Create(OpCodes.Call, ensureConfigured));

        // 2. Replace IsPossibleToStay callvirt/call in PlayerMovementGroundMove.Update
        var groundMoveType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "PlayerMovementGroundMove")
            ?? throw new InvalidOperationException("PlayerMovementGroundMove type not found.");

        var updateMethod = groundMoveType.Methods.FirstOrDefault(static m => m.Name == "Update" && !m.IsStatic && m.HasBody)
            ?? throw new InvalidOperationException("PlayerMovementGroundMove.Update not found.");

        var alreadyPatched = updateMethod.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == "IsPossibleToStayForAutoCrouch");
        if (alreadyPatched)
        {
            return;
        }

        var callInstr = updateMethod.Body.Instructions.FirstOrDefault(static i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr && mr.Name == "IsPossibleToStay")
            ?? throw new InvalidOperationException("IsPossibleToStay call not found in PlayerMovementGroundMove.Update.");

        callInstr.OpCode = OpCodes.Call;
        callInstr.Operand = isPossibleToStayWrapper;
    }
}
