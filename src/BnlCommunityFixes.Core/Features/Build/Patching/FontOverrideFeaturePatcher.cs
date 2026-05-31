using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class FontOverrideFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "font-override";

    public void Apply(ExperimentalPatchContext context)
    {
        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.FontOverrideRuntime")
            ?? throw new InvalidOperationException("FontOverrideRuntime not found in helper assembly.");

        var ensureInit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInit")
            ?? throw new InvalidOperationException("FontOverrideRuntime.EnsureInit not found."));

        var patchChatMessage = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "PatchChatMessage")
            ?? throw new InvalidOperationException("FontOverrideRuntime.PatchChatMessage not found."));

        // GuiActivityScroll.Start — call EnsureInit() at top
        InjectEnsureInitAtStart(context, "GuiActivityScroll", ensureInit);

        // GuiNotices.Start — call EnsureInit() at top
        InjectEnsureInitAtStart(context, "GuiNotices", ensureInit);

        // UiChatMessage.Fill (all overloads) — call PatchChatMessage(this) at top
        var chatMsgType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiChatMessage");
        if (chatMsgType is not null)
        {
            foreach (var fillMethod in chatMsgType.Methods.Where(static m => m.Name == "Fill" && m.HasBody))
            {
                if (fillMethod.Body.Instructions.Any(i =>
                        (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                        i.Operand is MethodReference mr &&
                        mr.Name == "PatchChatMessage"))
                {
                    continue;
                }

                var il = fillMethod.Body.GetILProcessor();
                var first = fillMethod.Body.Instructions[0];
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, il.Create(OpCodes.Call, patchChatMessage));
            }
        }
    }

    private static void InjectEnsureInitAtStart(ExperimentalPatchContext context, string typeName, MethodReference ensureInit)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        if (type is null) return;

        var startMethod = type.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody);
        if (startMethod is null) return;

        if (startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "EnsureInit" &&
                mr.DeclaringType.Name == "FontOverrideRuntime"))
        {
            return;
        }

        var il = startMethod.Body.GetILProcessor();
        il.InsertBefore(startMethod.Body.Instructions[0], il.Create(OpCodes.Call, ensureInit));
    }
}
