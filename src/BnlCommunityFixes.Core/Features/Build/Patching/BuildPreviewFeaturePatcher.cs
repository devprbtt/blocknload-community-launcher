using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class BuildPreviewFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "build-preview";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.LocalBuildPredictionRuntime")
            ?? throw new InvalidOperationException("LocalBuildPredictionRuntime not found in helper assembly.");

        MethodReference Imp(string name) => context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(m => m.Name == name)
            ?? throw new InvalidOperationException($"LocalBuildPredictionRuntime.{name} not found."));

        var onLocalPlace               = Imp("OnLocalPlace");
        var onBlockUpdates             = Imp("OnBlockUpdates");
        var onDeviceBuilt              = Imp("OnDeviceBuilt");
        var onUnitCreate               = Imp("OnUnitCreate");
        var shouldBypassBuildValidate  = Imp("ShouldBypassBuildValidate");
        var shouldZeroBuildTime        = Imp("ShouldZeroBuildTime");
        var getBuildPrecastTime        = Imp("GetBuildPrecastTime");
        var getBuildTotalCastTime      = Imp("GetBuildTotalCastTime");
        var shouldSkipWait             = Imp("ShouldSkipBuildCompletionWait");
        var tryInstantAcceptStartBuild = Imp("TryInstantAcceptStartBuild");
        var tryInstantAcceptSwitchGear = Imp("TryInstantAcceptSwitchGear");
        var onStartBuildResult         = Imp("OnStartBuildResult");

        // BuildGhostController.Place — call OnLocalPlace(this) after ServiceZone.Hit
        PatchPlace(context, onLocalPlace);

        // ZoneServiceListener — BlockUpdates, DeviceBuilt, UnitCreate
        PatchZoneServiceListener(context, onBlockUpdates, onDeviceBuilt, onUnitCreate);

        // ServiceZone.StartBuild — inject TryInstantAcceptStartBuild before first ret
        PatchStartBuild(context, tryInstantAcceptStartBuild);

        // ServiceZone.Rpc_StartBuild._Success — notify OnStartBuildResult
        PatchRpcStartBuildSuccess(context, onStartBuildResult);

        // BuffHelper.BuildTime — zero build time bypass
        PatchBuffHelperBuildTime(context, shouldZeroBuildTime);

        // ToolLogicBuild.ValidateUse — bypass validation
        PatchValidateUse(context, shouldBypassBuildValidate);

        // PlayerActSwitch coroutine — instant gear switch
        PatchPlayerActSwitch(context, tryInstantAcceptSwitchGear);

        // ToolLogicBuild coroutines — replace GetTotalCastTime / GetPrecastTime
        PatchToolLogicBuildCoroutines(context, getBuildPrecastTime, getBuildTotalCastTime, shouldSkipWait);
    }

    private static void PatchPlace(ExperimentalPatchContext context, MethodReference onLocalPlace)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "BuildGhostController")
            ?? throw new InvalidOperationException("BuildGhostController not found.");
        var method = type.Methods.FirstOrDefault(static m => m.Name == "Place" && m.HasBody)
            ?? throw new InvalidOperationException("BuildGhostController.Place not found.");

        if (HasHelperCall(method, "OnLocalPlace"))
        {
            return;
        }

        var hitCall = method.Body.Instructions.FirstOrDefault(static i =>
            (i.OpCode.Code == Code.Callvirt || i.OpCode.Code == Code.Call) &&
            i.Operand is MethodReference mr && mr.Name == "Hit")
            ?? throw new InvalidOperationException("ServiceZone.Hit call not found in BuildGhostController.Place.");

        var il = method.Body.GetILProcessor();
        var afterHit = hitCall.Next;
        il.InsertBefore(afterHit, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(afterHit, il.Create(OpCodes.Call, onLocalPlace));
    }

    private static void PatchZoneServiceListener(ExperimentalPatchContext context, MethodReference onBlockUpdates, MethodReference onDeviceBuilt, MethodReference onUnitCreate)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ZoneServiceListener")
            ?? throw new InvalidOperationException("ZoneServiceListener not found.");

        void InjectAtStart(string methodName, Action<ILProcessor, Instruction> inject)
        {
            var m = type.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody)
                ?? throw new InvalidOperationException($"ZoneServiceListener.{methodName} not found.");
            if (HasAnyBuildPreviewHelperCall(m))
            {
                return;
            }
            inject(m.Body.GetILProcessor(), m.Body.Instructions[0]);
        }

        InjectAtStart("BlockUpdates", (il, first) =>
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Call, onBlockUpdates));
        });

        InjectAtStart("DeviceBuilt", (il, first) =>
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_3));
            il.InsertBefore(first, il.Create(OpCodes.Call, onDeviceBuilt));
        });

        InjectAtStart("UnitCreate", (il, first) =>
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(first, il.Create(OpCodes.Call, onUnitCreate));
        });
    }

    private static void PatchStartBuild(ExperimentalPatchContext context, MethodReference tryInstantAcceptStartBuild)
    {
        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone")
            ?? throw new InvalidOperationException("ServiceZone not found.");
        var method = serviceZoneType.Methods.FirstOrDefault(static m => m.Name == "StartBuild" && m.HasBody)
            ?? throw new InvalidOperationException("ServiceZone.StartBuild not found.");
        if (HasHelperCall(method, "TryInstantAcceptStartBuild"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var ret = method.Body.Instructions.FirstOrDefault(static i => i.OpCode.Code == Code.Ret)
            ?? throw new InvalidOperationException("ServiceZone.StartBuild Ret not found.");

        il.InsertBefore(ret, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(ret, il.Create(OpCodes.Ldloc_0));
        il.InsertBefore(ret, il.Create(OpCodes.Call, tryInstantAcceptStartBuild));
    }

    private static void PatchRpcStartBuildSuccess(ExperimentalPatchContext context, MethodReference onStartBuildResult)
    {
        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone")
            ?? throw new InvalidOperationException("ServiceZone not found.");
        var rpcType = serviceZoneType.NestedTypes.FirstOrDefault(static t => t.Name == "Rpc_StartBuild")
            ?? throw new InvalidOperationException("ServiceZone.Rpc_StartBuild not found.");
        var method = rpcType.Methods.FirstOrDefault(static m => m.Name == "_Success" && m.HasBody)
            ?? throw new InvalidOperationException("Rpc_StartBuild._Success not found.");
        if (HasHelperCall(method, "OnStartBuildResult"))
        {
            return;
        }

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, onStartBuildResult));
    }

    private static void PatchBuffHelperBuildTime(ExperimentalPatchContext context, MethodReference shouldZeroBuildTime)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "BuffHelper")
            ?? throw new InvalidOperationException("BuffHelper not found.");
        var method = type.Methods.FirstOrDefault(static m => m.Name == "BuildTime" && m.HasBody)
            ?? throw new InvalidOperationException("BuffHelper.BuildTime not found.");
        if (HasHelperCall(method, "ShouldZeroBuildTime"))
        {
            return;
        }

        // Inject: if (ShouldZeroBuildTime(arg0, arg2)) return 0f; at top
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var cont = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldZeroBuildTime));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, 0f));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchValidateUse(ExperimentalPatchContext context, MethodReference shouldBypassBuildValidate)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ToolLogicBuild")
            ?? throw new InvalidOperationException("ToolLogicBuild not found.");
        var method = type.Methods.FirstOrDefault(static m => m.Name == "ValidateUse" && m.HasBody)
            ?? throw new InvalidOperationException("ToolLogicBuild.ValidateUse not found.");
        if (HasHelperCall(method, "ShouldBypassBuildValidate"))
        {
            return;
        }

        // Inject: if (ShouldBypassBuildValidate(this)) return true; at top
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var cont = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldBypassBuildValidate));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchPlayerActSwitch(ExperimentalPatchContext context, MethodReference tryInstantAcceptSwitchGear)
    {
        var playerActSwitchType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "PlayerActSwitch")
            ?? throw new InvalidOperationException("PlayerActSwitch not found.");

        // The coroutine iterator type name contains "DoAction" and "c__Iterator"
        var iteratorType = playerActSwitchType.NestedTypes.FirstOrDefault(static t =>
            t.Name.Contains("DoAction") && t.Name.Contains("c__Iterator"))
            ?? throw new InvalidOperationException("PlayerActSwitch DoAction iterator not found.");

        var moveNext = iteratorType.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody)
            ?? throw new InvalidOperationException("PlayerActSwitch iterator MoveNext not found.");
        if (HasHelperCall(moveNext, "TryInstantAcceptSwitchGear"))
        {
            return;
        }

        var playerActType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "PlayerAct")
            ?? throw new InvalidOperationException("PlayerAct not found.");
        var unitField = context.TargetModule.ImportReference(
            playerActType.Fields.FirstOrDefault(static f => f.Name == "unit")
            ?? throw new InvalidOperationException("PlayerAct.unit not found."));

        // Find the rpc store instruction (stfld for the <rpc>__ field)
        var rpcStoreInstr = moveNext.Body.Instructions.FirstOrDefault(i =>
            i.OpCode.Code == Code.Stfld &&
            i.Operand is FieldReference fr &&
            fr.DeclaringType.FullName.Contains("PlayerActSwitch") &&
            fr.Name.StartsWith("<rpc>__"))
            ?? throw new InvalidOperationException("PlayerActSwitch RPC store not found.");

        var thisField = context.TargetModule.ImportReference(
            iteratorType.Fields.FirstOrDefault(static f => f.Name == "<>f__this")
            ?? throw new InvalidOperationException("PlayerActSwitch iterator <>f__this not found."));

        var rpcField = context.TargetModule.ImportReference(
            iteratorType.Fields.FirstOrDefault(static f => f.Name.StartsWith("<rpc>__"))
            ?? throw new InvalidOperationException("PlayerActSwitch iterator <rpc>__ field not found."));

        var il = moveNext.Body.GetILProcessor();
        var nextInstr = rpcStoreInstr.Next;
        il.InsertBefore(nextInstr, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(nextInstr, il.Create(OpCodes.Ldfld, thisField));
        il.InsertBefore(nextInstr, il.Create(OpCodes.Ldfld, unitField));
        il.InsertBefore(nextInstr, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(nextInstr, il.Create(OpCodes.Ldfld, rpcField));
        il.InsertBefore(nextInstr, il.Create(OpCodes.Call, tryInstantAcceptSwitchGear));
    }

    private static void PatchToolLogicBuildCoroutines(
        ExperimentalPatchContext context,
        MethodReference getBuildPrecastTime,
        MethodReference getBuildTotalCastTime,
        MethodReference shouldSkipWait)
    {
        var buildType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ToolLogicBuild")
            ?? throw new InvalidOperationException("ToolLogicBuild not found.");

        // Replace ToolTimingHelper.GetTotalCastTime / GetPrecastTime calls in known coroutine types
        foreach (var iterTypeName in new[] { "DoUseProjectile", "DoUseRotationLock", "DoUseRotationFree" })
        {
            var iterType = buildType.NestedTypes.FirstOrDefault(t => t.Name.Contains(iterTypeName));
            var moveNext = iterType?.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody);
            if (moveNext is null) continue;

            ReplaceMethodCalls(moveNext, "ToolTimingHelper", "GetTotalCastTime", getBuildTotalCastTime);
            ReplaceMethodCalls(moveNext, "ToolTimingHelper", "GetPrecastTime", getBuildPrecastTime);

            // For RotationLock: also inject instant skip after the LastShotEndTime store
            if (iterTypeName == "DoUseRotationLock")
            {
                var lastShotStore = moveNext.Body.Instructions.LastOrDefault(i =>
                    i.OpCode.Code == Code.Stfld &&
                    i.Operand is FieldReference fr &&
                    fr.FullName == "System.Single GearData::LastShotEndTime");

                var returnTarget = moveNext.Body.Instructions.LastOrDefault(static i =>
                    i.OpCode.Code == Code.Ldc_I4_M1);

                if (lastShotStore is not null && returnTarget is not null)
                {
                    if (!HasHelperCall(moveNext, "ShouldSkipBuildCompletionWait"))
                    {
                        InjectImmediateReturnAfter(moveNext, lastShotStore, shouldSkipWait, returnTarget);
                    }
                }
            }
        }
    }

    private static void ReplaceMethodCalls(MethodDefinition method, string declaringTypeName, string methodName, MethodReference replacement)
    {
        foreach (var instr in method.Body.Instructions)
        {
            if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt) &&
                instr.Operand is MethodReference mr &&
                mr.DeclaringType.Name == declaringTypeName &&
                mr.Name == methodName)
            {
                instr.OpCode = OpCodes.Call;
                instr.Operand = replacement;
            }
        }
    }

    private static void InjectImmediateReturnAfter(
        MethodDefinition method,
        Instruction afterInstruction,
        MethodReference conditionCall,
        Instruction returnTarget)
    {
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);

        var next = afterInstruction.Next;
        il.InsertBefore(next, il.Create(OpCodes.Call, conditionCall));
        il.InsertBefore(next, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(next, il.Create(OpCodes.Br, returnTarget));
        il.InsertBefore(next, cont);
    }

    private static bool HasHelperCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "LocalBuildPredictionRuntime");
    }

    private static bool HasAnyBuildPreviewHelperCall(MethodDefinition method)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.DeclaringType.Name == "LocalBuildPredictionRuntime");
    }
}
