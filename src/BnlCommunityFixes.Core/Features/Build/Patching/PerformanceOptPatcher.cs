using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class PerformanceOptPatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "performance-opt";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-performance-opt-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false))
        {
            return;
        }

        var runtimeType = context.HelperModule.Types.FirstOrDefault(
            static t => t.FullName == "BnlCommunityFixes.PerformanceOptRuntime");
        if (runtimeType is null)
        {
            return;
        }

        var getActiveHealthbarMakers = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetActiveHealthbarMakers"));
        if (getActiveHealthbarMakers is null)
        {
            return;
        }

        var registerHealthbarMaker = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "RegisterHealthbarMaker"));
        if (registerHealthbarMaker is null)
        {
            return;
        }

        var shouldSkipUpdate = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipUpdate"));
        if (shouldSkipUpdate is null)
        {
            return;
        }

        PatchGuiHealthbarPopulation(context, getActiveHealthbarMakers, shouldSkipUpdate);
        PatchGuiHealthBarMakerStart(context, registerHealthbarMaker);
    }

    private static void PatchGuiHealthbarPopulation(ExperimentalPatchContext context, MethodReference getActiveHealthbarMakers, MethodReference shouldSkipUpdate)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthbarPopulation");
        if (type is null)
        {
            return;
        }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "Update" && !m.IsStatic && m.HasBody);
        if (method is null || MethodAlreadyCalls(method, "GetActiveHealthbarMakers"))
        {
            return;
        }

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();

        // Inject early-exit guard at the top:
        //   if (ShouldSkipUpdate()) return;
        var firstInstruction = instructions.First();
        var retInstruction = il.Create(OpCodes.Ret);
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, shouldSkipUpdate));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Brfalse, firstInstruction));
        il.InsertBefore(firstInstruction, retInstruction);

        var targets = instructions.Where(static i =>
            i.OpCode == OpCodes.Ldsfld &&
            i.Operand is FieldReference fr &&
            fr.Name == "Units" &&
            fr.DeclaringType.Name == "GuiHealthbarPopulation").ToList();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            il.InsertAfter(target, il.Create(OpCodes.Call, getActiveHealthbarMakers));
        }
    }

    private static bool MethodAlreadyCalls(MethodDefinition method, string helperName) =>
        method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperName &&
            mr.DeclaringType.Name == "PerformanceOptRuntime");

    private static void PatchGuiHealthBarMakerStart(ExperimentalPatchContext context, MethodReference registerHealthbarMaker)
    {
        var type = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiHealthBarMaker");
        if (type is null)
        {
            return;
        }

        var method = type.Methods.FirstOrDefault(static m => m.Name == "Start" && !m.IsStatic && m.HasBody);
        if (method is null || MethodAlreadyCalls(method, "RegisterHealthbarMaker"))
        {
            return;
        }

        var instructions = method.Body.Instructions;
        var il = method.Body.GetILProcessor();
        var addCall = instructions.FirstOrDefault(static i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == "Add" &&
            mr.DeclaringType.Name.StartsWith("List", StringComparison.Ordinal));
        if (addCall is null || addCall.Next is null)
        {
            return;
        }

        il.InsertAfter(addCall, il.Create(OpCodes.Ldarg_0));
        il.InsertAfter(addCall.Next, il.Create(OpCodes.Call, registerHealthbarMaker));
    }
}
