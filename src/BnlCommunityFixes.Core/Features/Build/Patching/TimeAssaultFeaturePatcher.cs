using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class TimeAssaultFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "time-assault";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-time-assault-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false))
        {
            return;
        }

        PatchSceneServiceListener(context);
        PatchTimeTrialServiceButton(context);
        PatchUiMenuPlay(context);
        PatchSceneManager(context);
    }

    private static void PatchSceneServiceListener(ExperimentalPatchContext context)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneServiceListener");
        if (type is null)
        {
            return;
        }

        var field = type.Fields.FirstOrDefault(static f => f.Name == "TimeAssaultEnabled");
        var method = type.Methods.FirstOrDefault(static m => m.Name == "ServerUpdate" && m.HasBody);
        if (field is null || method is null || HasForcedTrueStore(method, field))
        {
            return;
        }

        var retInstructions = method.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray();
        if (retInstructions.Length == 0)
        {
            return;
        }

        var fieldRef = context.TargetModule.ImportReference(field);
        var il = method.Body.GetILProcessor();
        foreach (var ret in retInstructions)
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(ret, il.Create(OpCodes.Stfld, fieldRef));
        }
    }

    private static void PatchTimeTrialServiceButton(ExperimentalPatchContext context)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiSceneServiceListenerButton");
        if (type is null)
        {
            return;
        }

        var serviceField = type.Fields.FirstOrDefault(static f => f.Name == "Service");
        var method = type.Methods.FirstOrDefault(static m => m.Name == "IsServiceEnabled" && m.HasBody);
        if (serviceField is null || method is null || MethodStartsWithTimeTrialFastPath(method))
        {
            return;
        }

        var first = method.Body.Instructions.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        var serviceFieldRef = context.TargetModule.ImportReference(serviceField);
        var il = method.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldfld, serviceFieldRef));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Bne_Un_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    private static void PatchUiMenuPlay(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.TimeAssaultRuntime");
        if (runtimeType is null)
        {
            throw new InvalidOperationException("TimeAssaultRuntime not found in helper assembly.");
        }

        var ensureMenuTab = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureMenuTab" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("TimeAssaultRuntime.EnsureMenuTab not found."));

        var uiMenuPlayType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiMenuPlay")
            ?? throw new InvalidOperationException("UiMenuPlay type not found.");

        var awakeMethod = uiMenuPlayType.Methods.FirstOrDefault(static m => m.Name == "Awake" && m.HasBody)
            ?? throw new InvalidOperationException("UiMenuPlay.Awake not found.");

        if (MethodCalls(awakeMethod, "EnsureMenuTab"))
        {
            return;
        }

        var il = awakeMethod.Body.GetILProcessor();
        foreach (var ret in awakeMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret, il.Create(OpCodes.Call, ensureMenuTab));
        }
    }

    private static void PatchSceneManager(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.TimeAssaultRuntime");
        if (runtimeType is null)
        {
            throw new InvalidOperationException("TimeAssaultRuntime not found in helper assembly.");
        }

        var normalizeSceneRestart = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "NormalizeSceneRestart" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("TimeAssaultRuntime.NormalizeSceneRestart not found."));

        var sceneManagerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneManager")
            ?? throw new InvalidOperationException("SceneManager type not found.");

        var serverLoadZoneMethod = sceneManagerType.Methods.FirstOrDefault(static m => m.Name == "ServerLoadZone" && m.HasBody && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("SceneManager.ServerLoadZone not found.");

        if (MethodCalls(serverLoadZoneMethod, "NormalizeSceneRestart"))
        {
            return;
        }

        var first = serverLoadZoneMethod.Body.Instructions.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        var il = serverLoadZoneMethod.Body.GetILProcessor();
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, normalizeSceneRestart));
    }

    private static bool MethodCalls(MethodDefinition method, string methodName) =>
        method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == methodName);

    private static bool HasForcedTrueStore(MethodDefinition method, FieldDefinition field)
    {
        var instructions = method.Body.Instructions;
        for (var index = 0; index <= instructions.Count - 3; index++)
        {
            if (instructions[index].OpCode != OpCodes.Ldarg_0 ||
                instructions[index + 1].OpCode != OpCodes.Ldc_I4_1 ||
                instructions[index + 2].OpCode != OpCodes.Stfld)
            {
                continue;
            }

            if (instructions[index + 2].Operand is FieldReference fieldRef &&
                fieldRef.Name == field.Name &&
                fieldRef.DeclaringType.Name == field.DeclaringType.Name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MethodStartsWithTimeTrialFastPath(MethodDefinition method)
    {
        var instructions = method.Body.Instructions;
        if (instructions.Count < 6)
        {
            return false;
        }

        return instructions[0].OpCode == OpCodes.Ldarg_0 &&
               instructions[1].OpCode == OpCodes.Ldfld &&
               instructions[2].OpCode == OpCodes.Ldc_I4_1 &&
               instructions[3].OpCode == OpCodes.Bne_Un_S &&
               instructions[4].OpCode == OpCodes.Ldc_I4_1 &&
               instructions[5].OpCode == OpCodes.Ret;
    }
}
