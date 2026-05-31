using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class CrosshairFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "crosshair";

    public void Apply(ExperimentalPatchContext context)
    {
        var crosshairRuntimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.CrosshairRuntime")
            ?? throw new InvalidOperationException("CrosshairRuntime type not found in helper assembly.");

        var applyHardHide = ImportHelperMethod(context, crosshairRuntimeType, "ApplyHardHide");
        var applyVisibility = ImportHelperMethod(context, crosshairRuntimeType, "ApplyVisibility");
        var applyController = ImportHelperMethod(context, crosshairRuntimeType, "ApplyController");
        var applyBlank = ImportHelperMethod(context, crosshairRuntimeType, "ApplyBlank");
        var scaleAngle = ImportHelperMethod(context, crosshairRuntimeType, "ScaleAngle");
        var scaleSizeVector = ImportHelperMethod(context, crosshairRuntimeType, "ScaleSizeVector");
        var getCrosshairPrefab = ImportHelperMethod(context, crosshairRuntimeType, "GetAppropriateCrosshairPrefab");

        PatchGuiCrosshairController(context, applyHardHide, applyVisibility, applyController, getCrosshairPrefab);
        PatchGuiCrosshairBlankSetColor(context, applyBlank);
        PatchSetAngleMethods(context, scaleAngle, scaleSizeVector, applyBlank);
    }

    private static void PatchGuiCrosshairController(
        ExperimentalPatchContext context,
        MethodReference applyHardHide,
        MethodReference applyVisibility,
        MethodReference applyController,
        MethodReference getCrosshairPrefab)
    {
        var controllerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiCrosshairController");
        if (controllerType is null)
        {
            return;
        }

        var updateMethod = controllerType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (updateMethod is not null)
        {
            var il = updateMethod.Body.GetILProcessor();
            var instructions = updateMethod.Body.Instructions.ToArray();

            if (instructions.Length > 0 && !HasHelperCall(updateMethod, "ApplyHardHide"))
            {
                // Insert at top: if (ApplyHardHide(this)) return;
                var continueNop = il.Create(OpCodes.Nop);
                il.InsertBefore(instructions[0], il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(instructions[0], il.Create(OpCodes.Call, applyHardHide));
                il.InsertBefore(instructions[0], il.Create(OpCodes.Brfalse_S, continueNop));
                il.InsertBefore(instructions[0], il.Create(OpCodes.Ret));
                il.InsertBefore(instructions[0], continueNop);
            }

            // After SetActive call: ApplyVisibility(this)
            var setActiveCall = updateMethod.Body.Instructions
                .FirstOrDefault(static i => i.Operand is MethodReference m && m.Name == "SetActive");
            if (setActiveCall is not null && !HasHelperCall(updateMethod, "ApplyVisibility"))
            {
                var afterSetActive = setActiveCall.Next;
                il.InsertBefore(afterSetActive, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(afterSetActive, il.Create(OpCodes.Call, applyVisibility));
            }

            // Before UpdatePopulation call: ApplyController(this)
            var updatePopulationCall = updateMethod.Body.Instructions
                .FirstOrDefault(static i => i.Operand is MethodReference m && m.Name == "UpdatePopulation");
            if (updatePopulationCall is not null && !HasHelperCall(updateMethod, "ApplyController"))
            {
                il.InsertBefore(updatePopulationCall, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(updatePopulationCall, il.Create(OpCodes.Call, applyController));
            }
        }

        // Replace GetAppropriateCrosshairPrefab body entirely — only when the method has exactly
        // one explicit parameter (ReticleInfo), matching the helper's (controller, reticleInfo) signature.
        var getAppropriateMethod = controllerType.Methods.FirstOrDefault(static m =>
            m.Name == "GetAppropriateCrosshairPrefab" && m.Parameters.Count == 1);
        if (getAppropriateMethod is not null)
        {
            getAppropriateMethod.Body.Instructions.Clear();
            getAppropriateMethod.Body.Variables.Clear();
            getAppropriateMethod.Body.ExceptionHandlers.Clear();
            getAppropriateMethod.Body.InitLocals = false;
            var replaceIl = getAppropriateMethod.Body.GetILProcessor();
            replaceIl.Append(replaceIl.Create(OpCodes.Ldarg_0));
            replaceIl.Append(replaceIl.Create(OpCodes.Ldarg_1));
            replaceIl.Append(replaceIl.Create(OpCodes.Call, getCrosshairPrefab));
            replaceIl.Append(replaceIl.Create(OpCodes.Ret));
        }
    }

    private static void PatchGuiCrosshairBlankSetColor(ExperimentalPatchContext context, MethodReference applyBlank)
    {
        var blankType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiCrosshairBlank");
        if (blankType is null)
        {
            return;
        }

        var setColorMethod = blankType.Methods.FirstOrDefault(static m => m.Name == "SetColor" && m.HasBody);
        if (setColorMethod is null)
        {
            return;
        }

        if (HasHelperCall(setColorMethod, "ApplyBlank"))
        {
            return;
        }

        var il = setColorMethod.Body.GetILProcessor();
        var retInstruction = setColorMethod.Body.Instructions
            .LastOrDefault(static i => i.OpCode == OpCodes.Ret);
        if (retInstruction is not null)
        {
            il.InsertBefore(retInstruction, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(retInstruction, il.Create(OpCodes.Call, applyBlank));
        }
    }

    private static void PatchSetAngleMethods(
        ExperimentalPatchContext context,
        MethodReference scaleAngle,
        MethodReference scaleSizeVector,
        MethodReference applyBlank)
    {
        foreach (var typeName in new[] { "GuiCrosshair", "GuiCrosshairCircle", "GuiCrosshairMelee" })
        {
            var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
            if (type is null)
            {
                continue;
            }

            var setAngle = type.Methods.FirstOrDefault(static m => m.Name == "SetAngle" && m.HasBody && m.Parameters.Count >= 1);
            if (setAngle is null)
            {
                continue;
            }

            var il = setAngle.Body.GetILProcessor();
            var instructions = setAngle.Body.Instructions.ToArray();

            // For GuiCrosshair: after each ldfld MaxBloom, call ScaleSizeVector
            if (typeName == "GuiCrosshair")
            {
                var maxBloomField = type.Fields.FirstOrDefault(static f => f.Name == "MaxBloom");
                if (maxBloomField is not null)
                {
                    var maxBloomLoads = setAngle.Body.Instructions
                        .Where(i => i.OpCode.Code == Code.Ldfld && i.Operand is FieldReference fr && fr.Resolve()?.MetadataToken == maxBloomField.MetadataToken)
                        .ToArray();
                    foreach (var load in maxBloomLoads)
                    {
                        il.InsertAfter(load, il.Create(OpCodes.Call, scaleSizeVector));
                    }
                }
            }

            // At top: arg = ScaleAngle(arg)
            if (instructions.Length > 0 && !HasHelperCall(setAngle, "ScaleAngle"))
            {
                il.InsertBefore(instructions[0], il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(instructions[0], il.Create(OpCodes.Call, scaleAngle));
                il.InsertBefore(instructions[0], il.Create(OpCodes.Starg_S, setAngle.Parameters[0]));
            }

            // Before last Ret: ApplyBlank(this)
            var ret = setAngle.Body.Instructions.LastOrDefault(static i => i.OpCode == OpCodes.Ret);
            if (ret is not null && !HasHelperCall(setAngle, "ApplyBlank"))
            {
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Call, applyBlank));
            }
        }
    }

    private static MethodReference ImportHelperMethod(ExperimentalPatchContext context, TypeDefinition helperType, string methodName)
    {
        var method = helperType.Methods.FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"CrosshairRuntime.{methodName} not found in helper assembly.");
        return context.TargetModule.ImportReference(method);
    }

    private static bool HasHelperCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "CrosshairRuntime");
    }
}
