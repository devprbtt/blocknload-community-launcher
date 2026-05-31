using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class DebugMenuFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "debug-menu";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-debug-menu-config.json");
        var debugMenuKey  = PatcherConfigReader.GetString(config, "debug_menu_key",  "F9");
        var mainMenuKey   = PatcherConfigReader.GetString(config, "main_menu_key",   "F10");
        var lobbyMenuKey  = PatcherConfigReader.GetString(config, "lobby_menu_key",  "F11");
        var zoneMenuKey   = PatcherConfigReader.GetString(config, "zone_menu_key",   "F12");

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.DebugMenuRuntime")
            ?? throw new InvalidOperationException("DebugMenuRuntime not found in helper assembly.");

        var configureMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "Configure" && m.Parameters.Count == 5)
            ?? throw new InvalidOperationException("DebugMenuRuntime.Configure(5 params) not found."));

        var ensureInstance = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "EnsureInstance")
            ?? throw new InvalidOperationException("DebugMenuRuntime.EnsureInstance not found."));

        var mainMenuType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MainMenu")
            ?? throw new InvalidOperationException("MainMenu type not found.");

        var startMethod = mainMenuType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("MainMenu.Start not found.");

        if (startMethod.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.DeclaringType.Name == "DebugMenuRuntime" &&
                (mr.Name == "Configure" || mr.Name == "EnsureInstance")))
        {
            return;
        }

        var il = startMethod.Body.GetILProcessor();
        var first = startMethod.Body.Instructions[0];

        // Configure(true, debugMenuKey, mainMenuKey, lobbyMenuKey, zoneMenuKey)
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, debugMenuKey));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, mainMenuKey));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, lobbyMenuKey));
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, zoneMenuKey));
        il.InsertBefore(first, il.Create(OpCodes.Call, configureMethod));
        il.InsertBefore(first, il.Create(OpCodes.Call, ensureInstance));
    }
}
