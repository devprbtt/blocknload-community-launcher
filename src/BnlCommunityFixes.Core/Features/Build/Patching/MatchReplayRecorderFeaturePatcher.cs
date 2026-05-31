using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class MatchReplayRecorderFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "match-replay-recorder";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-match-replay-recorder-config.json");
        var capturePayload       = PatcherConfigReader.GetBool(config, "capture_payload",       true);
        var maxPayloadBytes      = PatcherConfigReader.GetInt(config,  "max_payload_bytes",      262144);
        var recordCustomGames    = PatcherConfigReader.GetBool(config, "record_custom_games",    true);
        var recordCasualGames    = PatcherConfigReader.GetBool(config, "record_casual_games",    true);
        var recordRankedGames    = PatcherConfigReader.GetBool(config, "record_ranked_games",    true);

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.MatchReplayRecorderRuntime")
            ?? throw new InvalidOperationException("MatchReplayRecorderRuntime not found in helper assembly.");

        var replayPlayerType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.ReplayPlayerRuntime");

        MethodReference Imp(string name, int? paramCount = null) => context.TargetModule.ImportReference(
            (paramCount is null
                ? runtimeType.Methods.FirstOrDefault(m => m.Name == name)
                : runtimeType.Methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == paramCount))
            ?? throw new InvalidOperationException($"MatchReplayRecorderRuntime.{name} not found."));

        var configure                    = Imp("Configure");
        var recordMatchReplayPacket      = Imp("RecordPacket", 2);
        var recordLocalCast              = Imp("RecordLocalCast");
        var recordLocalProjectileInfo    = Imp("RecordLocalProjectileInfo");
        var recordLocalProjectileMove    = Imp("RecordLocalProjectileMove");
        var recordLocalProjectileDrop    = Imp("RecordLocalProjectileDrop");
        var recordLocalUnitMove          = Imp("RecordLocalUnitMove");
        var recordLocalUnitProjectileHit = Imp("RecordLocalUnitProjectileHit");

        // Inject Configure(...) at MainMenu.Start
        var mainMenuType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MainMenu")
            ?? throw new InvalidOperationException("MainMenu not found.");
        var startMethod = mainMenuType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody)
            ?? throw new InvalidOperationException("MainMenu.Start not found.");

        if (!HasHelperCall(startMethod, "Configure"))
        {
            var il = startMethod.Body.GetILProcessor();
            var first = startMethod.Body.Instructions[0];

            // Configure(true, maxPayloadBytes, capturePayload, recordCustomGames, recordCasualGames, recordRankedGames)
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, maxPayloadBytes));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, capturePayload ? 1 : 0));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, recordCustomGames ? 1 : 0));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, recordCasualGames ? 1 : 0));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, recordRankedGames ? 1 : 0));
            il.InsertBefore(first, il.Create(OpCodes.Call, configure));

            // Also inject ReplayPlayerRuntime.EnsureInstance() so the replay player is available
            if (replayPlayerType is not null)
            {
                var ensureInstance = replayPlayerType.Methods.FirstOrDefault(static m => m.Name == "EnsureInstance");
                if (ensureInstance is not null)
                {
                    var importedEnsure = context.TargetModule.ImportReference(ensureInstance);
                    il.InsertBefore(first, il.Create(OpCodes.Call, importedEnsure));
                }
            }
        }

        // Patch all Protocol.ServiceZone and Protocol.ServiceChat Recv_* methods
        PatchRecvMethods(context, "Protocol.ServiceZone", recordMatchReplayPacket);
        PatchRecvMethods(context, "Protocol.ServiceChat", recordMatchReplayPacket);

        // Patch local outgoing calls in Protocol.ServiceZone
        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.FullName == "Protocol.ServiceZone")
            ?? throw new InvalidOperationException("Protocol.ServiceZone not found.");

        InjectServiceZoneRecorder(serviceZoneType, "Cast",              recordLocalCast,              1);
        InjectServiceZoneRecorder(serviceZoneType, "CreateProjectile",  recordLocalProjectileInfo,    2);
        InjectServiceZoneRecorder(serviceZoneType, "MoveProjectile",    recordLocalProjectileMove,    3);
        InjectServiceZoneRecorder(serviceZoneType, "DropProjectile",    recordLocalProjectileDrop,    1);
        InjectServiceZoneRecorder(serviceZoneType, "UnitMove",          recordLocalUnitMove,          1);
        InjectServiceZoneRecorder(serviceZoneType, "UnitProjectileHit", recordLocalUnitProjectileHit, 2);
    }

    private static void PatchRecvMethods(ExperimentalPatchContext context, string typeFullName, MethodReference recorder)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.FullName == typeFullName);
        if (type is null) return;

        foreach (var method in type.Methods.Where(static m =>
            m.Name.StartsWith("Recv_") && m.HasBody &&
            m.Parameters.Count >= 1 &&
            m.Parameters[0].ParameterType.FullName == "System.IO.BinaryReader"))
        {
            if (HasHelperCall(method, "RecordPacket"))
            {
                continue;
            }

            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[0];
            il.InsertBefore(first, il.Create(OpCodes.Ldstr, method.Name));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Call, recorder));
        }
    }

    private static void InjectServiceZoneRecorder(TypeDefinition serviceZoneType, string methodName, MethodReference recorder, int argCount)
    {
        var method = serviceZoneType.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is null) return;
        if (HasHelperCall(method, recorder.Name)) return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];

        il.InsertBefore(first, il.Create(OpCodes.Ldstr, $"ServiceZone.{methodName}"));
        for (var i = 1; i <= argCount; i++)
        {
            il.InsertBefore(first, i switch
            {
                1 => il.Create(OpCodes.Ldarg_1),
                2 => il.Create(OpCodes.Ldarg_2),
                3 => il.Create(OpCodes.Ldarg_3),
                _ => throw new InvalidOperationException($"Too many args for {methodName}")
            });
        }
        il.InsertBefore(first, il.Create(OpCodes.Call, recorder));
    }

    private static bool HasHelperCall(MethodDefinition method, string helperMethodName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == helperMethodName &&
            mr.DeclaringType.Name == "MatchReplayRecorderRuntime");
    }
}
