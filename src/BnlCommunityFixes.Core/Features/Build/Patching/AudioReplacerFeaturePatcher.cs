using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class AudioReplacerFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "audio-replacer";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-audio-replacer-config.json");
        var logAll       = PatcherConfigReader.GetBool(config,  "log_all_events", true);
        var volume       = PatcherConfigReader.GetFloat(config, "volume",          1f);
        var replacements = PatcherConfigReader.GetStringDict(config, "replacements");
        var customAudio  = PatcherConfigReader.GetStringDict(config, "custom_audio");
        var volumes      = PatcherConfigReader.GetFloatDict(config,  "volumes");
        var ignored      = PatcherConfigReader.GetStringArray(config, "ignored_events");

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AudioReplacerRuntime")
            ?? throw new InvalidOperationException("AudioReplacerRuntime not found in helper assembly.");

        var logAndResolvePostEvent      = TryImp(context, runtimeType, "LogAndResolvePostEvent");
        var logAndResolveWithFlags      = TryImp(context, runtimeType, "LogAndResolvePostEventWithFlags");
        var shouldSuppressUint          = TryImp(context, runtimeType, "ShouldSuppressUint");
        var beginBootstrap              = Imp(context, runtimeType, "BeginBootstrap");
        var registerReplacement         = Imp(context, runtimeType, "RegisterReplacement");
        var registerCustomReplacement   = TryImp(context, runtimeType, "RegisterCustomReplacement");
        var registerEventVolume         = TryImp(context, runtimeType, "RegisterEventVolume");
        var ignoreEvent                 = TryImp(context, runtimeType, "IgnoreEvent");
        var logRegistered               = Imp(context, runtimeType, "LogRegisteredReplacements");

        var managerType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AudioReplacerManager");
        var setVolume = managerType is not null ? TryImp(context, managerType, "SetVolume") : null;

        // Patch AkSoundEngine.PostEvent string overloads
        var akType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "AkSoundEngine");
        if (akType is not null && logAndResolvePostEvent is not null && logAndResolveWithFlags is not null)
        {
            foreach (var m in akType.Methods.Where(static m => m.Name == "PostEvent" && m.HasBody && m.Parameters.Count >= 2))
            {
                if (m.Parameters[0].ParameterType.FullName != "System.String") continue;
                if (HasAudioRuntimeCall(m)) continue;

                var il = m.Body.GetILProcessor();
                var first = m.Body.Instructions[0];
                var cont = il.Create(OpCodes.Nop);
                var resolver = m.Parameters.Count >= 3 ? logAndResolveWithFlags : logAndResolvePostEvent;

                il.InsertBefore(first, il.Create(OpCodes.Ldarga_S, m.Parameters[0]));
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
                if (m.Parameters.Count >= 3) il.InsertBefore(first, il.Create(OpCodes.Ldarg_2));
                il.InsertBefore(first, il.Create(OpCodes.Call, resolver));
                il.InsertBefore(first, il.Create(OpCodes.Brfalse, cont));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
                il.InsertBefore(first, il.Create(OpCodes.Ret));
                il.InsertBefore(first, cont);
            }
        }

        // Patch AkSoundEngine.PostEvent uint overloads
        if (akType is not null && shouldSuppressUint is not null)
        {
            foreach (var m in akType.Methods.Where(static m => m.Name == "PostEvent" && m.HasBody && m.Parameters.Count >= 2))
            {
                if (m.Parameters[0].ParameterType.FullName != "System.UInt32") continue;
                if (HasAudioRuntimeCall(m)) continue;

                var il = m.Body.GetILProcessor();
                var first = m.Body.Instructions[0];
                var cont = il.Create(OpCodes.Nop);

                il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first, il.Create(OpCodes.Call, shouldSuppressUint));
                il.InsertBefore(first, il.Create(OpCodes.Brfalse, cont));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
                il.InsertBefore(first, il.Create(OpCodes.Ret));
                il.InsertBefore(first, cont);
            }
        }

        // Inject bootstrap + registrations into MainMenu.Start and GearModel.Awake
        InjectBootstrap(context, "MainMenu", "Start", logAll, volume, replacements, customAudio, volumes, ignored,
            beginBootstrap, registerReplacement, registerCustomReplacement, registerEventVolume, ignoreEvent, logRegistered, setVolume);

        InjectBootstrap(context, "GearModel", "Awake", logAll, volume, replacements, customAudio, volumes, ignored,
            beginBootstrap, registerReplacement, registerCustomReplacement, registerEventVolume, ignoreEvent, logRegistered, setVolume);
    }

    private static void InjectBootstrap(
        ExperimentalPatchContext context,
        string typeName,
        string methodName,
        bool logAll,
        float volume,
        IReadOnlyDictionary<string, string> replacements,
        IReadOnlyDictionary<string, string> customAudio,
        IReadOnlyDictionary<string, float> volumes,
        IReadOnlyList<string> ignored,
        MethodReference beginBootstrap,
        MethodReference registerReplacement,
        MethodReference? registerCustomReplacement,
        MethodReference? registerEventVolume,
        MethodReference? ignoreEvent,
        MethodReference logRegistered,
        MethodReference? setVolume)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is null) return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var cont = il.Create(OpCodes.Nop);

        if (HasAudioRuntimeCall(method))
        {
            return;
        }

        // if (!BeginBootstrap(logAll)) goto cont;
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, logAll ? 1 : 0));
        il.InsertBefore(first, il.Create(OpCodes.Call, beginBootstrap));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse, cont));

        // SetVolume(volume)
        if (setVolume is not null)
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, volume));
            il.InsertBefore(first, il.Create(OpCodes.Call, setVolume));
        }

        // RegisterReplacement(orig, replacement) for each entry
        foreach (var kvp in replacements)
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Key));
            il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Value));
            il.InsertBefore(first, il.Create(OpCodes.Call, registerReplacement));
        }

        // RegisterCustomReplacement(orig, filePath) for each custom entry
        if (registerCustomReplacement is not null)
        {
            foreach (var kvp in customAudio)
            {
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Key));
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Value));
                il.InsertBefore(first, il.Create(OpCodes.Call, registerCustomReplacement));
            }
        }

        il.InsertBefore(first, il.Create(OpCodes.Call, logRegistered));

        // RegisterEventVolume(eventName, vol/100) for each entry
        if (registerEventVolume is not null)
        {
            foreach (var kvp in volumes)
            {
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Key));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, kvp.Value / 100f));
                il.InsertBefore(first, il.Create(OpCodes.Call, registerEventVolume));
            }
        }

        if (ignoreEvent is not null)
        {
            foreach (var eventName in ignored)
            {
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, eventName));
                il.InsertBefore(first, il.Create(OpCodes.Call, ignoreEvent));
            }
        }

        il.InsertBefore(first, cont);
    }

    private static MethodReference Imp(ExperimentalPatchContext context, TypeDefinition type, string name) =>
        context.TargetModule.ImportReference(
            type.Methods.FirstOrDefault(m => m.Name == name)
            ?? throw new InvalidOperationException($"{type.Name}.{name} not found."));

    private static MethodReference? TryImp(ExperimentalPatchContext context, TypeDefinition type, string name)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == name);
        return method is null ? null : context.TargetModule.ImportReference(method);
    }

    private static bool HasAudioRuntimeCall(MethodDefinition method)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            (mr.DeclaringType.Name == "AudioReplacerRuntime" || mr.DeclaringType.Name == "AudioReplacerManager"));
    }
}
