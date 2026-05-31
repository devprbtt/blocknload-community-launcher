using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class DeathCamHealthbarFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "deathcam-healthbar";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.DeathCamRuntime")
            ?? throw new InvalidOperationException("DeathCamRuntime not found in helper assembly.");

        var isDeathCamFriendly = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "IsDeathCamFriendly")
            ?? throw new InvalidOperationException("DeathCamRuntime.IsDeathCamFriendly not found."));

        var updateHpText = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "UpdateDeathCamHpText")
            ?? throw new InvalidOperationException("DeathCamRuntime.UpdateDeathCamHpText not found."));

        // Patch GuiDeathCameraTargets.Update(): inject UpdateDeathCamHpText(Nickname) before last ret
        var deathCamTargetsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiDeathCameraTargets")
            ?? throw new InvalidOperationException("GuiDeathCameraTargets type not found.");

        var updateMethod = deathCamTargetsType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody)
            ?? throw new InvalidOperationException("GuiDeathCameraTargets.Update not found.");

        var nicknameField = context.TargetModule.ImportReference(
            deathCamTargetsType.Fields.FirstOrDefault(static f => f.Name == "Nickname")
            ?? throw new InvalidOperationException("GuiDeathCameraTargets.Nickname field not found."));

        if (!HasHelperCall(updateMethod, "UpdateDeathCamHpText"))
        {
            var updateIl = updateMethod.Body.GetILProcessor();
            var updateRet = updateMethod.Body.Instructions.LastOrDefault(static i => i.OpCode.Code == Code.Ret)
                ?? throw new InvalidOperationException("GuiDeathCameraTargets.Update Ret not found.");

            updateIl.InsertBefore(updateRet, updateIl.Create(OpCodes.Ldarg_0));
            updateIl.InsertBefore(updateRet, updateIl.Create(OpCodes.Ldfld, nicknameField));
            updateIl.InsertBefore(updateRet, updateIl.Create(OpCodes.Call, updateHpText));
        }

        // Patch GuiHealthbar.IsUnitAvailableForShow + AlphaUpdate for death cam visibility
        var healthbarType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbar");
        if (healthbarType is not null)
        {
            var unitField = context.TargetModule.ImportReference(
                healthbarType.Fields.FirstOrDefault(static f => f.Name == "unit")
                ?? throw new InvalidOperationException("GuiHealthbar.unit not found."));

            var showTimeField = context.TargetModule.ImportReference(
                healthbarType.Fields.FirstOrDefault(static f => f.Name == "showTime")
                ?? throw new InvalidOperationException("GuiHealthbar.showTime not found."));

            PatchIsUnitAvailableForShow(healthbarType, isDeathCamFriendly, unitField);
            PatchAlphaUpdate(healthbarType, isDeathCamFriendly, unitField, showTimeField);

            // Attach DeathCamHealthbarController via GuiHealthbar.Start()
            var attachMethod = runtimeType.Methods.FirstOrDefault(static m => m.Name == "AttachDeathCamController");
            if (attachMethod is not null)
            {
                var importedAttach = context.TargetModule.ImportReference(attachMethod);
                var startMethod = healthbarType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody);
                if (startMethod is not null)
                {
                    var startIl = startMethod.Body.GetILProcessor();
                    var startRet = startMethod.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret);
                    if (startRet is not null && !HasHelperCall(startMethod, "AttachDeathCamController"))
                    {
                        startIl.InsertBefore(startRet, startIl.Create(OpCodes.Ldarg_0));
                        startIl.InsertBefore(startRet, startIl.Create(OpCodes.Call, importedAttach));
                    }
                }
            }
        }
    }

    private static void PatchIsUnitAvailableForShow(TypeDefinition healthbarType, MethodReference isDeathCamFriendly, FieldReference unitField)
    {
        var method = healthbarType.Methods.FirstOrDefault(static m => m.Name == "IsUnitAvailableForShow" && m.HasBody);
        if (method is null || HasHelperCall(method, "IsDeathCamFriendly")) return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var branch = il.Create(OpCodes.Brfalse_S, first);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldfld, unitField));
        il.InsertBefore(first, il.Create(OpCodes.Call, isDeathCamFriendly));
        il.InsertBefore(first, branch);
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
    }

    private static void PatchAlphaUpdate(TypeDefinition healthbarType, MethodReference isDeathCamFriendly, FieldReference unitField, FieldReference showTimeField)
    {
        var method = healthbarType.Methods.FirstOrDefault(static m => m.Name == "AlphaUpdate" && m.HasBody);
        if (method is null || HasHelperCall(method, "IsDeathCamFriendly")) return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var branch = il.Create(OpCodes.Brfalse_S, first);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldfld, unitField));
        il.InsertBefore(first, il.Create(OpCodes.Call, isDeathCamFriendly));
        il.InsertBefore(first, branch);
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, 1f));
        il.InsertBefore(first, il.Create(OpCodes.Stfld, showTimeField));
    }

    private static bool HasHelperCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "DeathCamRuntime");
    }
}
