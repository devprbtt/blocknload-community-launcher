using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Experimental font patching for UI canvases — guards on experimental-font-config.json enabled.</summary>
public sealed class FontUiFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__font-ui";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-font-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled")) return;

        var fontRuntimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.FontRuntime")
            ?? throw new InvalidOperationException("FontRuntime not found in helper assembly.");

        var applyAllCanvases = context.TargetModule.ImportReference(
            fontRuntimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyAllCanvases")
            ?? throw new InvalidOperationException("FontRuntime.ApplyAllCanvases not found."));

        var applyToText = context.TargetModule.ImportReference(
            fontRuntimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyToText" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("FontRuntime.ApplyToText not found."));

        // ApplyAllCanvases() at MainMenu.Start and CameraFov.Start
        foreach (var (typeName, methodName) in new[] { ("MainMenu", "Start"), ("CameraFov", "Start") })
        {
            var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
            var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
            if (method is null) continue;
            if (HasFontRuntimeCall(method, "ApplyAllCanvases")) continue;
            var il = method.Body.GetILProcessor();
            il.InsertBefore(method.Body.Instructions[0], il.Create(OpCodes.Call, applyAllCanvases));
        }

        // UiStyleFontComponent.SetStyle — before every Ret: ldarg.0 / ldfld m_text / call ApplyToText
        var styleType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "UiStyleFontComponent");
        var setStyle = styleType?.Methods.FirstOrDefault(static m => m.Name == "SetStyle" && m.HasBody);
        if (setStyle is not null)
        {
            var mTextField = styleType!.Fields.FirstOrDefault(static f => f.Name == "m_text");
            if (mTextField is not null)
            {
                var importedField = context.TargetModule.ImportReference(mTextField);
                var il = setStyle.Body.GetILProcessor();
                foreach (var ret in setStyle.Body.Instructions.Where(static i => i.OpCode.Code == Code.Ret).ToArray())
                {
                    if (HasFontRuntimeCall(setStyle, "ApplyToText")) break;
                    il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(ret, il.Create(OpCodes.Ldfld, importedField));
                    il.InsertBefore(ret, il.Create(OpCodes.Call, applyToText));
                }
            }
        }
    }

    private static bool HasFontRuntimeCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "FontRuntime");
    }
}
