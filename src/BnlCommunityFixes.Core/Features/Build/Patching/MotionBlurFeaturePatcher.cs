using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class MotionBlurFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "motion-blur";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static type => type.FullName == "BnlCommunityFixes.MotionBlurRuntime")
            ?? throw new InvalidOperationException("MotionBlurRuntime not found in helper assembly.");

        InjectBeforeReturns(
            context,
            "AssignCameraEffects",
            "Start",
            runtimeType,
            "EnableAssignedEffect");

        InjectBeforeReturns(
            context,
            "CameraFov",
            "Start",
            runtimeType,
            "EnsureCameraEffect");

        ReplaceUnitMotionBlurUpdate(context, runtimeType);
    }

    private static void InjectBeforeReturns(
        ExperimentalPatchContext context,
        string targetTypeName,
        string targetMethodName,
        TypeDefinition runtimeType,
        string runtimeMethodName)
    {
        var targetType = context.TargetModule.Types.FirstOrDefault(type => type.Name == targetTypeName);
        var targetMethod = targetType?.Methods.FirstOrDefault(
            method => method.Name == targetMethodName && method.HasBody && !method.IsStatic);
        if (targetMethod is null) return;

        var runtimeMethod = runtimeType.Methods.FirstOrDefault(method => method.Name == runtimeMethodName)
            ?? throw new InvalidOperationException($"MotionBlurRuntime.{runtimeMethodName} not found.");
        var importedRuntimeMethod = context.TargetModule.ImportReference(runtimeMethod);

        if (targetMethod.Body.Instructions.Any(instruction =>
                instruction.Operand is MethodReference reference &&
                reference.Name == runtimeMethodName &&
                reference.DeclaringType.Name == "MotionBlurRuntime"))
        {
            return;
        }

        var il = targetMethod.Body.GetILProcessor();
        foreach (var ret in targetMethod.Body.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, importedRuntimeMethod));
        }
    }

    private static void ReplaceUnitMotionBlurUpdate(
        ExperimentalPatchContext context,
        TypeDefinition runtimeType)
    {
        var targetType = context.TargetModule.Types.FirstOrDefault(type => type.Name == "UnitMotionBlur");
        var update = targetType?.Methods.FirstOrDefault(
            method => method.Name == "Update" && method.HasBody && !method.IsStatic);
        if (update is null) return;

        var runtimeMethod = runtimeType.Methods.FirstOrDefault(method => method.Name == "EnableUnitBlur")
            ?? throw new InvalidOperationException("MotionBlurRuntime.EnableUnitBlur not found.");
        var importedRuntimeMethod = context.TargetModule.ImportReference(runtimeMethod);

        update.Body.Instructions.Clear();
        update.Body.ExceptionHandlers.Clear();
        update.Body.Variables.Clear();
        var il = update.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, importedRuntimeMethod));
        il.Append(il.Create(OpCodes.Ret));
    }
}
